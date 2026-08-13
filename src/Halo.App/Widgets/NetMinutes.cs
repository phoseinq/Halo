using System;
using System.Collections.Generic;

namespace Halo.Widgets;

internal sealed class NetMinutes
{
    internal const int Keep = 62;

    private readonly Dictionary<(DateTime, NetLink), (long Down, long Up)> _minutes = new();

    internal static DateTime MinuteOf(DateTime at) => new(at.Year, at.Month, at.Day, at.Hour, at.Minute, 0);

    internal void Add(DateTime at, NetLink link, long down, long up)
    {
        if (down < 0) down = 0;
        if (up < 0) up = 0;
        if (down == 0 && up == 0) return;
        var key = (MinuteOf(at), link);
        _minutes.TryGetValue(key, out var cur);
        _minutes[key] = (cur.Down + down, cur.Up + up);
    }

    internal (long Down, long Up) Minute(DateTime minuteStart, NetLink? link = null)
    {
        long down = 0, up = 0;
        foreach (var ((minute, l), v) in _minutes)
        {
            if (minute != minuteStart) continue;
            if (link is { } want && l != want) continue;
            down += v.Down; up += v.Up;
        }
        return (down, up);
    }

        internal IReadOnlyList<(DateTime Minute, long Down, long Up)> Series(DateTime now, int minutes)
    {
        var list = new List<(DateTime, long, long)>(minutes);
        var newest = MinuteOf(now);
        for (int i = minutes - 1; i >= 0; i--)
        {
            var at = newest.AddMinutes(-i);
            var v = Minute(at);
            list.Add((at, v.Down, v.Up));
        }
        return list;
    }

    internal void Trim(DateTime now)
    {
        var cutoff = MinuteOf(now).AddMinutes(-Keep);
        foreach (var key in new List<(DateTime, NetLink)>(_minutes.Keys))
            if (key.Item1 < cutoff) _minutes.Remove(key);
    }
}
