using System;

namespace Halo.Shell;

internal static class PressFeel
{
    internal const float Depth = 0.02f;
    internal const float InSeconds = 0.08f;
    internal const float OutSeconds = 0.18f;

        internal static float Step(float press, bool held, float dt)
    {
        float target = held ? 1f : 0f;
        float rate = dt / (held ? InSeconds : OutSeconds);
        return press < target ? Math.Min(target, press + rate) : Math.Max(target, press - rate);
    }

        internal static float Scale(float press)
    {
        float t = Math.Clamp(press, 0f, 1f);
        return 1f - Depth * (t * t * (3f - 2f * t));
    }
}
