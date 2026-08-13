using System;

namespace Halo.Widgets;

internal struct Drift
{
    private float _level;
    private long _at;

    internal readonly float Level => _level;

    internal float Step(float target, float seconds) => Step(target, seconds, Environment.TickCount64);

    internal float Step(float target, float seconds, long nowMs)
    {
        if (nowMs > _at)
        {

            long dt = _at == 0 ? 16 : Math.Clamp(nowMs - _at, 1, 250);
            _at = nowMs;
            _level += (target - _level) * (1f - MathF.Exp(-dt / (seconds * 1000f)));
        }
        return _level;
    }

        internal void Seed(float level, long nowMs) { _level = level; _at = nowMs; }
}
