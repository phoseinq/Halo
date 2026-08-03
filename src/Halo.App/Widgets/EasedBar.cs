using System;

namespace Halo.Widgets;

internal struct EasedBar
{

    private const float PerSecond = 1.25f;

    private float _shown;
    private bool _seeded;
    private long _at;

    internal readonly float Shown => _shown;

    internal float Step(float target)
    {
        long now = Environment.TickCount64;
        float dt = _at == 0 ? 0.008f : (now - _at) / 1000f;
        _at = now;
        return Step(target, dt);
    }

    internal float Step(float target, float dt)
    {
        target = Math.Clamp(target, 0f, 1f);
        if (!_seeded) { _seeded = true; return _shown = target; }

        float step = PerSecond * Math.Clamp(dt, 0f, 0.05f);
        float gap = target - _shown;

        if (Math.Abs(gap) <= step) _shown = target;
        else _shown += Math.Sign(gap) * step;
        return _shown;
    }

    internal void Reset() => _seeded = false;
}
