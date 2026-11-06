using System;

namespace Halo.Shell;

internal static class Goodbye
{

    internal const int DurationMs = 720;

    internal const int DotPx = 6;

        internal static (int W, int H, int R, float Content, byte Alpha) At(int w, int h, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        h = Math.Max(DotPx, h);
        w = Math.Max(h, w);

        float content = 1f - Smooth((t - 0.04f) / 0.30f);
        int W, H;
        if (t < 0.40f)
        {

            float u = Smooth(t / 0.40f);
            W = (int)MathF.Round(w + (h - w) * u);
            H = h;
        }
        else
        {

            float u = (t - 0.40f) / 0.60f;
            u = u * u;
            int s = (int)MathF.Round(h + (DotPx - h) * u);
            W = H = Math.Max(DotPx, s);
        }
        float fade = Smooth((t - 0.62f) / 0.38f);
        byte alpha = (byte)Math.Clamp((int)MathF.Round(255f * (1f - fade)), 0, 255);
        return (W, H, Math.Min(W, H) / 2, content, alpha);
    }

        internal static float EyesShut(float t) => Smooth(t / 0.26f);

    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
