using System;
using System.Text;

namespace Halo.Widgets;

internal static class HistoryText
{

    public static string When(DateTime t, DateTime now)
    {
        var d = now - t;
        if (d < TimeSpan.Zero) d = TimeSpan.Zero;
        if (d.TotalSeconds < 45) return Localization.Strings.Get("history.now");
        if (d.TotalMinutes < 60) return $"{(int)Math.Round(d.TotalMinutes)}m";
        if (t.Date == now.Date) return t.ToString("HH:mm");
        if ((now.Date - t.Date).TotalDays < 7) return t.ToString("ddd HH:mm");
        return t.ToString("d MMM HH:mm");
    }

    public static string Gist(string? body, int maxChars = 58)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        string s = Strip(body);
        if (s.Length == 0) return "";

        int cut = -1;
        for (int i = 8; i < s.Length; i++)
        {
            char c = s[i];

            if (c != '.' && c != '!' && c != '?' && c != ':' && c != '\u061F') continue;
            if (i + 1 < s.Length && !char.IsWhiteSpace(s[i + 1])) continue;
            cut = i + 1;
            break;
        }
        if (cut > 0 && cut <= maxChars) return s[..cut].Trim();

        if (s.Length > maxChars)
        {
            int comma = s.LastIndexOf(',', Math.Min(maxChars, s.Length - 1));
            if (comma > maxChars / 2) return s[..(comma + 1)].Trim();
        }
        return Clip(s, maxChars);
    }

    public static string Full(string? body) => string.IsNullOrWhiteSpace(body) ? "" : Strip(body, keepLines: true);

    public static bool HasMore(string? body, int maxChars = 96)
    {
        string full = string.IsNullOrWhiteSpace(body) ? "" : Strip(body);
        return full.Length > 0 && full != Gist(body, maxChars);
    }

    private static string Strip(string body, bool keepLines = false)
    {
        var sb = new StringBuilder(body.Length);
        foreach (var raw in body.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('>')) continue;
            if (line == "--" || line == "—") break;
            if (line.StartsWith("On ") && line.EndsWith("wrote:")) break;
            if (sb.Length > 0) sb.Append(keepLines ? '\n' : ' ');
            sb.Append(line);

            if (!keepLines && sb.Length > 400) break;
        }

        var outp = new StringBuilder(sb.Length);
        bool gap = false;
        foreach (char c in sb.ToString())
        {
            if (c == '\n') { outp.Append('\n'); gap = false; continue; }
            if (char.IsWhiteSpace(c)) { gap = outp.Length > 0 && outp[^1] != '\n'; continue; }
            if (gap) outp.Append(' ');
            gap = false;
            outp.Append(c);
        }
        return outp.ToString();
    }

    private static string Clip(string s, int max)
    {
        if (s.Length <= max) return s;
        int at = s.LastIndexOf(' ', Math.Min(max, s.Length - 1));
        return (at > max / 2 ? s[..at] : s[..max]).TrimEnd() + "…";
    }
}
