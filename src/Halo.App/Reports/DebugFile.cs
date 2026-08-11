using System;
using System.Collections.Generic;
using System.IO;

namespace Halo.Reports;

internal static class DebugFile
{
    internal const int DefaultCapBytes = 256 * 1024;

    private const int CheckEvery = 50;
    private static readonly object Lock = new();
    private static readonly Dictionary<string, int> Writes = new(StringComparer.OrdinalIgnoreCase);

    internal static void Append(string path, string line, int capBytes = DefaultCapBytes)
    {
        try
        {
            bool check;
            lock (Lock)
            {
                Writes.TryGetValue(path, out int n);
                check = n % CheckEvery == 0;
                Writes[path] = n + 1;
            }
            if (check) Trim(path, capBytes);
            File.AppendAllText(path, line);
        }
        catch { }
    }

        internal static void Trim(string path, int capBytes = DefaultCapBytes)
    {
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists || fi.Length <= capBytes) return;
            int keep = Math.Max(1024, capBytes / 2);
            byte[] tail;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                fs.Seek(-keep, SeekOrigin.End);
                tail = new byte[keep];
                int got = 0;
                while (got < keep)
                {
                    int r = fs.Read(tail, got, keep - got);
                    if (r <= 0) break;
                    got += r;
                }
                if (got < keep) Array.Resize(ref tail, got);
            }

            int nl = Array.IndexOf(tail, (byte)'\n');
            int start = nl >= 0 ? nl + 1 : tail.Length;
            using var w = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            w.Write(tail, start, tail.Length - start);
        }
        catch { }
    }
}
