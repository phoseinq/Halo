using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Halo.Launcher;

internal sealed record Reminder(string Id, DateTimeOffset When, string Text);

internal static class ReminderStore
{
    internal const string ActPrefix = "rem:";

    internal static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "reminders.txt");

    internal static string Format(Reminder r)
        => r.Id + "|" + r.When.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + "|"
           + r.Text.Replace('\r', ' ').Replace('\n', ' ');

    internal static Reminder? Parse(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        int a = line.IndexOf('|');
        if (a <= 0) return null;
        int b = line.IndexOf('|', a + 1);
        if (b <= a) return null;
        if (!long.TryParse(line[(a + 1)..b], NumberStyles.Integer, CultureInfo.InvariantCulture, out long secs))
            return null;
        string text = line[(b + 1)..].Trim();
        if (text.Length == 0) return null;
        return new Reminder(line[..a], DateTimeOffset.FromUnixTimeSeconds(secs), text);
    }

    internal readonly record struct ReminderCommand(DateTimeOffset When, string Text);

    internal static ReminderCommand? ParseCommand(string input, DateTimeOffset now, out string? complaint)
    {
        complaint = null;
        string s = (input ?? "").Trim();
        if (s.Length == 0) return null;

        if (Is(s, "in")) { complaint = "how long? try: in 20m walk the dog"; return null; }
        if (Is(s, "at")) { complaint = "at what time? try: at 17:30 call mum"; return null; }
        if (Is(s, "tomorrow")) { complaint = "tomorrow at what time? try: tomorrow 9am dentist"; return null; }

        if (Head(s, "in") is { } after) return Relative(after, now, ref complaint);
        if (Head(s, "at") is { } clock) return Clock(clock, now, sameDayOnly: false, ref complaint);
        if (Head(s, "tomorrow") is { } tom) return Clock(tom, now.AddDays(1), sameDayOnly: true, ref complaint);
        return null;
    }

    private static bool Is(string s, string word) => string.Equals(s, word, StringComparison.OrdinalIgnoreCase);

    private static string? Head(string s, string word)
        => s.StartsWith(word + " ", StringComparison.OrdinalIgnoreCase) ? s[(word.Length + 1)..].TrimStart() : null;

    private static ReminderCommand? Relative(string rest, DateTimeOffset now, ref string? complaint)
    {
        var (span, used) = ReadDuration(rest);
        if (span is not { } after || after <= TimeSpan.Zero)
        { complaint = "try: in 20m walk the dog"; return null; }
        string text = rest[used..].Trim();
        if (text.Length == 0) { complaint = "remind you of what?"; return null; }
        return new ReminderCommand(now + after, text);
    }

    private static ReminderCommand? Clock(string rest, DateTimeOffset day, bool sameDayOnly, ref string? complaint)
    {
        var (time, used) = ReadClock(rest);
        if (time is not { } at) { complaint = "try: at 17:30 call mum"; return null; }
        string text = rest[used..].Trim();
        if (text.Length == 0) { complaint = "remind you of what?"; return null; }
        var when = new DateTimeOffset(day.Year, day.Month, day.Day, 0, 0, 0, day.Offset) + at;

        if (!sameDayOnly && when <= day) when = when.AddDays(1);
        return new ReminderCommand(when, text);
    }

    internal static (TimeSpan? Span, int Used) ReadDuration(string s)
    {
        var total = TimeSpan.Zero;
        int i = 0, used = 0;
        bool any = false;
        while (i < s.Length)
        {
            while (i < s.Length && s[i] == ' ') i++;
            int numStart = i;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i == numStart) break;
            if (!int.TryParse(s[numStart..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                break;
            while (i < s.Length && s[i] == ' ') i++;
            int unitStart = i;
            while (i < s.Length && char.IsLetter(s[i])) i++;
            if (Unit(s[unitStart..i]) is not { } scale) break;
            total += scale * n;
            any = true;
            used = i;
        }
        return (any ? total : null, used);
    }

    private static TimeSpan? Unit(string u) => u.ToLowerInvariant() switch
    {
        "s" or "sec" or "secs" or "second" or "seconds" => TimeSpan.FromSeconds(1),
        "m" or "min" or "mins" or "minute" or "minutes" => TimeSpan.FromMinutes(1),
        "h" or "hr" or "hrs" or "hour" or "hours" => TimeSpan.FromHours(1),
        "d" or "day" or "days" => TimeSpan.FromDays(1),
        _ => null,
    };

        internal static (TimeSpan? At, int Used) ReadClock(string s)
    {
        int i = 0;
        while (i < s.Length && s[i] == ' ') i++;
        int h0 = i;
        while (i < s.Length && char.IsDigit(s[i])) i++;
        if (i == h0 || i - h0 > 2) return (null, 0);
        int hour = int.Parse(s[h0..i], CultureInfo.InvariantCulture);

        int minute = 0;
        bool hadColon = false;
        if (i < s.Length && s[i] == ':')
        {
            hadColon = true;
            i++;
            int m0 = i;
            while (i < s.Length && char.IsDigit(s[i])) i++;
            if (i - m0 != 2) return (null, 0);
            minute = int.Parse(s[m0..i], CultureInfo.InvariantCulture);
        }

        int afterTime = i;
        while (i < s.Length && s[i] == ' ') i++;
        int sfx = i;
        while (i < s.Length && char.IsLetter(s[i])) i++;
        string suffix = s[sfx..i].ToLowerInvariant();
        if (suffix is "am" or "pm")
        {

            if (hour < 1 || hour > 12) return (null, 0);
            if (suffix == "pm" && hour != 12) hour += 12;
            else if (suffix == "am" && hour == 12) hour = 0;
        }
        else
        {
            i = afterTime;

            if (!hadColon) return (null, 0);
        }
        if (hour > 23 || minute > 59) return (null, 0);
        return (new TimeSpan(hour, minute, 0), i);
    }

    internal static (string Label, DateTimeOffset When)[] Choices(DateTimeOffset now)
    {
        var list = new List<(string, DateTimeOffset)>
        {
            ("in 20 minutes", now.AddMinutes(20)),
            ("in 1 hour", now.AddHours(1)),
            ("in 3 hours", now.AddHours(3)),
        };
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var evening = today.AddHours(18);
        if (evening > now) list.Add(("this evening", evening));
        list.Add(("tomorrow morning", today.AddDays(1).AddHours(9)));
        list.Add(("tomorrow evening", today.AddDays(1).AddHours(18)));
        return [.. list];
    }

    internal static IReadOnlyList<Reminder> Due(IEnumerable<Reminder> all, DateTimeOffset now)
        => [.. all.Where(r => r.When <= now)];

    internal static IReadOnlyList<Reminder> Pending(IEnumerable<Reminder> all, DateTimeOffset now)
        => [.. all.Where(r => r.When > now).OrderBy(r => r.When)];

    internal static IReadOnlyList<Reminder> Load()
    {
        try
        {
            if (!File.Exists(DefaultPath)) return [];
            var list = new List<Reminder>();
            foreach (var line in File.ReadAllLines(DefaultPath))
                if (Parse(line) is { } r) list.Add(r);
            return [.. list.OrderBy(r => r.When)];
        }
        catch { return []; }
    }

    internal static void Save(IEnumerable<Reminder> all)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath)!);
            File.WriteAllLines(DefaultPath, all.Select(Format));
        }
        catch { }
    }

    internal static Reminder Add(DateTimeOffset when, string text)
    {
        var r = new Reminder(Guid.NewGuid().ToString("N")[..8], when, text);
        var all = new List<Reminder>(Load()) { r };
        Save(all);
        return r;
    }

    internal static string Describe(Reminder r, DateTimeOffset now)
    {
        var when = r.When.ToLocalTime();
        var today = now.ToLocalTime().Date;
        string clock = when.ToString("HH:mm", CultureInfo.InvariantCulture);
        string day = when.Date == today ? ""
                   : when.Date == today.AddDays(1) ? "tomorrow "
                   : when.ToString("ddd d MMM ", CultureInfo.InvariantCulture);
        var left = r.When - now;
        string near = left < TimeSpan.FromMinutes(1) ? "under a minute"
                    : left < TimeSpan.FromHours(1) ? $"{(int)left.TotalMinutes}m"
                    : left < TimeSpan.FromDays(1) ? $"{(int)left.TotalHours}h {left.Minutes}m"
                    : $"{(int)left.TotalDays}d";
        return $"{day}{clock}  -  in {near}";
    }

    internal static void Remove(string id)
        => Save(Load().Where(r => r.Id != id));
}
