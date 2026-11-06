using System;
using System.Collections.Generic;

namespace Halo.Shell;

internal static class TabMatch
{

    public static int Find(string? recorded, IReadOnlyList<string?> tabs)
    {
        if (tabs is null || tabs.Count == 0) return -1;
        string want = Clean(recorded);
        if (want.Length == 0) return -1;

        int hit = -1;
        for (int i = 0; i < tabs.Count; i++)
        {
            if (!Clean(tabs[i]).Equals(want, StringComparison.Ordinal)) continue;
            if (hit >= 0) return -1;
            hit = i;
        }
        if (hit >= 0) return hit;

        for (int i = 0; i < tabs.Count; i++)
        {
            string t = Clean(tabs[i]);
            if (t.Length == 0) continue;
            int n = Common(want, t);
            if (n < MinPrefix || n < Math.Min(want.Length, t.Length) / 2) continue;
            if (hit >= 0) return -1;
            hit = i;
        }
        return hit;
    }

    private const int MinPrefix = 12;

    internal static string Clean(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        bool started = false, gap = false;
        foreach (char c in s)
        {

            bool keep = char.IsLetterOrDigit(c) || c == '/' || c == '\\' || c == '.' || c == '_';
            if (!keep)
            {

                if (started) gap = true;
                continue;
            }
            if (gap && started) sb.Append(' ');
            gap = false;
            started = true;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static int Common(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length), i = 0;
        while (i < n && a[i] == b[i]) i++;
        return i;
    }
}
