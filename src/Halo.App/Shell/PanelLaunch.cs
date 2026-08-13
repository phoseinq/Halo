using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Halo.Shell;

internal static class PanelLaunch
{

    internal const double RequestFreshSeconds = 30.0;

    private static string StatePath(string name) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", name);

    internal static bool ShouldShow(bool requested) => requested;

    internal static bool Request()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath("panel-request"))!);
            File.WriteAllText(StatePath("panel-request"),
                DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            return true;
        }
        catch { return false; }
    }

    internal static bool TakeRequest()
    {
        try
        {
            string path = StatePath("panel-request");
            if (!File.Exists(path)) return false;
            string text = File.ReadAllText(path).Trim();
            File.Delete(path);
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks))
                return false;
            return Fresh((DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds);
        }
        catch { return false; }
    }

    internal static bool Fresh(double ageSeconds) => ageSeconds is >= -5.0 and <= RequestFreshSeconds;

    internal static double? SessionAgeSeconds()
    {
        try
        {
            using var me = Process.GetCurrentProcess();
            int session = me.SessionId;
            double? oldest = null;
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                try
                {
                    if (p.SessionId != session) continue;
                    double age = (DateTime.Now - p.StartTime).TotalSeconds;
                    if (age >= 0 && (oldest is null || age > oldest)) oldest = age;
                }
                catch { }
                finally { p.Dispose(); }
            }
            return oldest;
        }
        catch { return null; }
    }

    internal static string RefusedLine(DateTime now, double? sessionAgeSeconds)
        => $"{Stamp(now)} panel refused=unrequested sessionAge={Age(sessionAgeSeconds)}\r\n";

    internal static string Age(double? seconds) => seconds is { } s
        ? s.ToString("0.0", CultureInfo.InvariantCulture) + "s"
        : "?";

    private static string Stamp(DateTime now)
        => now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        internal static void LogProbe(string text) => Append($"{Stamp(DateTime.Now)} panel probe {text}\r\n");

    internal static void LogRefused(double? sessionAgeSeconds)
        => Append(RefusedLine(DateTime.Now, sessionAgeSeconds));

    private static void Append(string line)
    {
        try
        {
            string path = StatePath("launch-debug.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, line);
        }
        catch { }
    }
}
