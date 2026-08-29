using System;
using System.Drawing;

namespace Halo.Widgets;

internal static class HaloMood
{

    private const float StartHue = 200f;
    private const float Spread = 44f;

    private static readonly (float Hour, float Sat, float Val)[] Light =
    [
        (0f,  0.62f, 0.93f),
        (5f,  0.58f, 0.96f),
        (9f,  0.52f, 1.00f),
        (12f, 0.40f, 1.00f),
        (16f, 0.55f, 1.00f),
        (19f, 0.68f, 1.00f),
        (22f, 0.66f, 0.95f),
    ];

        internal static (Color Left, Color Right) At(DateTime when)
        => At((float)when.TimeOfDay.TotalHours);

    internal const float LowBattery = 0.25f;

    internal readonly record struct Conditions(
        float Battery = -1f, bool Charging = false, bool Mic = false, bool Cam = false, bool Offline = false,
        Doing Activity = Doing.Nothing, Color? Accent = null);

    internal enum Doing { Nothing, Music, Video, Downloading }

    internal static (Color Left, Color Right) At(float hour, Conditions c)
    {
        var (l, r) = At(hour);

        if (!c.Charging && c.Battery > 0f && c.Battery < LowBattery)
        {
            float worry = Smooth(Math.Clamp((LowBattery - c.Battery) / LowBattery, 0f, 1f));
            l = Toward(l, Warning, worry);
            r = Toward(r, WarningEdge, worry);
        }

        switch (c.Activity)
        {
            case Doing.Music:

                if (c.Accent is { } art) { l = Toward(l, art, 0.34f); r = Toward(r, Lift(art), 0.34f); }
                break;
            case Doing.Video:

                l = Toward(Dim(l, 0.78f), FilmLeft, 0.40f);
                r = Toward(Dim(r, 0.78f), FilmRight, 0.40f);
                break;
            case Doing.Downloading:
                l = Toward(l, PourLeft, 0.30f);
                r = Toward(r, PourRight, 0.30f);
                break;
        }

        if (c.Offline)
        {

            l = Drain(l, 0.72f);
            r = Drain(r, 0.72f);
        }

        if (c.Cam) { l = Toward(l, CamLeft, 0.74f); r = Toward(r, CamRight, 0.74f); }
        else if (c.Mic) { l = Toward(l, MicLeft, 0.70f); r = Toward(r, MicRight, 0.70f); }
        return (l, r);
    }

    private static readonly Color CamLeft = Color.FromArgb(255, 42, 214, 126);
    private static readonly Color CamRight = Color.FromArgb(255, 122, 232, 96);
    private static readonly Color MicLeft = Color.FromArgb(255, 255, 168, 44);
    private static readonly Color MicRight = Color.FromArgb(255, 255, 122, 60);

    private static readonly Color FilmLeft = Color.FromArgb(255, 74, 66, 158);
    private static readonly Color FilmRight = Color.FromArgb(255, 128, 70, 172);
    private static readonly Color PourLeft = Color.FromArgb(255, 96, 150, 236);
    private static readonly Color PourRight = Color.FromArgb(255, 120, 196, 244);

        private static Color Dim(Color c, float k)
        => Color.FromArgb(255, (int)(c.R * k), (int)(c.G * k), (int)(c.B * k));

    private static Color Lift(Color c)
        => Color.FromArgb(255, Math.Min(255, c.R + 52), Math.Min(255, c.G + 52), Math.Min(255, c.B + 52));

        private static Color Drain(Color c, float amount)
    {
        float grey = c.R * 0.30f + c.G * 0.59f + c.B * 0.11f;
        float k = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb(255,
            (int)MathF.Round(c.R + (grey - c.R) * k),
            (int)MathF.Round(c.G + (grey - c.G) * k),
            (int)MathF.Round(c.B + (grey - c.B) * k));
    }

    internal static (Color Left, Color Right) At(float hour, float battery, bool charging)
        => At(hour, new Conditions(battery, charging));

    private static readonly Color Warning = Color.FromArgb(255, 255, 156, 48);
    private static readonly Color WarningEdge = Color.FromArgb(255, 255, 88, 72);

    private static Color Toward(Color from, Color to, float t)
        => Color.FromArgb(255,
            (int)MathF.Round(from.R + (to.R - from.R) * t),
            (int)MathF.Round(from.G + (to.G - from.G) * t),
            (int)MathF.Round(from.B + (to.B - from.B) * t));

        internal static (Color Left, Color Right) At(float hour)
    {
        hour = ((hour % 24f) + 24f) % 24f;
        float hue = (StartHue + hour * 15f) % 360f;

        int i = Light.Length - 1;
        for (int k = 0; k < Light.Length; k++) if (Light[k].Hour <= hour) i = k;
        var a = Light[i];
        var b = Light[(i + 1) % Light.Length];

        float span = (b.Hour > a.Hour ? b.Hour : b.Hour + 24f) - a.Hour;
        float t = span <= 0f ? 0f : Smooth((hour - a.Hour) / span);

        float sat = a.Sat + (b.Sat - a.Sat) * t;
        float val = a.Val + (b.Val - a.Val) * t;
        return (Fx.HsvToRgb(hue, sat, val), Fx.HsvToRgb((hue + Spread) % 360f, sat, val));
    }

    internal static float LerpHue(float from, float to, float t)
    {
        float d = ((to - from + 540f) % 360f) - 180f;
        return ((from + d * t) % 360f + 360f) % 360f;
    }

    private static float Smooth(float t)
    {
        float k = Math.Clamp(t, 0f, 1f);
        return k * k * (3f - 2f * k);
    }
}
