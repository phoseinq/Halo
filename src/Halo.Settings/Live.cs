using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Halo.Settings;

internal static class Live
{
    internal enum State { Neutral, Enabled, Attention }

    internal static bool Costly(string key) => key is "hooks.claude" or "hooks.codex" or "access.startup";

    private static readonly object CacheGate = new();
    private static readonly Dictionary<string, (string Value, long At)> Cache = new(StringComparer.Ordinal);
    internal const int FreshMs = 3000;

    private static int _generation;

    internal static string? Peek(string key)
    {
        lock (CacheGate)
            return Cache.TryGetValue(key, out var hit) && (_primed || Environment.TickCount64 - hit.At < FreshMs)
                ? hit.Value : null;
    }

    internal static void Warm(IEnumerable<string> keys, Action done)
    {
        var wanted = keys.Where(Costly).Where(k => Peek(k) is null).Distinct().ToArray();
        if (wanted.Length == 0) return;
        int generation;
        lock (CacheGate) generation = _generation;

        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var read = await System.Threading.Tasks.Task.WhenAll(wanted.Select(key =>
                    System.Threading.Tasks.Task.Run(() => (Key: key, Value: Guarded(key)))));

                long at = Environment.TickCount64;
                lock (CacheGate)
                {

                    if (_generation != generation) return;
                    foreach (var (key, value) in read) Cache[key] = (value, at);
                }
                done();
            }

            catch { }
        });
    }

    private static string Guarded(string key)
    {
        try { return Read(new Row(key, "", "", RowKind.Status, "", [])); }
        catch { return Unavailable; }
    }

    internal static void Forget()
    {
        lock (CacheGate) { Cache.Clear(); _primed = false; _generation++; }
    }

    private static bool _primed;

    internal static void Prime(IEnumerable<string> keys)
    {
        foreach (var key in keys.Where(Costly).Distinct())
        {
            string value = Guarded(key);
            lock (CacheGate) Cache[key] = (value, Environment.TickCount64);
        }
        lock (CacheGate) _primed = true;
    }

    internal static string Value(Row row)
    {
        if (!Costly(row.Key)) return Read(row);
        if (Peek(row.Key) is string cached) return cached;
        string value = Read(row);
        lock (CacheGate) Cache[row.Key] = (value, Environment.TickCount64);
        return value;
    }

    private static string Read(Row row) => row.Key switch
    {

        "api.token" => Token,
        "about.version" => Version,
        "appearance.fpsMeasured" => Rate,

        "access.startup" => StartupAnswer switch
        {
            0 => "On",
            3 => "Turned off in Windows",

            2 => Halo.Interop.AppModel.IsPackaged ? "Off" : "Missing",
            _ => Unavailable,
        },

        "hooks.claude" => HookState("claude"),
        "hooks.codex" => HookState("codex"),

        "reset.everything" => "Reverses everything",
        "access.notifications" => "Managed by Windows",
        _ => row.Fallback,
    };

    internal const string Checking = "Checking...";
    internal const string Unavailable = "Unavailable";

    internal static string ActionLabel(Row row) => row.Key switch
    {
        "hooks.claude" or "hooks.codex" => HookAction(Peek(row.Key)),
        _ => row.ActionLabel,
    };

    internal static string HookAction(string? reading) => reading switch
    {
        "Connected" => "Disconnect",
        "Not connected" or "Disconnected" => "Connect",

        Unavailable => "Retry",
        _ => Checking,
    };

    internal static State Tone(string value) => value.ToLowerInvariant() switch
    {
        "on" or "allowed" or "watching" or "connected" => State.Enabled,
        "off" or "missing" or "denied" or "needs access" => State.Attention,

        "disconnected" or "not connected" or "unavailable" => State.Neutral,
        _ => State.Neutral,
    };

    private static string Token
    {
        get
        {
            try
            {
                var store = new Store();
                string token = store.Text("api.token", "");
                return token.Length >= 8 ? token[..4] + "..." + token[^4..] : "Not generated yet";
            }
            catch { return "Not generated yet"; }
        }
    }

    private static string Version
    {
        get
        {
            try { return typeof(Live).Assembly.GetName().Version?.ToString(3) ?? "unknown"; }
            catch { return "unknown"; }
        }
    }

    private static string Rate
    {
        get
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "fps");
                var parts = File.ReadAllText(path).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                int measured = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
                int hz = parts.Length > 1 && int.TryParse(parts[1], out var h) ? h : 0;
                return Describe(measured, hz);
            }
            catch { return NotMeasured; }
        }
    }

    internal const string NotMeasured = "Not measured yet";

    internal static string Describe(int measured, int hz)
    {
        if (measured > 0 && hz > 0) return $"{measured} fps on a {hz} Hz display";
        if (measured > 0) return $"{measured} fps";
        if (hz > 0) return $"{hz} Hz display";
        return NotMeasured;
    }

    internal static string HookReading(string which) => HookState(which, 8000);

    private static string HookState(string which, int timeoutMs = 4000)
    {
        string agent = which == "codex" ? "Codex" : "Claude Code";
        if (string.Equals(Halo.ClaudeCode.HookMarks.Of(agent), Halo.ClaudeCode.HookMarks.Undone,
                System.StringComparison.OrdinalIgnoreCase))
            return "Disconnected";
        int code = Hooks("query-" + which + "-hooks", timeoutMs);

        return code switch { 0 => "Connected", 2 => "Not connected", _ => Unavailable };
    }

    internal const int CouldNotRun = -1;

    internal static int Hooks(string verb, int timeoutMs = 4000)
    {
        try
        {
            string exe = Path.Combine(AppContext.BaseDirectory, "Halo.Hooks.exe");
            if (!File.Exists(exe)) return CouldNotRun;
            var psi = new System.Diagnostics.ProcessStartInfo(exe)
            { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add(verb);
            using var p = System.Diagnostics.Process.Start(psi);
            if (p == null) return CouldNotRun;

            if (!p.WaitForExit(timeoutMs)) { try { p.Kill(entireProcessTree: true); } catch { } return CouldNotRun; }
            return p.ExitCode;
        }
        catch { return CouldNotRun; }
    }

    private static int StartupAnswer => Hooks("query-autostart");
}
