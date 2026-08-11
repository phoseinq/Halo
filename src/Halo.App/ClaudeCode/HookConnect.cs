using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Halo.ClaudeCode;

internal static class HookConnect
{
    internal enum Step
    {
        Wait,
        Install,
    }

    internal static Step Next(bool busy, bool alreadyTried, bool undone, Func<bool> agentSeen, Func<bool?> hooksInstalled)
    {
        if (busy || alreadyTried || undone) return Step.Wait;
        if (!agentSeen()) return Step.Wait;
        return hooksInstalled() switch { false => Step.Install, _ => Step.Wait };
    }

    internal static string? MarkFor(bool installed) => installed ? HookMarks.Done : null;

    internal const int MaxAttempts = 4;

    internal static int RetryDelayMs(int attempt) => attempt switch
    {
        <= 1 => 15_000,
        2 => 60_000,
        _ => 240_000,
    };

    internal static bool ShouldReport(int attempt) => attempt >= MaxAttempts;

    internal static string Short(string path)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && path.StartsWith(home, StringComparison.OrdinalIgnoreCase)
            ? "~" + path[home.Length..]
            : path;
    }

    internal static (string App, string Title, string Body) Notice(string agent, string settingsPath) =>
        (agent,
         Halo.Localization.Strings.Format("hooks.connected.title", agent),
         Halo.Localization.Strings.Format("hooks.connected.body", Short(settingsPath)));

    internal static (string App, string Title, string Body) Failed(string agent, string why, bool wrote = true)
    {

        string what = Halo.Localization.Strings.Get(wrote ? "hooks.failed.write" : "hooks.failed.run");
        return (agent,
                Halo.Localization.Strings.Format("hooks.failed.title", agent),
                string.IsNullOrWhiteSpace(why)
                    ? Halo.Localization.Strings.Format("hooks.failed.body", what)
                    : Halo.Localization.Strings.Format("hooks.failed.bodyWhy", what, why));
    }

    private static readonly (string Agent, string[] Processes)[] Agents =
    [
        ("Claude Code", ["claude"]),
        ("Codex", ["codex", "ChatGPT"]),
    ];

    private static readonly object Gate = new();
    private static long _nextScan;
    private static readonly HashSet<string> Busy = [];

    private static readonly HashSet<string> Settled = [];

    private static readonly Dictionary<string, int> Attempts = [];
    private static readonly Dictionary<string, long> RetryAt = [];

    private static bool IsSettled(string agent) { lock (Gate) return Settled.Contains(agent); }

    private static readonly Dictionary<string, string> LastLine = [];

    private static bool _on;
    private static long _onAt;

    private static void Log(string agent, string line)
    {
        try
        {
            long now = Environment.TickCount64;
            lock (Gate)
            {

                if (now - _onAt >= 5000)
                {
                    _onAt = now;
                    bool was = _on;
                    _on = File.Exists(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Halo", "hooks-debug.on"));

                    if (_on && !was) LastLine.Clear();
                }
                if (!_on) return;
                if (LastLine.TryGetValue(agent, out var previous) && previous == line) return;
                LastLine[agent] = line;
            }
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "hooks-debug.txt");
            var f = new FileInfo(path);
            if (f.Exists && f.Length > 200_000) f.Delete();
            Halo.Reports.DebugFile.Append(path, DateTime.Now.ToString("HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture) + "  " + line + Environment.NewLine);
        }
        catch { }
    }

    private static void NoteFailure(string agent, string why, Action<string, string, string, bool> notify,
        bool wrote = true)
    {
        int attempt;
        lock (Gate)
        {
            Attempts.TryGetValue(agent, out attempt);
            Attempts[agent] = ++attempt;
            if (attempt >= MaxAttempts) Settled.Add(agent);
            else RetryAt[agent] = Environment.TickCount64 + RetryDelayMs(attempt);
        }
        if (!ShouldReport(attempt)) return;
        var (a, t, b) = Failed(agent, why, wrote);
        notify(a, t, b, false);
    }

    private static void NoteSuccess(string agent)
    {
        lock (Gate)
        {
            Settled.Add(agent);
            Attempts.Remove(agent);
            RetryAt.Remove(agent);
        }
    }

    internal static string HookExe()
    {
        if (Halo.Interop.AppModel.IsPackaged)
        {
            string stub = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps", "Halo.Hooks.exe");
            if (File.Exists(stub)) return stub;
        }
        return Path.Combine(AppContext.BaseDirectory, "Halo.Hooks.exe");
    }

    private static bool Running(string[] names)
    {
        foreach (var n in names)
        {
            try { if (Process.GetProcessesByName(n).Length > 0) return true; }
            catch { }
        }
        return false;
    }

    internal static void Tick(Action<string, string, string, bool> notify)
    {
        long now = Environment.TickCount64;
        lock (Gate)
        {
            if (now < _nextScan) return;
            _nextScan = now + 5000;
        }

        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            foreach (var (agent, processes) in Agents)
            {
                try
                {

                    int waiting = -1;
                    lock (Gate)
                    {

                        if (!Busy.Add(agent)) continue;

                        if (RetryAt.TryGetValue(agent, out long at) && Environment.TickCount64 < at)
                            waiting = (int)((at - Environment.TickCount64) / 1000);
                    }
                    if (waiting >= 0)
                    {

                        Log(agent, $"{agent}: backing off, {waiting}s to go");
                        continue;
                    }
                    string mark = HookMarks.Of(agent);

                    bool probed = false;
                    bool? answer = null;
                    var step = Next(
                        busy: false,

                        alreadyTried: IsSettled(agent),
                        undone: string.Equals(mark, HookMarks.Undone, StringComparison.OrdinalIgnoreCase),
                        agentSeen: () => { bool up = Running(processes); if (!up) Log(agent, $"{agent}: not running"); return up; },

                        hooksInstalled: () =>
                        {
                            probed = true;
                            int code = Query(agent);
                            return answer = code switch { 0 => true, 2 => false, _ => (bool?)null };
                        });

                    Log(agent, $"{agent}: mark={(mark.Length == 0 ? "-" : mark)} settled={IsSettled(agent)} probed={probed} "
                        + $"answer={answer?.ToString() ?? "-"} step={step}");
                    if (answer == true) NoteSuccess(agent);
                    if (step != Step.Install)
                    {
                        if (probed && answer is null)
                            NoteFailure(agent, "", notify, wrote: false);
                        continue;
                    }

                    {
                        var (ok, why, path) = Install(agent);
                        if (MarkFor(ok) is string mark2) HookMarks.Write(agent, mark2);
                        Log(agent, $"{agent}: install ok={ok} why={(why.Length == 0 ? "-" : why)}");
                        if (ok)
                        {
                            NoteSuccess(agent);
                            var (a, t, b) = Notice(agent, path);
                            notify(a, t, b, true);
                        }
                        else NoteFailure(agent, why, notify);
                    }
                }

                catch (Exception e) { Log(agent, $"{agent}: threw {e.GetType().Name}: {e.Message}"); }

                finally { lock (Gate) Busy.Remove(agent); }
            }
        });
    }

    internal const int CouldNotRun = -1;

    private static int Query(string agent)
    {
        try
        {
            var psi = new ProcessStartInfo(HookExe()) { UseShellExecute = false, CreateNoWindow = true };
            psi.ArgumentList.Add(agent == "Codex" ? "query-codex-hooks" : "query-claude-hooks");
            using var p = Process.Start(psi);
            if (p == null) return CouldNotRun;

            if (!p.WaitForExit(15_000)) { try { p.Kill(entireProcessTree: true); } catch { } return CouldNotRun; }
            return p.ExitCode;
        }
        catch { return CouldNotRun; }
    }

    private static (bool Ok, string Why, string Path) Install(string agent)
    {

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string settings = agent == "Codex"
            ? Path.Combine(home, ".codex", "hooks.json")
            : Path.Combine(home, ".claude", "settings.json");
        try
        {
            string exe = HookExe();
            var psi = new ProcessStartInfo(exe)
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            psi.ArgumentList.Add(agent == "Codex" ? "install-codex-hooks" : "install-claude-hooks");
            psi.ArgumentList.Add(exe);
            using var p = Process.Start(psi);
            if (p == null) return (false, "the helper did not start", settings);

            var err = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(30_000))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                return (false, "timed out", settings);
            }

            string why = "";
            try { if (err.Wait(2_000)) why = err.Result.Trim(); } catch { }
            return (p.ExitCode == 0, why, settings);
        }
        catch (Exception e) { return (false, e.Message, settings); }
    }
}
