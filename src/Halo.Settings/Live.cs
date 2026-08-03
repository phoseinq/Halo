using System;
using System.IO;

namespace Halo.Settings;

internal static class Live
{
    internal enum State { Neutral, Enabled, Attention }

    internal static string Value(Row row) => row.Key switch
    {

        "api.token" => Token,
        "about.version" => Version,
        "appearance.fpsMeasured" => Rate,
        "access.startup" => StartupTask ? "On" : "Missing",
        "access.notifications" => "Managed by Windows",
        _ => row.Fallback,
    };

    internal static State Tone(string value) => value.ToLowerInvariant() switch
    {
        "on" or "allowed" or "watching" => State.Enabled,
        "off" or "missing" or "denied" or "needs access" => State.Attention,
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

    private static bool StartupTask
    {
        get
        {
            try
            {
                string hooks = Path.Combine(AppContext.BaseDirectory, "Halo.Hooks.exe");
                if (!File.Exists(hooks)) return false;
                var psi = new System.Diagnostics.ProcessStartInfo(hooks)
                { UseShellExecute = false, CreateNoWindow = true };
                psi.ArgumentList.Add("query-autostart");
                using var p = System.Diagnostics.Process.Start(psi);
                if (p == null) return false;

                if (!p.WaitForExit(4000)) return false;
                return p.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}
