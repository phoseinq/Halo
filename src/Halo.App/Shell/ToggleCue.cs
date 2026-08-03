using System;

namespace Halo.Shell;

internal readonly struct ToggleCue
{

    internal const int RiseMs = 160;

    internal const int HoldMs = 5400;
    internal const int FallMs = 420;
    internal const int TotalMs = RiseMs + HoldMs + FallMs;

    private readonly long _firedAt;

    internal ToggleCue(long firedAt) => _firedAt = firedAt;

    internal float Alpha(long now)
    {
        if (_firedAt <= 0) return 0f;
        long dt = now - _firedAt;
        if (dt < 0 || dt >= TotalMs) return 0f;
        if (dt < RiseMs) return Smooth(dt / (float)RiseMs);
        if (dt < RiseMs + HoldMs) return 1f;
        return Smooth(1f - (dt - RiseMs - HoldMs) / (float)FallMs);
    }

    internal bool Alive(long now) => _firedAt > 0 && now - _firedAt < TotalMs;

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
