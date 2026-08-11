using System;
using System.Collections.Generic;
using System.Globalization;

namespace Halo.Widgets;

internal enum NetLink { Wifi, Lan }

internal readonly record struct NetDay(DateOnly Day, NetLink Link, long Down, long Up);

internal sealed class NetLedger
{

    internal const int KeepDays = 90;

    private readonly Dictionary<(DateOnly, NetLink), (long Down, long Up)> _days = new();

        internal void Add(DateOnly day, NetLink link, long down, long up)
    {
        if (down < 0) down = 0;
        if (up < 0) up = 0;
        if (down == 0 && up == 0) return;
        _days.TryGetValue((day, link), out var cur);
        _days[(day, link)] = (cur.Down + down, cur.Up + up);
    }

    internal (long Down, long Up) Total(DateOnly from, DateOnly to, NetLink? link = null)
    {
        long down = 0, up = 0;
        foreach (var ((day, l), v) in _days)
        {
            if (day < from || day > to) continue;
            if (link is { } want && l != want) continue;
            down += v.Down; up += v.Up;
        }
        return (down, up);
    }

    internal (long Down, long Up) Today(DateOnly today, NetLink? link = null) => Total(today, today, link);

    internal (long Down, long Up) Week(DateOnly today, NetLink? link = null)
        => Total(today.AddDays(-6), today, link);

    internal (long Down, long Up) Month(DateOnly today, NetLink? link = null)
        => Total(today.AddDays(-29), today, link);

        internal IReadOnlyList<(DateOnly Day, long Down, long Up)> Series(DateOnly today, int days)
    {
        var list = new List<(DateOnly, long, long)>(days);
        for (int i = days - 1; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            var (down, up) = Total(day, day);
            list.Add((day, down, up));
        }

        return list;
    }

    internal void Trim(DateOnly today)
    {
        var cutoff = today.AddDays(-(KeepDays - 1));
        var stale = new List<(DateOnly, NetLink)>();
        foreach (var key in _days.Keys) if (key.Item1 < cutoff) stale.Add(key);
        foreach (var key in stale) _days.Remove(key);
    }

    internal IEnumerable<string> Save()
    {
        var keys = new List<(DateOnly, NetLink)>(_days.Keys);
        keys.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2));
        foreach (var key in keys)
        {
            var v = _days[key];
            yield return string.Create(CultureInfo.InvariantCulture,
                $"{key.Item1:yyyy-MM-dd}\t{key.Item2}\t{v.Down}\t{v.Up}");
        }
    }

        internal static NetLedger Load(IEnumerable<string> lines)
    {
        var led = new NetLedger();
        foreach (var raw in lines)
        {

            var parts = (raw ?? "").Split('\t');
            if (parts.Length < 4) continue;
            if (!DateOnly.TryParseExact(parts[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day)) continue;
            if (!Enum.TryParse<NetLink>(parts[1].Trim(), ignoreCase: true, out var link)) continue;
            if (!long.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var down)) continue;
            if (!long.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var up)) continue;
            led.Add(day, link, down, up);
        }
        return led;
    }
}
