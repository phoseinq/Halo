using System;
using System.Collections.Generic;

namespace Halo.Notifications;

internal static class LiveText
{

    internal const int MaxLines = 6;

        internal static bool CanFold(NotifItem live, NotifItem next)

        => live.Aumid.Length > 0
        && string.Equals(live.Aumid, next.Aumid, StringComparison.OrdinalIgnoreCase)
        && string.Equals(live.Title, next.Title, StringComparison.Ordinal)

        && live.Kind.Length == 0 && next.Kind.Length == 0
        && live.Preview is null && next.Preview is null
        && live.Code.Length == 0 && next.Code.Length == 0
        && next.Body.Trim().Length > 0;

        internal static string Append(string body, string line)
    {
        line = (line ?? "").Trim();
        if (line.Length == 0) return body ?? "";
        var lines = new List<string>((body ?? "").Split('\n'));
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            lines[i] = lines[i].Trim();
            if (lines[i].Length == 0) lines.RemoveAt(i);
        }
        if (lines.Count == 0) return line;

        string last = lines[^1];
        if (string.Equals(last, line, StringComparison.Ordinal)) return string.Join("\n", lines);
        if (line.Contains(last, StringComparison.Ordinal)) lines[^1] = line;
        else lines.Add(line);

        if (lines.Count > MaxLines) lines.RemoveRange(0, lines.Count - MaxLines);
        return string.Join("\n", lines);
    }

        internal static double Extend(double duration)

        => Math.Min(12, duration + 1.5);
}
