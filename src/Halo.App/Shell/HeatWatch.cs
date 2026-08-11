using System;
using System.Collections.Generic;

namespace Halo.Shell;

internal sealed class HeatWatch
{
    internal const int RiseC = 4;
    internal const int WindowMinutes = 90;
    internal const int FloorC = 25;
    internal const int CooldownMinutes = 6 * 60;

    private readonly List<(DateTime At, int TempC)> _seen = new();
    private DateTime _fired = DateTime.MinValue;

        internal int? Observe(int tempC, DateTime now)
    {

        _seen.RemoveAll(s => (now - s.At).TotalMinutes > WindowMinutes || s.At > now);
        int? rise = null;
        if (tempC >= FloorC && (now - _fired).TotalMinutes >= CooldownMinutes)
        {
            int coldest = int.MaxValue;
            foreach (var s in _seen) if (s.TempC < coldest) coldest = s.TempC;

            if (coldest != int.MaxValue && tempC - coldest >= RiseC)
            {
                rise = tempC - coldest;
                _fired = now;

                _seen.Clear();
            }
        }
        _seen.Add((now, tempC));
        return rise;
    }
}
