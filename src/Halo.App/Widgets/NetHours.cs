using System;
using System.Collections.Generic;
using System.Globalization;

namespace Halo.Widgets;

internal sealed class NetHours
{
    internal const int Keep = 26;

    private readonly Dictionary<(DateTime Hour, NetLink Link), (long Down, long Up)> _hours = [];

        internal static DateTime HourOf(DateTime at) => new(at.Year, at.Month, at.Day, at.Hour, 0, 0);

    internal void Add(DateTime at, NetLink link, long down, long up)
    {
        if (down <= 0 && up <= 0) return;
        var key = (HourOf(at), link);
        _hours.TryGetValue(key, out var had);
        _hours[key] = (had.Down + down, had.Up + up);
    }

    internal (long Down, long Up) Hour(DateTime hourStart, NetLink? link = null)
    {
        long down = 0, up = 0;
        var want = HourOf(hourStart);
        foreach (var (key, value) in _hours)
        {
            if (key.Hour != want) continue;
            if (link is { } only && key.Link != only) continue;
            down += value.Down; up += value.Up;
        }
        return (down, up);
    }

        internal IReadOnlyList<(DateTime Hour, long Down, long Up)> Series(DateTime now, int hours)
    {
        var list = new List<(DateTime, long, long)>(Math.Max(0, hours));
        var last = HourOf(now);
        for (int i = hours - 1; i >= 0; i--)
        {
            var h = last.AddHours(-i);
            var v = Hour(h);
            list.Add((h, v.Down, v.Up));
        }
        return list;
    }

    internal void Trim(DateTime now)
    {
        var cutoff = HourOf(now).AddHours(-(Keep - 1));
        var stale = new List<(DateTime, NetLink)>();
        foreach (var key in _hours.Keys) if (key.Hour < cutoff) stale.Add(key);
        foreach (var key in stale) _hours.Remove(key);
    }

    internal IEnumerable<string> Save()
    {
        foreach (var (key, value) in _hours)
            yield return string.Join('\t',
                key.Hour.ToString("o", CultureInfo.InvariantCulture),
                key.Link == NetLink.Lan ? "lan" : "wifi",
                value.Down.ToString(CultureInfo.InvariantCulture),
                value.Up.ToString(CultureInfo.InvariantCulture));
    }

    internal static NetHours Load(IEnumerable<string> lines)
    {
        var hours = new NetHours();
        foreach (var line in lines)
        {
            try
            {
                var parts = line.Split('\t');
                if (parts.Length < 4) continue;
                if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var hour)) continue;
                if (!long.TryParse(parts[2], CultureInfo.InvariantCulture, out long down)) continue;
                if (!long.TryParse(parts[3], CultureInfo.InvariantCulture, out long up)) continue;
                var link = parts[1] == "lan" ? NetLink.Lan : NetLink.Wifi;
                hours._hours[(HourOf(hour), link)] = (down, up);
            }
            catch { }
        }
        return hours;
    }
}
