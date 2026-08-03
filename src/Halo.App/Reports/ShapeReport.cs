using System;
using System.IO;

namespace Halo.Reports;

internal static class ShapeReport
{
    internal static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "shape");

    private const int WriteEveryMs = 2000;
    private static long _lastAt;
    private static string _written = "";

    internal static bool Due => Environment.TickCount64 - _lastAt >= WriteEveryMs;

    internal static string Format(string primary, string[] live, bool expanded, bool heavy, int tier)
        => $"primary={primary}\nlive={string.Join(",", live)}\nexpanded={(expanded ? "1" : "0")}\n"
         + $"heavy={(heavy ? "1" : "0")}\ntier={tier}";

    internal static void Write(string primary, string[] live, bool expanded, bool heavy, int tier)
    {
        _lastAt = Environment.TickCount64;
        string body = Format(primary, live, expanded, heavy, tier);
        if (body == _written) return;
        _written = body;
        try
        {
            string path = Path;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, body);
        }
        catch { }
    }

    internal static System.Collections.Generic.Dictionary<string, string> Read()
    {
        var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var line in File.ReadAllLines(Path))
            {
                int eq = line.IndexOf('=');
                if (eq > 0) map[line[..eq]] = line[(eq + 1)..];
            }
        }
        catch { }
        return map;
    }
}
