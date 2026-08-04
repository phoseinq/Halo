using System;
using System.Collections.Generic;
using System.IO;

namespace Halo.ClaudeCode;

internal static class HookMarks
{
    internal const string Done = "done", Undone = "undone";

    internal static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "hooks-connect.txt");

    private static readonly object Gate = new();

    internal static Dictionary<string, string> Read()
    {
        var marks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            lock (Gate)
            {
                if (!File.Exists(Path)) return marks;
                foreach (var line in File.ReadAllLines(Path))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) marks[line[..eq].Trim()] = line[(eq + 1)..].Trim();
                }
            }
        }
        catch { }
        return marks;
    }

    internal static string Of(string agent)
        => Read().TryGetValue(agent, out var v) ? v : "";

    internal static void Write(string agent, string value)
    {
        try
        {
            lock (Gate)
            {
                var marks = Read();
                marks[agent] = value;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                var lines = new List<string>();
                foreach (var (k, v) in marks) lines.Add($"{k}={v}");
                File.WriteAllLines(Path, lines);
            }
        }
        catch { }
    }
}
