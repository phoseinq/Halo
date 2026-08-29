using System;
using System.Collections.Generic;
using System.Text;

namespace Halo.Launcher;

internal static class AppMatch
{
    internal const int MaxResults = 6;

    internal static IReadOnlyList<string> Words(string name)
    {
        var words = new List<string>();
        var cur = new StringBuilder();
        char prev = '\0';
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            char next = i + 1 < name.Length ? name[i + 1] : '\0';
            bool alnum = char.IsLetterOrDigit(c);
            bool boundary = alnum && cur.Length > 0
                && ((char.IsUpper(c) && char.IsLower(prev))
                 || (char.IsUpper(c) && char.IsUpper(prev) && char.IsLower(next))
                 || (char.IsDigit(c) != char.IsDigit(prev)));
            if (!alnum || boundary)
            {
                if (cur.Length > 0) { words.Add(cur.ToString()); cur.Clear(); }
                if (!alnum) { prev = c; continue; }
            }
            cur.Append(c);
            prev = c;
        }
        if (cur.Length > 0) words.Add(cur.ToString());
        return words;
    }

    internal static int Tier(string name, string query)
    {
        string clean = Halo.Widgets.Fx.CleanText(name);
        if (clean.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 2;
        foreach (string w in Words(clean))
            if (w.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
        return 0;
    }

    internal static IReadOnlyList<AppEntry> Top(
        IReadOnlyList<AppEntry> apps, string query, Func<string, double> score)
    {
        query = (query ?? "").Trim();
        if (query.Length == 0) return [];

        var hits = new List<(AppEntry App, int Tier, double Score)>();
        foreach (var a in apps)
        {
            int tier = Tier(a.Name, query);
            if (tier > 0) hits.Add((a, tier, score(a.Aumid)));
        }

        hits.Sort((x, y) =>
        {
            int c = y.Tier.CompareTo(x.Tier);
            if (c != 0) return c;
            c = y.Score.CompareTo(x.Score);
            if (c != 0) return c;
            c = x.App.Name.Length.CompareTo(y.App.Name.Length);
            if (c != 0) return c;
            return string.CompareOrdinal(x.App.Name, y.App.Name);
        });

        var top = new List<AppEntry>(MaxResults);
        for (int i = 0; i < hits.Count && i < MaxResults; i++) top.Add(hits[i].App);
        return top;
    }
}
