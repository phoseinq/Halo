using System;

namespace Halo.Widgets;

internal static class FaceDirector
{

    internal const float FadeSeconds = 0.42f;

    internal const float BlinkSeconds = 0.16f;
    internal const float BlinkGapMin = 2.6f, BlinkGapMax = 6.4f;

    internal const int BlinkCycle = 13;

    internal static float Gap(int index)
    {
        float f = (index + 1) * 0.6180339887f;
        return BlinkGapMin + (f - MathF.Floor(f)) * (BlinkGapMax - BlinkGapMin);
    }

    internal static float CycleSeconds()
    {
        float total = 0f;
        for (int i = 0; i < BlinkCycle; i++) total += Gap(i);
        return total;
    }

    internal static float Open(float elapsed)
    {
        if (elapsed < 0f) return 1f;
        float cycle = CycleSeconds();

        double t = elapsed - Math.Floor(elapsed / (double)cycle) * cycle;
        double at = 0.0;
        for (int i = 0; i < BlinkCycle; i++)
        {
            at += Gap(i);
            double into = t - at;
            if (into < 0.0) break;
            if (into < BlinkSeconds)
            {
                const float half = BlinkSeconds / 2f;
                float k = (float)(into < half ? into / half : (BlinkSeconds - into) / half);

                return 0.06f + (1f - 0.06f) * (1f - Smooth(k));
            }
        }
        return 1f;
    }

    internal static (float X, float Y) Gaze(float elapsed)
        => (0.26f * MathF.Sin(elapsed / 3.1f), 0.17f * MathF.Sin(elapsed / 4.7f));

    internal static float Glow(float elapsed)
        => 1f + 0.09f * MathF.Sin(elapsed / 2.05f);

    internal static Face.Look At(float elapsed)
    {
        var (gx, gy) = Gaze(elapsed);
        return new Face.Look(Open(elapsed), gx, gy, Glow(elapsed));
    }

        internal static float Alpha(float t) => Smooth(Math.Clamp(t, 0f, 1f));

    internal const float NoticeEnd = 0.28f, CostumeEnd = 0.90f, HoldEnd = 1.22f, BeatEnd = 1.52f;
    internal const float BareEnd = 0.22f;

    internal const float MeltSeconds = 0.44f;

    internal static float HandSeconds(bool hasProp) => hasProp ? BeatEnd : BareEnd;

    internal const float MusicBeat = 2.80f;

    internal const float MusicSound = 0.95f;

    internal const float DownloadBeat = 2.60f;
    internal const float DownloadHit = 0.50f;

    internal const float DownloadPour = 0.78f, DownloadFull = 2.10f;

    internal const float DownloadBurp = 2.22f;

    internal const float VisorBeat = 2.60f, VisorHit = 0.46f;
    internal const float VisorWatch = 0.80f;

    internal const float AgentBeat = 2.70f;

    internal const float AgentWake = 0.35f, AgentWork = 2.05f;

    internal const float AgentCursorHz = 1.8f;
    internal const float ClampBeat = AgentBeat, ClampHit = 0.30f;
    internal const float SparkBeat = AgentBeat, SparkHit = 0.30f;
    internal const float PlugBeat = 2.20f, PlugHit = 0.52f;
    internal const float SproutBeat = 2.40f, SproutAt = 0.42f;
    internal const float SweepBeat = 1.90f;
    internal const float TrayBeat = 2.10f, TrayCatch = 0.50f;

        internal static float HandSeconds(FaceProp prop) => prop switch
    {
        FaceProp.None => BareEnd,
        FaceProp.Headphones => MusicBeat,
        FaceProp.Download => DownloadBeat,
        FaceProp.Brackets => ClampBeat,
        FaceProp.Goggles => VisorBeat,
        FaceProp.Spark => SparkBeat,
        FaceProp.Earbud => PlugBeat,
        FaceProp.Antenna => SproutBeat,
        FaceProp.Search => SweepBeat,
        FaceProp.Tray => TrayBeat,
        _ => BeatEnd,
    };

    internal readonly record struct Beat(
        Face.Look Look, float Prop, float Alpha, float Bob, float Squash,
        float Scale, float Sway, float Wave, float Phase, float Fill, float Slosh, float Bubble,
        float Spin, bool Code, float Film, float Letterbox)
    {

        internal Beat(Face.Look look, float prop, float alpha, float bob, float squash = 0f)
            : this(look, prop, alpha, bob, squash, 1f, 0f, 0f, 0f, -1f, 0f, 0f, -1f, false, -1f, 0f) { }

        internal Face.Liquid? Liquid => Fill < 0f ? null : new Face.Liquid(Fill, Slosh, Phase, Bubble);

        internal Face.Chase? Chase => Spin < 0f ? null : new Face.Chase(
            Spin, Code ? 0.15f : 0.26f, Code ? 2 : 1,

            Code ? System.Drawing.Color.FromArgb(140, 226, 255)
                 : System.Drawing.Color.FromArgb(240, 156, 108));
    }

    internal static float Hit(float since, float decay, float note)
        => since < 0f ? 0f : MathF.Exp(-decay * since) * MathF.Cos(note * since);

    internal const float MusicLand = 0.55f;

    internal static Beat Hand(float t, FaceProp prop, float age, float level)
    {
        float end = HandSeconds(prop);
        var (look, on, alpha, bob) = Hand(t, prop != FaceProp.None, age, end);
        switch (prop)
        {
            case FaceProp.Download: return Filling(t, end, look, alpha, bob, level);
            case FaceProp.Brackets: return Clamped(t, end, look, alpha, bob, age);
            case FaceProp.Goggles: return Watching(t, end, look, alpha, bob, age);
            case FaceProp.Spark: return Sparked(t, end, look, alpha, bob, age);
            case FaceProp.Earbud: return Plugged(t, end, look, alpha, bob);
            case FaceProp.Antenna: return Sprouted(t, end, look, alpha, bob);
            case FaceProp.Search: return Swept(t, end, look, alpha, bob);
            case FaceProp.Tray: return Caught(t, end, look, alpha, bob, age);
            case FaceProp.Headphones: break;
            default: return new Beat(look, on, alpha, bob);
        }

        return Playing(t, end, look, alpha, bob, level, age);
    }

    private static Beat Playing(float t, float end, Face.Look look, float alpha, float bob, float level,
                                float age)
    {
        float k = Math.Clamp(t, 0f, end);
        float lvl = Math.Clamp(level, 0f, 1f);

        float fall = Math.Clamp((k - 0.16f) / (MusicLand - 0.16f), 0f, 1f);
        float on = fall * fall;

        float since = k - MusicLand;

        if (since < 0f) bob -= Smooth(Math.Clamp((k - 0.44f) / (MusicLand - 0.44f), 0f, 1f)) * 0.022f;

        float hit = since < 0f ? 0f : MathF.Exp(-6f * since) * MathF.Cos(10.5f * since);

        float settled = Smooth(Math.Clamp(since / 0.34f, 0f, 1f)) * -0.030f;

        float squash = MathF.Max(hit * 0.14f, -0.07f - settled) + settled;

        float drawIn = Smooth(Math.Clamp(since / (MusicSound - MusicLand), 0f, 1f));

        float playing = Smooth(Math.Clamp((k - MusicSound) / 0.30f, 0f, 1f));

        float scale = 1f - 0.07f * drawIn + 0.07f * playing * lvl;

        float sway = playing * 0.020f * MathF.Sin((k - MusicSound) / 0.68f * MathF.Tau);

        bob += playing * lvl * 0.045f;

        float shut = since < 0f ? 0f : MathF.Exp(-5f * since) + 0.55f * drawIn * (1f - playing);
        float open = look.Open * (1f - 0.80f * Math.Clamp(shut, 0f, 1f));

        float grooving = Open(age) * (0.66f - 0.26f * lvl);
        open = open * (1f - playing) + grooving * playing;

        look = look with
        {
            Open = open,

            Glow = look.Glow * (1f - 0.18f * drawIn * (1f - playing)) * (1f + 0.22f * playing * lvl),
        };

        return new Beat(look, on, alpha, bob, squash, scale, sway,
                        playing * lvl, (k - MusicSound) / MusicWaveSeconds, -1f, 0f, 0f, -1f, false, -1f, 0f);
    }

    internal const float MusicWaveSeconds = 0.72f;

    internal static (Face.Look Look, float Prop, float Alpha, float Bob) Hand(float t, bool hasProp, float age)
        => Hand(t, hasProp, age, HandSeconds(hasProp));

    private static (Face.Look Look, float Prop, float Alpha, float Bob) Hand(
        float t, bool hasProp, float age, float end)
    {
        float k = Math.Clamp(t, 0f, end);
        var idle = At(age);

        float fadeFrom = hasProp ? end - (BeatEnd - HoldEnd) : BareEnd * 0.3f;
        float fade = 1f - Math.Clamp((k - fadeFrom) / Math.Max(0.01f, end - fadeFrom), 0f, 1f);
        if (!hasProp) return (idle, 0f, Smooth(fade), 0f);

        float gazeT = Math.Clamp(k / CostumeEnd, 0f, 1f);
        var (gaze, gazeUp) = Searching(gazeT);

        float slide = Math.Clamp((k - NoticeEnd) / (CostumeEnd - NoticeEnd), 0f, 1f);
        float prop = Back(slide);

        float since = k - CostumeEnd;
        float bob = since < 0f ? 0f
            : MathF.Exp(-8f * since) * MathF.Sin(15f * since) * 0.048f;

        float beat = Math.Clamp((k - CostumeEnd) / (HoldEnd - CostumeEnd), 0f, 1f);
        float open = idle.Open * (1f - 0.55f * Smooth(beat));

        float land = MathF.Exp(-9f * Math.Max(0f, since)) * (since < 0f ? 0f : 1f);
        open *= 1f + 0.30f * land;

        return (new Face.Look(open, gaze, idle.GazeY + gazeUp,
                              idle.Glow + 0.25f * Smooth(Math.Clamp(prop, 0f, 1f))),
                prop, Smooth(fade), bob);
    }

    private static Beat Filling(float t, float end, Face.Look look, float alpha, float bob, float level)
    {
        float k = Math.Clamp(t, 0f, end);
        float fall = Math.Clamp((k - 0.14f) / (DownloadHit - 0.14f), 0f, 1f);
        float on = fall * fall;

        float since = k - DownloadHit;

        float hit = since < 0f ? 0f : MathF.Exp(-5.5f * since) * MathF.Cos(9.5f * since);
        float squash = MathF.Max(hit * 0.14f, -0.07f);

        if (since >= 0f) bob += MathF.Exp(-5f * since) * MathF.Sin(9f * since) * 0.055f;

        bool known = level >= 0f;
        float target = known ? Math.Clamp(level, 0f, 1f) : 0.22f;
        float pour = Smooth(Math.Clamp((k - DownloadPour) / (DownloadFull - DownloadPour), 0f, 1f));
        float overshoot = k > DownloadPour
            ? MathF.Exp(-2.6f * (k - DownloadPour)) * MathF.Sin(6.2f * (k - DownloadPour)) * 0.16f : 0f;
        float fill = Math.Clamp(target * pour + overshoot * pour, 0f, 1f);

        float agitation = k < DownloadPour ? 0f
            : MathF.Exp(-1.9f * (k - DownloadPour)) * 0.85f + (known ? 0.05f : 0.34f);

        float shut = since < 0f ? 0f : MathF.Exp(-4.5f * since);
        float open = look.Open * (1f - 0.85f * shut);

        float afloat = Smooth(Math.Clamp((fill - 0.47f) / 0.22f, 0f, 1f));
        look = look with
        {
            Open = open * (1f + 0.34f * afloat),
            GazeY = look.GazeY - 0.42f * afloat,
        };

        float burp = k - DownloadBurp;
        if (burp >= 0f)
        {
            squash += MathF.Exp(-7f * burp) * MathF.Cos(13f * burp) * 0.10f;
            look = look with { Open = look.Open * (1f - 0.70f * MathF.Exp(-6f * burp)) };
            agitation += MathF.Exp(-4f * burp) * 0.7f;
        }

        float bubble = Math.Clamp((k - DownloadBurp + 0.16f) / 0.34f, 0f, 1f);

        return new Beat(look, on, alpha, bob, squash, 1f, 0f, 0f, k * 0.62f,
                        k < DownloadHit ? -1f : fill,
                        Math.Clamp(agitation, 0f, 1.6f), burp >= -0.16f ? bubble : 0f, -1f, false, -1f, 0f);
    }

    internal const float AgentIn = 0.34f;

    internal const float AgentSpin = 1.15f;

        private static (Face.Look Look, float Phase, float Sway) Working(float k, Face.Look look, float age)
    {
        float on = Smooth(Math.Clamp((k - AgentWake) / 0.22f, 0f, 1f)) *
                   (1f - Smooth(Math.Clamp((k - AgentWork) / 0.20f, 0f, 1f)));
        if (on <= 0.001f) return (look, -1f, 0f);

        float since = MathF.Max(0f, k - AgentIn);
        float phase = AgentSpin * since * (1f + 0.35f * since);

        float sway = on * 0.020f * MathF.Sin(k / 0.62f * MathF.Tau);

        return (look with
        {

            Open = look.Open * (1f - on) + Open(age) * 0.72f * on,

            Glow = look.Glow * (1f - 0.16f * on),
        }, phase, sway);
    }

        private static float Finishing(float k) => Smooth(Math.Clamp((k - AgentWork) / 0.22f, 0f, 1f));

    private static Beat Clamped(
        float t, float end, Face.Look look, float alpha, float bob, float age)
    {
        float k = Math.Clamp(t, 0f, end);
        float on = Smooth(Math.Clamp((k - 0.10f) / (ClampHit - 0.10f), 0f, 1f));
        var (working, phase, sway) = Working(k, look, age);
        look = working;

        float since = k - AgentWork;

        float squash = since < 0f ? 0f : MathF.Max(Hit(since, 6.5f, 12f) * -0.07f, -0.07f);
        float done = Finishing(k);

        look = look with
        {
            Open = look.Open * (1f - done) + Open(age) * done,
            Glow = look.Glow * (1f + 0.55f * (since < 0f ? 0f : MathF.Exp(-5f * since))),
        };
        return new Beat(look, on, alpha, bob, squash, 1f, sway, 0f, 0f, -1f, 0f, 0f, phase, true, -1f, 0f);
    }

    internal const float FilmClose = 0.42f;
    internal const float FilmOpen = 2.22f;

    private static Beat Watching(
        float t, float end, Face.Look look, float alpha, float bob, float age)
    {
        float k = Math.Clamp(t, 0f, end);

        float bars = Smooth(Math.Clamp((k - 0.10f) / (FilmClose - 0.10f), 0f, 1f)) *
                     (1f - Smooth(Math.Clamp((k - FilmOpen) / (end - FilmOpen), 0f, 1f)));

        float watching = Smooth(Math.Clamp((k - FilmClose) / 0.34f, 0f, 1f));
        bob += watching * 0.026f;

        float drift = watching * (1f - Smooth(Math.Clamp((k - FilmOpen) / 0.30f, 0f, 1f)));

        int cut = (int)MathF.Floor(MathF.Max(0f, k - FilmClose) * 3.1f);
        float f = (cut + 1) * 0.6180339887f;
        float bright = f - MathF.Floor(f);
        look = look with
        {
            GazeX = look.GazeX * (1f - drift * 0.85f) + drift * 0.58f * MathF.Sin(k / 0.79f),
            GazeY = look.GazeY * (1f - drift * 0.85f) + drift * 0.20f * MathF.Sin(k / 1.31f),

            Open = look.Open * (1f - 0.26f * drift) * (1f + 0.30f * drift * (bright - 0.5f)),

            Glow = look.Glow * (1f - 0.30f * drift),
        };

        return new Beat(look, 0f, alpha, bob, 0f, 1f, 0f, 0f, 0f, -1f, 0f, 0f, -1f, false,
                        drift > 0.02f ? k - FilmClose : -1f, bars);
    }

    private static Beat Sparked(
        float t, float end, Face.Look look, float alpha, float bob, float age)
    {
        float k = Math.Clamp(t, 0f, end);
        float on = Back(Math.Clamp((k - 0.10f) / (SparkHit - 0.10f), 0f, 1f));
        var (working, phase, sway) = Working(k, look, age);
        look = working;

        float since = k - AgentWork;

        float flash = since < 0f ? 0f : MathF.Exp(-4.5f * since);
        float done = Finishing(k);
        look = look with
        {
            Glow = look.Glow * (1f + 1.15f * flash),
            Open = (look.Open * (1f - done) + Open(age) * done) * (1f + 0.34f * flash),
        };
        return new Beat(look, on, alpha, bob, 0f, 1f, sway, 0f, 0f, -1f, 0f, 0f, phase, false, -1f, 0f);
    }

    private static Beat Plugged(
        float t, float end, Face.Look look, float alpha, float bob)
    {
        float k = Math.Clamp(t, 0f, end);
        float on = Math.Clamp(k / end, 0f, 1f);

        float sweep = Smooth(Math.Clamp((k - 0.10f) / 0.55f, 0f, 1f));
        float tracking = Smooth(Math.Clamp((k - 0.10f) / 0.16f, 0f, 1f)) *
                         (1f - Smooth(Math.Clamp((k - 0.62f) / 0.16f, 0f, 1f)));
        look = look with
        {
            GazeX = look.GazeX * (1f - tracking) + tracking * MathF.Cos(-0.9f + 3.4f * sweep),
            GazeY = look.GazeY * (1f - tracking) + tracking * 0.5f * MathF.Sin(-0.9f + 3.4f * sweep),
        };

        float merged = k - 0.65f;
        float squash = merged < 0f ? 0f : Hit(merged, 14f, 30f) * 0.045f;

        float reach = Smooth(Math.Clamp((k - 0.34f) / 0.31f, 0f, 1f));
        float scale = 1f + 0.055f * reach
                    - (merged < 0f ? 0f : 0.055f * Smooth(Math.Clamp(merged / 0.45f, 0f, 1f)));
        if (merged >= 0f)
        {
            float flash = MathF.Exp(-4.2f * merged);
            look = look with { Glow = look.Glow * (1f + 0.85f * flash) };
            if (merged < 0.18f)
                look = look with { Open = look.Open * (1f - 0.70f * MathF.Sin(merged / 0.18f * MathF.PI)) };
        }
        return new Beat(look, on, alpha, bob, squash, scale, 0f, 0f, 0f, -1f, 0f, 0f, -1f, false, -1f, 0f);
    }

    private static Beat Sprouted(
        float t, float end, Face.Look look, float alpha, float bob)
    {
        float k = Math.Clamp(t, 0f, end);

        float on = Math.Clamp(k / end, 0f, 1f);

        float land = 0f, filled = 0f;
        for (int i = 0; i < 6; i++)
        {
            float at = 0.085f * i + 0.30f;
            float since = k - at;
            if (since <= 0f) continue;
            filled += 1f / 6f;
            if (since < 0.34f) land += MathF.Exp(-8f * since) * MathF.Sin(22f * since);
        }
        land = Math.Clamp(land, -1.2f, 1.2f);
        filled = Smooth(Math.Clamp(filled, 0f, 1f));

        bob += land * 0.012f;
        return new Beat(look with
        {

            Glow = look.Glow * (1f + 0.34f * filled),
            Open = look.Open * (1f + 0.14f * filled),
        }, on, alpha, bob, land * 0.055f, 1f + 0.05f * filled, 0f, 0f, 0f, -1f, 0f, 0f, -1f, false, -1f, 0f);
    }

    private static Beat Caught(float t, float end, Face.Look look, float alpha, float bob, float age)
    {
        float k = Math.Clamp(t, 0f, end);
        float fall = Math.Clamp((k - 0.12f) / (TrayCatch - 0.12f), 0f, 1f);
        float on = fall * fall;

        float since = k - TrayCatch;

        float squash = MathF.Max(Hit(since, 7f, 11f) * 0.10f, -0.06f);
        if (since >= 0f) bob += MathF.Exp(-6f * since) * MathF.Sin(10f * since) * 0.040f;

        float coming = since < 0f ? Smooth(Math.Clamp((k - 0.12f) / (TrayCatch - 0.12f), 0f, 1f)) : 0f;

        float look_at = Smooth(Math.Clamp((since - 0.22f) / 0.36f, 0f, 1f));
        float sway = look_at * 0.022f * MathF.Sin((since - 0.22f) * 3.6f);
        look = look with
        {
            GazeY = look.GazeY - 0.55f * coming,
            GazeX = look.GazeX * (1f - look_at) + look_at * 0.5f * MathF.Sin((since - 0.22f) * 3.6f),
            Open = look.Open * (1f + 0.28f * coming) * (1f - 0.30f * (since < 0f ? 0f : MathF.Exp(-6f * since))),
        };
        return new Beat(look, on, alpha, bob, squash, 1f, sway, 0f, 0f, -1f, 0f, 0f, -1f, false, -1f, 0f);
    }

    private static Beat Swept(
        float t, float end, Face.Look look, float alpha, float bob)
    {
        float k = Math.Clamp(t, 0f, end);
        float sweep = Smooth(Math.Clamp((k - 0.20f) / 0.95f, 0f, 1f));

        float lens = 1f - 2f * sweep;
        float locked = Smooth(Math.Clamp((k - 0.20f) / 0.18f, 0f, 1f)) *
                       (1f - Smooth(Math.Clamp((k - 1.15f) / 0.25f, 0f, 1f)));
        look = look with
        {
            GazeX = look.GazeX * (1f - locked) + lens * locked,

            Open = look.Open * (1f + 0.30f * locked * (1f - MathF.Abs(lens))),
        };
        return new Beat(look, sweep, alpha, bob);
    }

    internal static readonly (float T, float X, float Y)[] Glances =
    [
        (0.00f,  0.00f,  0.00f),
        (0.18f, -0.70f, -0.28f),
        (0.34f, -0.62f, -0.22f),
        (0.52f,  0.55f,  0.30f),
        (0.66f,  0.50f,  0.26f),
        (0.86f,  0.90f, -0.10f),
        (1.00f,  0.62f,  0.00f),
    ];

    internal static (float X, float Y) Searching(float t)
    {
        float k = Math.Clamp(t, 0f, 1f);
        for (int i = 1; i < Glances.Length; i++)
        {
            if (k > Glances[i].T) continue;
            var (t0, x0, y0) = Glances[i - 1];
            var (t1, x1, y1) = Glances[i];
            float span = MathF.Max(0.0001f, t1 - t0);
            float f = Smooth((k - t0) / span);
            return (x0 + (x1 - x0) * f, y0 + (y1 - y0) * f);
        }
        return (Glances[^1].X, Glances[^1].Y);
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Back(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float k = Math.Clamp(t, 0f, 1f) - 1f;
        return 1f + c3 * k * k * k + c1 * k * k;
    }
}
