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

    internal (long Down, long Up) Total(DateTime now, int minutes, NetLink? link = null)
    {
        var newest = MinuteOf(now);
        var oldest = newest.AddMinutes(-(minutes - 1));
        long down = 0, up = 0;
        foreach (var ((minute, l), v) in _minutes)
        {
            if (minute < oldest || minute > newest) continue;
            if (link is { } want && l != want) continue;
            down += v.Down; up += v.Up;
        }
        return (down, up);
    }

    internal IReadOnlyList<(DateTime Minute, long Down, long Up)> Series(DateTime now, int minutes)
    {
        var newest = MinuteOf(now);
        var down = new long[minutes];
        var up = new long[minutes];
        foreach (var ((minute, _), v) in _minutes)
        {
            int back = (int)Math.Round((newest - minute).TotalMinutes);
            if (back < 0 || back >= minutes) continue;
            down[minutes - 1 - back] += v.Down;
            up[minutes - 1 - back] += v.Up;
        }
        var list = new List<(DateTime, long, long)>(minutes);
        for (int i = 0; i < minutes; i++)
            list.Add((newest.AddMinutes(-(minutes - 1 - i)), down[i], up[i]));
        return list;
    }

    internal void Trim(DateTime now)
    {
        var cutoff = MinuteOf(now).AddMinutes(-Keep);
        foreach (var key in new List<(DateTime, NetLink)>(_minutes.Keys))
            if (key.Item1 < cutoff) _minutes.Remove(key);
    }
}
