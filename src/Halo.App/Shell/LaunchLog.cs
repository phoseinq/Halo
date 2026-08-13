using System;
using System.Globalization;
using System.IO;

namespace Halo.Shell;

internal static class LaunchLog
{

    private const int CapBytes = 64 * 1024;

    private static readonly string LogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "launch-debug.txt");

    internal static void Launch(bool won, bool askedForSettings, double? winnerAgeSeconds, bool openPanel)
        => Write(LaunchLine(DateTime.Now, Environment.ProcessId, won, askedForSettings, winnerAgeSeconds, openPanel));

    internal static void Panel(string reason, bool started, bool stamped)
        => Write(PanelLine(DateTime.Now, reason, started, stamped));

    internal static string LaunchLine(
        DateTime now, int pid, bool won, bool askedForSettings, double? winnerAgeSeconds, bool openPanel)
    {
        string age = winnerAgeSeconds is { } seconds
            ? seconds.ToString("0.0", CultureInfo.InvariantCulture) + "s"
            : "?";
        return won
            ? $"{Stamp(now)} launch pid={pid} won asked={YesNo(askedForSettings)}\r\n"
            : $"{Stamp(now)} launch pid={pid} lost winnerAge={age} asked={YesNo(askedForSettings)} "
                + $"panel={YesNo(openPanel)}\r\n";
    }

    internal static string PanelLine(DateTime now, string reason, bool started, bool stamped)
        => $"{Stamp(now)} panel reason={reason} started={YesNo(started)} stamp={YesNo(stamped)}\r\n";

    private static string Stamp(DateTime now)
        => now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static void Write(string line)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LogPath)!);
            Halo.Reports.DebugFile.Append(LogPath, line, CapBytes);
        }
        catch { }
    }
}
