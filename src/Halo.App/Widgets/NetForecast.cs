using System;
using System.Collections.Generic;

namespace Halo.Widgets;

internal static class NetForecast
{

    internal const int MinDays = 7;

    internal const int LearnDays = 30;

    private const double Sigmas = 2.5;

    private const long FloorBytes = 512L * 1024 * 1024;

    internal readonly record struct Usage(long Typical, long Cap, int Days)
    {
                internal bool IsHeavy(long dayBytes) => dayBytes > Cap;
    }

        internal readonly record struct Progress(int Have, int Need);

    internal static Progress? Learning(int measuredDays) =>
        measuredDays <= 0 || measuredDays >= MinDays ? null : new Progress(measuredDays, MinDays);

        internal static Usage? Learn(IReadOnlyList<long> dailyTotals)
    {
        if (dailyTotals is null || dailyTotals.Count < MinDays) return null;
        long median = Median(dailyTotals);

        var spread = new long[dailyTotals.Count];
        for (int i = 0; i < dailyTotals.Count; i++) spread[i] = Math.Abs(dailyTotals[i] - median);
        long mad = Median(spread);

        long cap = (long)Math.Round(median + Sigmas * mad);

        long minimumRoom = median / 10;
        if (cap - median < minimumRoom) cap = median + minimumRoom;
        return new Usage(median, Math.Max(cap, FloorBytes), dailyTotals.Count);
    }

    private static long Median(IReadOnlyList<long> values)
    {
        var sorted = new long[values.Count];
        for (int i = 0; i < values.Count; i++) sorted[i] = values[i];
        Array.Sort(sorted);
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
