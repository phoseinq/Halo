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
                try { files[i].Delete(); } catch { }
            }
        }
        catch { }
    }

    internal static void Clear()
    {
        try { Directory.Delete(Dir, recursive: true); } catch { }
    }
}
