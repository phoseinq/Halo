using System;
using System.Collections.Generic;
using System.IO;

namespace Halo.Reports;

internal static class ReportStore
{
    internal const int MaxFiles = 10;
    internal const long MaxBytes = 2 * 1024 * 1024;

    internal static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "reports");

    internal static string Write(string json, string kind)
    {
        Directory.CreateDirectory(Dir);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss",
            System.Globalization.CultureInfo.InvariantCulture);
        string path = Path.Combine(Dir, $"{kind}-{stamp}.json");
        for (int n = 2; File.Exists(path) && n < 100; n++)
            path = Path.Combine(Dir, $"{kind}-{stamp}-{n}.json");
        File.WriteAllText(path, json);
        Prune();
        return path;
    }

    internal static string SentMarker(string reportPath) => reportPath + ".sent";

    internal static bool WasSent(string reportPath)
    {
        try { return File.Exists(SentMarker(reportPath)); }
        catch { return false; }
    }

    internal static void MarkSent(string reportPath)
    {
        try { File.WriteAllText(SentMarker(reportPath), DateTime.UtcNow.ToString("o")); }
        catch { }
    }

    internal static IReadOnlyList<FileInfo> List()
    {
        try
        {
            var files = new DirectoryInfo(Dir).GetFiles("*.json");
            Array.Sort(files, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            return files;
        }
        catch { return []; }
    }

    internal static void Prune()
    {
        try
        {
            var files = List();
            long total = 0;
            for (int i = 0; i < files.Count; i++)
            {
                total += files[i].Length;
                if (i < MaxFiles && total <= MaxBytes) continue;

                try { File.Delete(SentMarker(files[i].FullName)); } catch { }
                try { files[i].Delete(); } catch { }
            }
        }
        catch { }
    }

    internal static void Clear()
    {
        try { Directory.Delete(Dir, recursive: true); } catch { }

        foreach (var name in new[] { "crash-sent", "hooks-debug.txt", "hooks-debug.on" })
        {
            try { File.Delete(Path.Combine(Path.GetDirectoryName(Dir)!, name)); } catch { }
        }
    }
}
