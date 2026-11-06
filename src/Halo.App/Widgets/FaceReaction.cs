using System;

namespace Halo.Widgets;

internal readonly struct FaceReaction
{
    internal enum Kind { None, Giggle, Surprise }

    internal const int GiggleMs = 760;
    internal const int SurpriseMs = 560;

    internal readonly Kind What;
    private readonly long _firedAt;

    internal FaceReaction(Kind what, long firedAt) { What = what; _firedAt = firedAt; }

    internal int Duration => What switch { Kind.Giggle => GiggleMs, Kind.Surprise => SurpriseMs, _ => 0 };

    private float? T(long now)
    {
        if (What == Kind.None || _firedAt <= 0) return null;
        long dt = now - _firedAt;
        if (dt < 0 || dt >= Duration) return null;
        return dt / (float)Duration;
    }

    internal bool Alive(long now) => T(now) is not null;

        internal Face.Look Apply(Face.Look look, long now)
    {
        if (T(now) is not { } t) return look;
        if (What == Kind.Surprise)
        {

            float round = t < 0.14f ? Smooth(t / 0.14f) : t < 0.52f ? 1f : Smooth(1f - (t - 0.52f) / 0.48f);
            return look with
            {
                Round = Math.Max(look.Round, round),
                Open = look.Open * (1f + 0.18f * round),
                Glow = look.Glow * (1f + 0.25f * round),
            };
        }

        float squeeze = Bump(t, 0.06f, 0.40f) * 1f + Bump(t, 0.44f, 0.82f) * 0.7f;
        float flare = 1f - Smooth(t);
        return look with
        {
            Open = look.Open * (1f - 0.72f * squeeze),
            GazeY = look.GazeY * (1f - squeeze) + 0.35f * squeeze,
            Glow = look.Glow * (1f + 0.6f * flare),
        };
    }

    internal (float Bob, float Squash) Body(long now)
    {
        if (What != Kind.Giggle || T(now) is not { } t) return (0f, 0f);
        float damp = 1f - t;
        float squash = 0.14f * MathF.Sin(t * MathF.Tau * 2.5f) * damp * damp;
        return (0f, squash);
    }

    private static float Bump(float t, float a, float b)
    {
        if (t <= a || t >= b) return 0f;
        float u = (t - a) / (b - a);
        return MathF.Sin(u * MathF.PI);
    }

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
