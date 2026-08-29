using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Halo.Widgets;

internal static class Face
{

    private const float RefX = 570f, RefY = 351f, RefW = 396f, RefH = 319f;
    internal const float Aspect = RefW / RefH;

    private static readonly Color EyeTop = Color.FromArgb(255, 120, 184, 255);
    private static readonly Color EyeMid = Color.FromArgb(255, 155, 159, 255);
    private static readonly Color EyeBottom = Color.FromArgb(255, 210, 157, 245);

    internal static readonly Color HaloLeft = Color.FromArgb(255, 126, 156, 255);
    internal static readonly Color HaloRight = Color.FromArgb(255, 210, 155, 255);

    internal static (Color Left, Color Right)? RingTone;

    internal readonly record struct Look(float Open, float GazeX, float GazeY, float Glow, float Round = 0f)
    {
        internal static Look Awake => new(1f, 0f, 0f, 1f);
    }

    internal const int PlateW = 132, PlateH = 65;
    internal const float FloatTop = 10f;

    internal static RectangleF Squashed(RectangleF box, float squash)
    {
        float s = Math.Clamp(squash, -0.5f, 0.5f);
        if (MathF.Abs(s) < 0.0005f) return box;
        float w = box.Width * (1f + s), h = box.Height * (1f - s);
        return new RectangleF(box.X - (w - box.Width) / 2f, box.Bottom - h, w, h);
    }

    internal static RectangleF Scaled(RectangleF box, float k)
    {
        float s = Math.Clamp(k, 0.5f, 1f);
        if (MathF.Abs(s - 1f) < 0.0005f) return box;
        float w = box.Width * s, h = box.Height * s;
        return new RectangleF(box.X + (box.Width - w) / 2f, box.Bottom - h, w, h);
    }

        internal static RectangleF Swayed(RectangleF box, float sway)
    {
        box.Offset(sway * box.Width, 0f);
        return box;
    }

    internal static RectangleF BeatBox(float w, float h, float bob, float sway, float scale, float squash)
    {
        var box = PlateBox(w, h);
        box.Offset(sway * w, bob * h);
        return Squashed(Scaled(box, scale), squash);
    }

        internal static RectangleF PlateRect(float w, float h) => SheetRect(w, h);

    internal static RectangleF SheetRect(float w, float h) => new(0.5f, 0.5f, w - 1f, h - 1f);

    internal static RectangleF PlateBox(float w, float h)
    {
        float hh = h * 0.70f;
        float hw = hh * Aspect;
        return new RectangleF((w - hw) / 2f, (h - hh) / 2f, hw, hh);
    }

    internal static void DrawPlate(Graphics g, float w, float h, Look look, float alpha = 1f)
    {
        DrawGlass(g, w, h, alpha);

        Draw(g, PlateBox(w, h), look, Math.Clamp(alpha, 0f, 1f));
    }

        internal static void DrawGlass(Graphics g, float w, float h, float alpha = 1f)
        => Glass(g, PlateRect(w, h), h / 2f, alpha);

    internal static GraphicsPath SheetPath(RectangleF r, float radius) => RoundRect(r, radius);

        internal static void Glass(Graphics g, RectangleF plate, float radius, float alpha = 1f)
    {
        float a = Math.Clamp(alpha, 0f, 1f);
        if (a <= 0f || plate.Width < 4f || plate.Height < 4f) return;
        var keep = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var capsule = RoundRect(plate, radius))
        {

            using (var film = new LinearGradientBrush(
                new RectangleF(plate.X, plate.Y - 0.5f, plate.Width, plate.Height + 1f),
                Color.FromArgb((int)(22 * a), 186, 196, 255),
                Color.FromArgb((int)(18 * a), 198, 158, 245), LinearGradientMode.Vertical))
            {
                film.InterpolationColors = new ColorBlend
                {
                    Colors =
                    [
                        Color.FromArgb((int)(22 * a), 186, 196, 255),
                        Color.FromArgb((int)(11 * a), 160, 160, 240),
                        Color.FromArgb((int)(18 * a), 198, 158, 245),
                    ],
                    Positions = [0f, 0.55f, 1f],
                };
                g.FillPath(film, capsule);
            }

            using (var edge = new LinearGradientBrush(
                new RectangleF(plate.X - 0.5f, plate.Y - 0.5f, plate.Width + 1f, plate.Height + 1f),
                Color.FromArgb((int)(104 * a), 176, 190, 255),
                Color.FromArgb((int)(44 * a), 186, 150, 232), LinearGradientMode.ForwardDiagonal))
            using (var rim = new Pen(edge, 1f))
                g.DrawPath(rim, capsule);
        }
        g.SmoothingMode = keep;
    }

    internal static void DrawProp(Graphics g, RectangleF box, FaceProp prop, float on, float alpha = 1f,
                                  Image? icon = null, float beat = 0f)
    {
        float a = Math.Clamp(alpha, 0f, 1f) * Math.Clamp(on, 0f, 1f);
        if (a <= 0.01f || prop == FaceProp.None || box.Height < 4f) return;
        var keep = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float t = Math.Clamp(on, 0f, 1f);
        switch (prop)
        {
            case FaceProp.Headphones: Headphones(g, box, t, a); break;
            case FaceProp.Download: Download(g, box, t, a, beat); break;
            case FaceProp.Antenna: Packets(g, box, beat, a); break;
            case FaceProp.Tray: Filing(g, box, beat, a); break;
            case FaceProp.Earbud: Pairing(g, box, beat, a); break;
            case FaceProp.Search: Search(g, box, t, a); break;
            case FaceProp.Brackets: Brackets(g, box, t, a); break;
            case FaceProp.Spark: Spark(g, box, t, a); break;
            case FaceProp.Think: Think(g, box, t, a); break;
            case FaceProp.AppIcon: Devour(g, box, beat, a, icon); break;
        }
        g.SmoothingMode = keep;
    }

    private static readonly Color InkNet = Color.FromArgb(132, 231, 196);
    private static readonly Color InkTray = Color.FromArgb(226, 176, 104);
    private static readonly Color InkBt = Color.FromArgb(96, 168, 236);
    private static readonly Color InkClaude = Color.FromArgb(232, 146, 106);
    private static readonly Color InkCodex = Color.FromArgb(212, 216, 232);
    private static readonly Color InkThink = Color.FromArgb(150, 158, 232);
    private static readonly Color InkSearch = Color.FromArgb(196, 206, 255);

    private static void Packets(Graphics g, RectangleF box, float beat, float a)
    {
        var keep = g.SmoothingMode;
        var clip = g.Clip;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var head = HeadPath(box))
            g.SetClip(head, CombineMode.Exclude);

        float cx = box.X + box.Width / 2f, cy = box.Y + box.Height * 0.48f;
        const int count = 7;
        for (int i = 0; i < count; i++)
        {

            float f = (i + 1) * 0.6180339887f;
            f -= MathF.Floor(f);
            float start = i * 0.085f;
            float k = (beat - start) / 0.42f;
            if (k <= 0f || k >= 1f) continue;

            float ang = (f * 2f - 1f) * 2.5f;
            float far = box.Width * (0.86f + 0.16f * f);
            float near = box.Width * 0.30f;
            float r = far + (near - far) * Smooth01(k);
            float x = cx + MathF.Cos(ang) * r, y = cy + MathF.Sin(ang) * r * 0.62f;

            int ink = (int)(238 * a * Smooth01(Math.Min(1f, k * 3.4f)));

            float d = MathF.Max(2f, box.Height * (0.11f + 0.05f * k));
            using var dot = new SolidBrush(Color.FromArgb(ink, InkNet));
            g.FillEllipse(dot, x - d / 2f, y - d / 2f, d, d);

            using var tail = new Pen(Color.FromArgb((int)(ink * 0.55f), InkNet), MathF.Max(1.2f, d * 0.6f))
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            float back = MathF.Min(r + box.Width * 0.20f, far);
            g.DrawLine(tail, x, y, cx + MathF.Cos(ang) * back, cy + MathF.Sin(ang) * back * 0.62f);
        }
        g.Clip = clip;
        g.SmoothingMode = keep;
    }

    private static void Filing(Graphics g, RectangleF box, float beat, float a)
    {
        var keep = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float cx = box.X + box.Width / 2f;

        float slot = Smooth01(Math.Clamp((beat - 0.30f) / 0.14f, 0f, 1f)) *
                     (1f - Smooth01(Math.Clamp((beat - 0.74f) / 0.14f, 0f, 1f)));
        if (slot > 0.01f)
        {
            float sw = box.Width * 0.46f, sh = MathF.Max(1.6f, box.Height * 0.075f * slot);
            var lip = new RectangleF(cx - sw / 2f, box.Y + box.Height * 0.115f - sh / 2f, sw, sh);
            using var dark = new SolidBrush(Color.FromArgb((int)(228 * a), 4, 5, 12));
            using var shape = RoundRect(lip, sh * 0.5f);
            g.FillPath(dark, shape);

            using var lit = new Pen(Color.FromArgb((int)(120 * a * slot), InkTray), 1f);
            g.DrawLine(lit, lip.X + sh, lip.Bottom, lip.Right - sh, lip.Bottom);
        }

        float k = Math.Clamp((beat - 0.10f) / 0.52f, 0f, 1f);
        if (k <= 0f || k >= 1f) { g.SmoothingMode = keep; return; }

        float carry = Smooth01(Math.Clamp(k / 0.80f, 0f, 1f));
        float post = Smooth01(Math.Clamp((k - 0.80f) / 0.20f, 0f, 1f));

        float cw = box.Width * 0.34f * (1f - post), ch = box.Width * 0.34f * 0.68f;
        float slotY = box.Y + box.Height * 0.115f;
        float x = box.Right + box.Width * 0.55f + (cx - (box.Right + box.Width * 0.55f)) * carry;
        float y = slotY - ch + box.Height * 0.34f * post;
        var card = new RectangleF(x - cw / 2f, y, MathF.Max(0.6f, cw), ch);
        if (cw <= 0.6f) { g.SmoothingMode = keep; return; }

        var clip = g.Clip;
        if (post > 0.001f)
            using (var head = HeadPath(box))
                g.SetClip(head, CombineMode.Exclude);

        int ink = (int)(a * 236);
        using (var body = new SolidBrush(Color.FromArgb(ink, 30, 33, 52)))
        using (var shape = RoundRect(card, MathF.Min(ch, cw) * 0.16f))
            g.FillPath(body, shape);
        using (var rim = new Pen(Color.FromArgb(ink, InkTray), MathF.Max(1f, ch * 0.09f)))
        using (var shape = RoundRect(card, MathF.Min(ch, cw) * 0.16f))
            g.DrawPath(rim, shape);

        if (cw > box.Width * 0.15f)
            using (var rule = new Pen(Color.FromArgb((int)(ink * 0.55f), InkTray), MathF.Max(1f, ch * 0.07f)))
            {
                g.DrawLine(rule, card.X + cw * 0.18f, card.Y + ch * 0.36f, card.Right - cw * 0.18f, card.Y + ch * 0.36f);
                g.DrawLine(rule, card.X + cw * 0.18f, card.Y + ch * 0.62f, card.X + cw * 0.60f, card.Y + ch * 0.62f);
            }
        g.Clip = clip;
        g.SmoothingMode = keep;
    }

    private static void Pairing(Graphics g, RectangleF box, float beat, float a)
    {
        float k = Math.Clamp((beat - 0.10f) / 0.55f, 0f, 1f);
        if (k <= 0f) return;
        var keep = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float cx = box.X + box.Width / 2f, cy = box.Y + box.Height * 0.48f;
        float e = Smooth01(k);

        float ang = -0.9f + 3.4f * e;
        float orbit = box.Width * 0.78f * (1f - e);
        float px = cx + MathF.Cos(ang) * orbit, py = cy + MathF.Sin(ang) * orbit * 0.55f;

        float r = box.Height * (0.30f + 0.34f * (1f - e));
        int ink = (int)(238 * a * (1f - Smooth01(Math.Clamp((k - 0.82f) / 0.18f, 0f, 1f))));
        if (ink <= 3) { g.SmoothingMode = keep; return; }
        using var pen = new Pen(Color.FromArgb(ink, InkBt), MathF.Max(1.2f, box.Height * 0.055f));
        g.DrawEllipse(pen, px - r, py - r * 0.86f, r * 2f, r * 1.72f);
        g.SmoothingMode = keep;
    }

    private static void Antenna(Graphics g, RectangleF box, float on, float a)
    {

        float ox = box.X + box.Width * 0.70f, oy = box.Y + box.Height * 0.16f;
        float w = Math.Max(1.1f, box.Height * 0.055f);
        using var pen = new Pen(Color.FromArgb((int)(238 * a), InkNet), w)
        { StartCap = LineCap.Round, EndCap = LineCap.Round };

        using (var dot = new SolidBrush(Color.FromArgb((int)(238 * a), InkNet)))
            g.FillEllipse(dot, ox - w, oy - w, w * 2f, w * 2f);
        for (int i = 0; i < 3; i++)
        {
            float step = Math.Clamp((on - i * 0.17f) / 0.5f, 0f, 1f);
            if (step <= 0.01f) continue;

            float r = box.Height * (0.11f + i * 0.10f) * (0.92f + 0.08f * Math.Clamp(on, 0f, 1.3f));
            g.DrawArc(pen, new RectangleF(ox - r, oy - r, r * 2f, r * 2f), -72f, 76f * step);
        }
    }

    private static void Tray(Graphics g, RectangleF box, float on, float a)
    {
        float w = box.Width * 0.74f, h = Math.Max(1.6f, box.Height * 0.095f);
        float x = box.X + (box.Width - w) / 2f;
        float y = box.Y - box.Height * 0.02f - box.Height * 0.38f * (1f - Smooth01(on));
        using var body = new SolidBrush(Color.FromArgb((int)(238 * a), InkTray));

        float tw = w * 0.34f, th = h * 0.9f;
        using (var tab = RoundRect(new RectangleF(x + w * 0.06f, y - th * 0.72f, tw, th), th * 0.4f))
            g.FillPath(body, tab);
        using (var bar = RoundRect(new RectangleF(x, y, w, h), h * 0.45f))
            g.FillPath(body, bar);
    }

    private static void Earbud(Graphics g, RectangleF box, float on, float a)
    {
        float d = box.Height * 0.22f;
        float cx = box.Right - d * 0.15f + box.Width * 0.30f * (1f - Smooth01(on));
        float cy = box.Y + box.Height * 0.44f;
        using var body = new SolidBrush(Color.FromArgb((int)(238 * a), InkBt));
        g.FillEllipse(body, cx - d / 2f, cy - d / 2f, d, d);

        using var pen = new Pen(Color.FromArgb((int)(220 * a), InkBt), Math.Max(1.1f, d * 0.30f))
        { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, cx, cy + d * 0.30f, cx, cy + d * 0.92f);
    }

    private static void Search(Graphics g, RectangleF box, float on, float a)
    {
        float r = box.Height * 0.17f;

        float sweep = Math.Clamp(on, 0f, 1f);
        float cx = (box.Right + box.Width * 0.42f) - (box.Width * 1.84f) * sweep;
        float cy = box.Y + box.Height * 0.44f;

        float leaving = 1f - Smooth01(Math.Clamp((sweep - 0.80f) / 0.20f, 0f, 1f));
        if (leaving <= 0.02f) return;
        using var pen = new Pen(Color.FromArgb((int)(238 * a * leaving), InkSearch), Math.Max(1.2f, box.Height * 0.062f))
        { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawEllipse(pen, cx - r, cy - r, r * 2f, r * 2f);
        float k = r * 0.72f;
        g.DrawLine(pen, cx + r * 0.66f, cy + r * 0.66f, cx + r * 0.66f + k, cy + r * 0.66f + k);
    }

    private static void Brackets(Graphics g, RectangleF box, float on, float a)
    {
        float h = box.Height * 0.34f, reach = box.Width * 0.13f;
        float cy = box.Y + box.Height * 0.46f;
        float travel = box.Width * 0.26f * (1f - Smooth01(on));
        using var pen = new Pen(Color.FromArgb((int)(238 * a), InkCodex), Math.Max(1.2f, box.Height * 0.062f))
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        float lx = box.X - reach * 0.4f - travel, rx = box.Right + reach * 0.4f + travel;
        g.DrawLines(pen, [new PointF(lx, cy - h / 2f), new PointF(lx - reach, cy), new PointF(lx, cy + h / 2f)]);
        g.DrawLines(pen, [new PointF(rx, cy - h / 2f), new PointF(rx + reach, cy), new PointF(rx, cy + h / 2f)]);
    }

    private static void Spark(Graphics g, RectangleF box, float on, float a)
    {
        float cx = box.X + box.Width / 2f;

        float cy = box.Y - box.Height * 0.01f + box.Height * 0.08f * (1f - Smooth01(on));
        float len = box.Height * 0.165f * Smooth01(on);
        if (len < 0.6f) return;
        using var pen = new Pen(Color.FromArgb((int)(238 * a), InkClaude), Math.Max(1.1f, box.Height * 0.05f))
        { StartCap = LineCap.Round, EndCap = LineCap.Round };

        float spin = (1f - Smooth01(on)) * 0.78f;
        for (int i = 0; i < 4; i++)
        {
            float ang = spin + i * MathF.PI / 4f;

            float k = i % 2 == 0 ? len : len * 0.62f;
            float dx = MathF.Cos(ang) * k, dy = MathF.Sin(ang) * k;
            g.DrawLine(pen, cx - dx, cy - dy, cx + dx, cy + dy);
        }
    }

    private static void Think(Graphics g, RectangleF box, float on, float a)
    {
        float d = box.Height * 0.125f;
        float step = box.Height * 0.19f;
        float cx = box.X + box.Width / 2f;
        float cy = box.Y - box.Height * 0.09f + box.Height * 0.10f * (1f - Smooth01(on));
        using var dot = new SolidBrush(Color.FromArgb((int)(238 * a), InkThink));
        for (int i = 0; i < 3; i++)
        {

            float k = Smooth01(Math.Clamp((on - i * 0.16f) / 0.55f, 0f, 1f));
            if (k <= 0.02f) continue;
            float r = d * (0.55f + 0.45f * k) / 2f;
            float x = cx + (i - 1) * step;
            g.FillEllipse(dot, x - r, cy - r, r * 2f, r * 2f);
        }
    }

    internal readonly record struct EatStyle(
        float FlyStart, float FlySpan, float GulpSpan,
        float MouthW, float MouthH,
        int Shards, float ShardSize, float ShardSpeed, float ShardLife)
    {
        internal static EatStyle Default => new(0.14f, 0.30f, 0.13f, 0.40f, 0.32f, 6, 0.17f, 1.25f, 0.46f);

        internal static (string Name, EatStyle Style)[] Variants =>
        [
            ("1 as it is now",   Default),
            ("2 snap",           Default with { FlyStart = 0.10f, FlySpan = 0.22f, GulpSpan = 0.08f }),
            ("3 savour",         Default with { FlyStart = 0.12f, FlySpan = 0.40f, GulpSpan = 0.20f }),
            ("4 big burst",      Default with { ShardSpeed = 2.10f, ShardLife = 0.60f }),
            ("5 confetti",       Default with { Shards = 14, ShardSize = 0.085f, ShardSpeed = 1.70f }),
            ("6 chunks",         Default with { Shards = 3, ShardSize = 0.26f, ShardSpeed = 1.00f }),
            ("7 wide mouth",     Default with { MouthW = 0.52f, MouthH = 0.40f }),
            ("8 everything louder", Default with { FlySpan = 0.24f, GulpSpan = 0.09f, MouthW = 0.50f,
                                                   MouthH = 0.38f, Shards = 8, ShardSize = 0.20f,
                                                   ShardSpeed = 2.00f, ShardLife = 0.55f }),
        ];
    }

        internal static EatStyle Eat = EatStyle.Default;

    private static void Devour(Graphics g, RectangleF box, float beat, float a, Image? icon)
    {
        if (icon == null) return;
        float b = Math.Clamp(beat, 0f, 1f);
        float H = box.Height, W = box.Width;
        float mouthX = box.X + W / 2f, mouthY = box.Y + H * 0.70f;

        var st = Eat;
        float openAt = st.FlyStart + st.FlySpan * 0.34f;
        float shutAt = st.FlyStart + st.FlySpan + st.GulpSpan;
        float open = Smooth01(Math.Clamp((b - openAt) / 0.14f, 0f, 1f)) *
                     (1f - Smooth01(Math.Clamp((b - shutAt) / 0.10f, 0f, 1f)));
        if (open > 0.01f)
        {
            float mw = W * st.MouthW, mh = H * st.MouthH * open;
            var mouth = new RectangleF(mouthX - mw / 2f, mouthY - mh / 2f, mw, mh);

            using (var hole = new SolidBrush(Color.FromArgb((int)(240 * a), 5, 5, 10)))
            using (var shape = RoundRect(mouth, MathF.Min(mh, mw) / 2f))
                g.FillPath(hole, shape);
            using (var lip = new Pen(Color.FromArgb((int)(170 * a * open), 186, 196, 255),
                                     Math.Max(1f, H * 0.026f)))
            using (var shape = RoundRect(mouth, MathF.Min(mh, mw) / 2f))
                g.DrawPath(lip, shape);
        }

        float fly = Smooth01(Math.Clamp((b - st.FlyStart) / st.FlySpan, 0f, 1f));
        float gulp = Smooth01(Math.Clamp((b - (st.FlyStart + st.FlySpan)) / st.GulpSpan, 0f, 1f));
        if (gulp < 1f)
        {
            float s = H * (0.50f - 0.20f * fly) * (1f - gulp * 0.9f);
            if (s >= 2f)
            {
                float x0 = box.Right + W * 0.62f, y0 = box.Y + H * 0.90f;
                float ix = x0 + (mouthX - x0) * fly;
                float iy = y0 + (mouthY - y0) * fly;
                DrawIcon(g, icon, new RectangleF(ix - s / 2f, iy - s / 2f, s, s), a * (1f - gulp * 0.5f));
            }
        }

        float age = b - (st.FlyStart + st.FlySpan + st.GulpSpan * 0.45f);
        if (age > 0f && age < st.ShardLife)
        {
            var tint = icon is Bitmap bmp ? Fx.AccentOf(bmp) : Color.FromArgb(196, 206, 255);
            float life = age / st.ShardLife;
            int ink = (int)(245 * a * (1f - Smooth01(life)));
            if (ink > 3)
            {
                using var shard = new SolidBrush(Color.FromArgb(ink, tint));

                using var pale = new SolidBrush(Color.FromArgb(ink,
                    Math.Min(255, tint.R + 70), Math.Min(255, tint.G + 70), Math.Min(255, tint.B + 70)));

                for (int i = 0; i < st.Shards; i++)
                {

                    float f = (i + 1) * 0.6180339887f;

                    float ang = -MathF.PI * (0.15f + 0.70f * (f - MathF.Floor(f)));
                    float speed = H * st.ShardSpeed * (1f + 0.6f * ((i % 3) / 2f));
                    float px2 = mouthX + MathF.Cos(ang) * speed * age;

                    float py2 = mouthY + MathF.Sin(ang) * speed * age + H * 3.4f * age * age;
                    float sz = H * st.ShardSize * (1f - 0.47f * life);
                    if (sz < 0.7f) continue;
                    using var piece = RoundRect(new RectangleF(px2 - sz / 2f, py2 - sz / 2f, sz, sz), sz * 0.22f);
                    g.FillPath(i % 2 == 0 ? shard : pale, piece);
                }
            }
        }
    }

    private static void DrawIcon(Graphics g, Image icon, RectangleF into, float a)
    {
        int px = Math.Max(1, (int)MathF.Ceiling(into.Width));
        using var scaled = new Bitmap(px, px, PixelFormat.Format32bppPArgb);
        using (var sg = Graphics.FromImage(scaled))
        {
            sg.InterpolationMode = InterpolationMode.HighQualityBicubic;
            sg.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using var ia = new ImageAttributes();
            ia.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(a, 0f, 1f) });
            int side = Math.Min(icon.Width, icon.Height);
            sg.DrawImage(icon, new Rectangle(0, 0, px, px),
                         (icon.Width - side) / 2, (icon.Height - side) / 2, side, side, GraphicsUnit.Pixel, ia);
        }
        g.DrawImage(scaled, into.X, into.Y, into.Width, into.Height);
    }

    private static void Headphones(Graphics g, RectangleF box, float on, float a)
    {
        float cw = box.Width * 0.19f, ch = box.Height * 0.40f;

        float drop = box.Height * 1.30f * (1f - Math.Clamp(on, 0f, 1f));
        float cy = box.Y + box.Height * 0.46f - ch / 2f - drop;
        int ink = (int)(238 * a);
        using var body = new SolidBrush(Color.FromArgb(ink, 232, 232, 246));
        using var pad = new SolidBrush(Color.FromArgb((int)(210 * a), 150, 158, 232));

        foreach (float side in new[] { -1f, 1f })
        {
            float cx = side < 0 ? box.X - cw * 0.45f : box.Right - cw * 0.55f;
            var cup = new RectangleF(cx, cy, cw, ch);
            using var shape = Capsule(cup, cw / 2f);
            g.FillPath(body, shape);
            var inner = RectangleF.Inflate(cup, -cw * 0.26f, -ch * 0.18f);
            if (inner.Width > 0.6f && inner.Height > 0.6f)
            {
                using var lining = Capsule(inner, inner.Width / 2f);
                g.FillPath(pad, lining);
            }
        }

        var arc = new RectangleF(box.X - cw * 0.2f, box.Y - box.Height * 0.10f - drop,
                                 box.Width + cw * 0.4f, box.Height * 0.9f);
        using var bandPen = new Pen(Color.FromArgb(ink, 232, 232, 246), Math.Max(1f, box.Height * 0.075f))
        { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawArc(bandPen, arc, 200f, 140f);
    }

    private static void Goggles(Graphics g, RectangleF box, float on, float a)
    {
        float w = box.Width * 0.78f, h = box.Height * 0.30f;
        float x = box.X + (box.Width - w) / 2f;
        float y = box.Y + box.Height * 0.36f - h / 2f - box.Height * 0.5f * (1f - on);
        var visor = new RectangleF(x, y, w, h);

        using (var glassShape = RoundRect(visor, h / 2f))
        {
            using var fill = new LinearGradientBrush(
                new RectangleF(visor.X, visor.Y - 0.5f, visor.Width, visor.Height + 1f),
                Color.FromArgb((int)(150 * a), 42, 46, 92),
                Color.FromArgb((int)(120 * a), 20, 22, 54), LinearGradientMode.Vertical);
            g.FillPath(fill, glassShape);
            using var rim = new Pen(Color.FromArgb((int)(190 * a), 186, 196, 255), Math.Max(1f, h * 0.10f));
            g.DrawPath(rim, glassShape);
        }
    }

    private static void Download(Graphics g, RectangleF box, float on, float a, float beat)
    {
        float cx = box.X + box.Width / 2f;
        float len = box.Height * 0.30f;

        float tip = box.Y + box.Height * 0.06f
                  - box.Height * 1.55f * (1f - Math.Clamp(on, 0f, 1f));

        float hitAt = FaceDirector.DownloadHit / FaceDirector.DownloadBeat;
        float sink = Smooth01(Math.Clamp(
            (beat - hitAt - 0.02f) / (0.26f / FaceDirector.DownloadBeat), 0f, 1f));
        if (sink >= 1f) return;
        len *= 1f - sink;
        tip += box.Height * 0.10f * sink;

        float w = Math.Max(1.4f, box.Height * 0.07f * (1f - sink * 0.4f));
        using var pen = new Pen(Color.FromArgb((int)(238 * a * (1f - sink)), 196, 206, 255), w)
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLine(pen, cx, tip - len, cx, tip);
        float head = len * 0.42f;
        g.DrawLine(pen, cx - head, tip - head, cx, tip);
        g.DrawLine(pen, cx + head, tip - head, cx, tip);
    }

    private static float Smooth01(float t)
    {
        float k = Math.Clamp(t, 0f, 1f);
        return k * k * (3f - 2f * k);
    }

    internal static GraphicsPath HeadPath(RectangleF box)
    {
        float sx = box.Width / RefW, sy = box.Height / RefH;
        PointF P(float x, float y) => new(box.X + (x - RefX) * sx, box.Y + (y - RefY) * sy);

        var path = new GraphicsPath();
        path.AddBezier(P(768, 351), P(658, 351), P(570, 412), P(570, 507));
        path.AddLine(P(570, 507), P(570, 586));
        path.AddBezier(P(570, 586), P(570, 625), P(594, 651), P(634, 660));
        path.AddBezier(P(634, 660), P(674, 670), P(862, 670), P(902, 660));
        path.AddBezier(P(902, 660), P(942, 651), P(966, 625), P(966, 586));
        path.AddLine(P(966, 586), P(966, 507));
        path.AddBezier(P(966, 507), P(966, 412), P(878, 351), P(768, 351));
        path.CloseFigure();
        return path;
    }

    internal static (RectangleF Left, RectangleF Right) Eyes(RectangleF box, Look look)
    {
        float sx = box.Width / RefW, sy = box.Height / RefH;
        float w = 40f * sx * (1f + 1.25f * Math.Clamp(look.Round, 0f, 1f)), full = 90f * sy;
        float h = Math.Max(2f, full * Math.Clamp(look.Open, 0f, 1.6f));

        float cy = box.Y + (474f + 45f - RefY) * sy + look.GazeY * 13f * sy;
        float dx = look.GazeX * 17f * sx;
        float left = box.X + (686f - RefX) * sx + dx;
        float right = box.X + (809f - RefX) * sx + dx;
        return (new RectangleF(left, cy - h / 2f, w, h), new RectangleF(right, cy - h / 2f, w, h));
    }

    internal readonly record struct Liquid(float Level, float Slosh, float Phase, float Bubble);

    internal static void Draw(Graphics g, RectangleF box, Look look, float alpha = 1f, Liquid? liquid = null,
                              Chase? chase = null, float film = -1f)
    {
        int a = (int)(Math.Clamp(alpha, 0f, 1f) * 255);
        if (a <= 0 || box.Width < 2f || box.Height < 2f) return;

        var keepSmoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var head = HeadPath(box);
        Halo(g, head, box, look.Glow, a);

        if (chase is { } run) Chased(g, box, run, Math.Clamp(alpha, 0f, 1f));

        using (var fill = new PathGradientBrush(head)
        {
            CenterPoint = new PointF(box.X + box.Width / 2f, box.Y + box.Height * 0.26f),
            CenterColor = Color.FromArgb(a, 38, 38, 58),
            SurroundColors = [Color.FromArgb(a, 0, 1, 4)],
        })
            g.FillPath(fill, head);

        if (liquid is { } pour)
            Fill(g, box, pour.Level, pour.Slosh, pour.Phase, pour.Bubble, Math.Clamp(alpha, 0f, 1f));

        if (film >= 0f) Screening(g, box, film, Math.Clamp(alpha, 0f, 1f));

        using (var rim = new Pen(Color.FromArgb(a, 8, 9, 16), Math.Max(1f, box.Height * 0.016f)))
            g.DrawPath(rim, head);

        Gloss(g, box, a);
        DrawEyes(g, box, look, a);

        g.SmoothingMode = keepSmoothing;
    }

    private const int HaloPasses = 7;

    private static void Halo(Graphics g, GraphicsPath head, RectangleF box, float strength, int a)
    {
        float k = Math.Clamp(strength, 0f, 2f);
        if (k <= 0.01f) return;
        float unit = Math.Max(1f, box.Height * 0.055f);

        for (int i = HaloPasses; i >= 1; i--)
        {
            float width = unit * i * (7.5f / HaloPasses) * k;
            int alpha = (int)(a * (0.45f / (i * i)) * Math.Min(1f, k));
            if (alpha <= 1) continue;
            using var brush = Ring(box, width, alpha);
            using var pen = new Pen(brush, width) { LineJoin = LineJoin.Round };
            g.DrawPath(pen, head);
        }

        float crisp = Math.Max(1f, unit * 0.42f * k);
        using (var brush = Ring(box, crisp, (int)(a * 0.95f)))
        using (var pen = new Pen(brush, crisp) { LineJoin = LineJoin.Round })
            g.DrawPath(pen, head);
    }

    private static LinearGradientBrush Ring(RectangleF box, float width, int alpha)
    {
        var r = RectangleF.Inflate(box, width + 2f, width + 2f);
        return new LinearGradientBrush(
            new RectangleF(r.X, r.Y, Math.Max(1f, r.Width), Math.Max(1f, r.Height)),
            Blend(RingTone?.Left ?? HaloLeft, alpha), Blend(RingTone?.Right ?? HaloRight, alpha),
            LinearGradientMode.Horizontal)
        { WrapMode = WrapMode.TileFlipXY };
    }

    internal const float CatDrop = 58f, CatSide = 0f;

    private const float CatHeadH = 42f, CatPawGap = 20f;

    private const float CatOut = 18f, CatFromRight = 40f;

    internal static float CatSpotX(RectangleF sheet, float anchor)
        => sheet.X + CatFromRight + (sheet.Width - 2f * CatFromRight) * Math.Clamp(anchor, 0f, 1f);

        internal static float CatTiltAt(float anchor) => anchor < 0.5f ? 360f - CatTilt : CatTilt;

    private const float CatPawDepth = 29f;

    private const float CatShadeX = 4f, CatShadeY = 6f;

    internal const float CatTilt = 169f;

        internal static RectangleF CatHead(RectangleF sheet, float grip, float duck, float anchor = 1f)
    {
        float hh = CatHeadH, hw = hh * Aspect;

        float back = (hh + 14f) * (1f - Math.Clamp(grip, 0f, 1f))
                   + hh * 1.90f * Math.Clamp(duck, 0f, 1f);

        float cx = CatSpotX(sheet, anchor);
        float cy = sheet.Bottom + CatOut - back;
        return new RectangleF(cx - hw / 2f, cy - hh / 2f, hw, hh);
    }

        internal static void Cling(Graphics g, RectangleF sheet, Look look, float grip, float duck,
                               float alpha, float anchor = 1f)
    {

        float a = Math.Clamp(alpha, 0f, 1f) * Math.Clamp(grip, 0f, 1f) *
                  (1f - Smooth01((Math.Clamp(duck, 0f, 1f) - 0.45f) / 0.55f));
        if (a <= 0.02f) return;
        var box = CatHead(sheet, grip, duck, anchor);

        look = look with { GazeX = -look.GazeX, GazeY = -look.GazeY };
        var keep = g.SmoothingMode;
        var saved = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Tilt(g, box, anchor);
        int ink = (int)(a * 255);

        using var head = HeadPath(box);

        Halo(g, head, box, look.Glow * 1.35f, ink);

        using (var film = new LinearGradientBrush(
            new RectangleF(box.X, box.Y - 0.5f, box.Width, box.Height + 1f),
            Color.FromArgb((int)(58 * a), 186, 196, 255),
            Color.FromArgb((int)(44 * a), 198, 158, 245), LinearGradientMode.Vertical))
        {
            film.InterpolationColors = new ColorBlend
            {
                Colors =
                [
                    Color.FromArgb((int)(58 * a), 186, 196, 255),
                    Color.FromArgb((int)(30 * a), 160, 160, 240),
                    Color.FromArgb((int)(46 * a), 198, 158, 245),
                ],
                Positions = [0f, 0.55f, 1f],
            };
            g.FillPath(film, head);
        }
        using (var edge = new LinearGradientBrush(
            new RectangleF(box.X - 0.5f, box.Y - 0.5f, box.Width + 1f, box.Height + 1f),
            Color.FromArgb((int)(150 * a), 176, 190, 255),
            Color.FromArgb((int)(78 * a), 186, 150, 232), LinearGradientMode.ForwardDiagonal))
        using (var rim = new Pen(edge, MathF.Max(1f, box.Height * 0.030f)))
            g.DrawPath(rim, head);

        Gloss(g, box, ink);

        DrawEyes(g, box, look, ink);
        g.Restore(saved);
        g.SmoothingMode = keep;
    }

        private static void Tilt(Graphics g, RectangleF box, float anchor)
    {
        g.TranslateTransform(box.X + box.Width / 2f, box.Y + box.Height / 2f);
        g.RotateTransform(CatTiltAt(anchor));
        g.TranslateTransform(-(box.X + box.Width / 2f), -(box.Y + box.Height / 2f));
    }

    internal static void ClingShadow(Graphics g, RectangleF sheet, float grip, float duck,
                                     float alpha, float anchor = 1f)
    {

        float a = Math.Clamp(alpha, 0f, 1f) * Math.Clamp(grip, 0f, 1f) *
                  (1f - Smooth01((Math.Clamp(duck, 0f, 1f) - 0.45f) / 0.55f));
        if (a <= 0.02f) return;
        var keep = g.SmoothingMode;
        var clip = g.Clip;
        var saved = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var sheetPath = SheetPath(sheet, 26f))
            g.SetClip(sheetPath, CombineMode.Intersect);

        var head = CatHead(sheet, grip, duck, anchor);
        g.TranslateTransform(CatShadeX, CatShadeY);
        Tilt(g, head, anchor);
        for (int i = 2; i >= 0; i--)
        {
            int ink = (int)(a * (i == 0 ? 34 : i == 1 ? 20 : 12));
            if (ink <= 2) continue;
            using var shade = new SolidBrush(Color.FromArgb(ink, 6, 8, 20));
            var grown = RectangleF.Inflate(head, i * 2.2f, i * 2.2f);
            using (var dome = HeadPath(grown))
                g.FillPath(shade, dome);
            float pw = 21f + i * 4.4f, ph = 17f + i * 4.4f;
            float cx = head.X + head.Width / 2f, cy = head.Y + head.Height / 2f;
            float depth = CatPawDepth + (ph + 10f) * (1f - Math.Clamp(grip, 0f, 1f));
            foreach (float side in new[] { -1f, 1f })
                using (var paw = RoundRect(
                    new RectangleF(cx + side * CatPawGap - pw / 2f, cy + depth - ph / 2f, pw, ph),
                    pw * 0.42f))
                    g.FillPath(shade, paw);
        }
        g.Restore(saved);
        g.Clip = clip;
        g.SmoothingMode = keep;
    }

        internal static void ClingPaws(Graphics g, RectangleF sheet, float grip, float duck,
                                   float alpha, float anchor = 1f)
    {

        float a = Math.Clamp(alpha, 0f, 1f) * Math.Clamp(grip, 0f, 1f) *
                  (1f - Smooth01((Math.Clamp(duck, 0f, 1f) - 0.45f) / 0.55f));
        if (a <= 0.02f) return;
        var keep = g.SmoothingMode;
        var saved = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var head = CatHead(sheet, grip, duck, anchor);

        Tilt(g, head, anchor);
        float cx = head.X + head.Width / 2f, cy = head.Y + head.Height / 2f;

        float pw = 21f, ph = 17f;

        float depth = CatPawDepth + (ph + 10f) * (1f - Math.Clamp(grip, 0f, 1f));

        using var pad = new SolidBrush(Color.FromArgb((int)(86 * a), 176, 190, 255));
        using var toe = new Pen(Color.FromArgb((int)(190 * a), 208, 218, 255), 1.4f);
        using var lit = new Pen(Color.FromArgb((int)(225 * a), 206, 216, 255), 1.5f);
        foreach (float side in new[] { -1f, 1f })
        {

            var paw = new RectangleF(cx + side * CatPawGap - pw / 2f, cy + depth - ph / 2f, pw, ph);
            using var shape = RoundRect(paw, pw * 0.42f);
            g.FillPath(pad, shape);

            g.DrawPath(lit, shape);

            for (int i = 0; i < 2; i++)
            {
                float x = paw.X + paw.Width * (0.34f + 0.32f * i);
                g.DrawLine(toe, x, paw.Y + paw.Height * 0.28f, x, paw.Y + paw.Height * 0.80f);
            }
        }
        g.Restore(saved);
        g.SmoothingMode = keep;
    }

    private const float WaveSpan = 24f, WaveNear = 0.60f, WaveFar = 1.02f;

    internal static void Waves(Graphics g, RectangleF box, float phase, float level, float alpha)
    {
        float lvl = Math.Clamp(level, 0f, 1f), a = Math.Clamp(alpha, 0f, 1f);
        if (lvl <= 0.02f || a <= 0.02f || box.Width < 8f) return;
        var keep = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float cx = box.X + box.Width / 2f, cy = box.Y + box.Height * 0.46f;
        float pen = Math.Max(1f, box.Height * 0.038f);
        for (int i = 0; i < 3; i++)
        {

            float f = phase + i / 3f;
            f -= MathF.Floor(f);
            float r = box.Width * (WaveNear + (WaveFar - WaveNear) * f);

            int ink = (int)(150 * a * lvl * MathF.Sin(MathF.PI * f));
            if (ink <= 2) continue;

            using var brush = Ring(box, r, ink);
            using var p = new Pen(brush, pen) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            var arc = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
            g.DrawArc(p, arc, -WaveSpan, WaveSpan * 2f);
            g.DrawArc(p, arc, 180f - WaveSpan, WaveSpan * 2f);
        }
        g.SmoothingMode = keep;
    }

    private const float FillFloor = 0.12f, FillCeiling = 0.88f;
    private static readonly Color Poured = Color.FromArgb(96, 150, 236);

    internal static void Fill(Graphics g, RectangleF box, float level, float slosh, float phase,
                              float bubble, float alpha)
    {
        float a = Math.Clamp(alpha, 0f, 1f);
        if (level < 0f || a <= 0.02f || box.Height < 6f) return;
        float lvl = Math.Clamp(level, 0f, 1f);
        var keep = g.SmoothingMode;
        var clip = g.Clip;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var head = HeadPath(box))
            g.SetClip(head, CombineMode.Intersect);

        float surface = box.Bottom - box.Height * (FillFloor + (FillCeiling - FillFloor) * lvl);

        float amp = box.Height * 0.030f * Math.Clamp(0.18f + slosh, 0f, 1.6f);

        const int steps = 26;
        var top = new PointF[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float u = i / (float)steps;
            float x = box.X - box.Width * 0.08f + box.Width * 1.16f * u;
            top[i] = new PointF(x, surface + amp * MathF.Sin((u * 2.0f + phase) * MathF.Tau));
        }

        using (var body = new GraphicsPath())
        {
            body.AddLines(top);
            body.AddLine(top[^1], new PointF(top[^1].X, box.Bottom + box.Height * 0.1f));
            body.AddLine(new PointF(top[^1].X, box.Bottom + box.Height * 0.1f),
                         new PointF(top[0].X, box.Bottom + box.Height * 0.1f));
            body.CloseFigure();

            using var pour = new LinearGradientBrush(
                new RectangleF(box.X, surface - amp - 1f, box.Width, box.Bottom - surface + amp + 2f),

                Color.FromArgb((int)(118 * a), Poured),
                Color.FromArgb((int)(58 * a), 30, 52, 122), LinearGradientMode.Vertical);
            g.FillPath(pour, body);
        }

        using (var lip = new Pen(Color.FromArgb((int)(190 * a), 176, 214, 255),
                                 Math.Max(1f, box.Height * 0.022f)))
            g.DrawLines(lip, top);

        float b = Math.Clamp(bubble, 0f, 1f);
        if (b > 0.01f)
        {
            float bx = box.X + box.Width * 0.56f;
            float by = box.Bottom - box.Height * 0.10f;
            float rise = by + (surface - by) * Smooth01(Math.Min(1f, b / 0.72f));
            if (b < 0.72f)
            {
                float d = box.Height * 0.085f;
                using var bub = new SolidBrush(Color.FromArgb((int)(200 * a), 198, 226, 255));
                g.FillEllipse(bub, bx - d / 2f, rise - d / 2f, d, d);
            }
            else
            {

                float k = (b - 0.72f) / 0.28f;
                float r = box.Height * (0.05f + 0.10f * k);
                int ink = (int)(190 * a * (1f - k));
                if (ink > 3)
                {
                    using var ring = new Pen(Color.FromArgb(ink, 198, 226, 255),
                                             Math.Max(1f, box.Height * 0.018f));
                    g.DrawEllipse(ring, bx - r, surface - r, r * 2f, r * 2f);
                }
            }
        }

        g.Clip = clip;
        g.SmoothingMode = keep;
    }

    internal static void Letterbox(Graphics g, float w, float h, float amount, float alpha)
    {
        float k = Math.Clamp(amount, 0f, 1f), a = Math.Clamp(alpha, 0f, 1f);
        if (k <= 0.005f || a <= 0.02f) return;
        var keep = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float bar = h * 0.19f * k;

        var clip = g.Clip;
        using (var plate = SheetPath(SheetRect(w, h), h / 2f))
            g.SetClip(plate, CombineMode.Intersect);
        using (var ink = new SolidBrush(Color.FromArgb((int)(232 * a), 3, 4, 9)))
        {
            g.FillRectangle(ink, -1f, -1f, w + 2f, bar + 1f);
            g.FillRectangle(ink, -1f, h - bar, w + 2f, bar + 2f);
        }

        using (var edge = new Pen(Color.FromArgb((int)(70 * a * k), 186, 196, 255), 1f))
        {
            g.DrawLine(edge, 0f, bar + 0.5f, w, bar + 0.5f);
            g.DrawLine(edge, 0f, h - bar - 0.5f, w, h - bar - 0.5f);
        }
        g.Clip = clip;
        g.SmoothingMode = keep;
    }

    internal static Color? FilmTint;

    internal static void Screening(Graphics g, RectangleF box, float phase, float alpha)
    {
        float a = Math.Clamp(alpha, 0f, 1f);
        if (phase < 0f || a <= 0.02f || box.Height < 8f) return;
        var keep = g.SmoothingMode;
        var clip = g.Clip;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var head = HeadPath(box))
            g.SetClip(head, CombineMode.Intersect);

        var tint = FilmTint ?? Color.FromArgb(255, 196, 216, 255);

        int cut = (int)MathF.Floor(phase * 3.1f);
        float f = (cut + 1) * 0.6180339887f;
        float bright = 0.45f + 0.55f * (f - MathF.Floor(f));

        for (int i = 0; i < 3; i++)
        {

            float y = box.Y + box.Height *
                (((phase * (0.31f + 0.11f * i) + i * 0.37f) % 1.2f) - 0.1f);
            float h = box.Height * (0.30f + 0.12f * i);

            int ink = (int)(92 * a * bright * (i == 0 ? 1f : 0.72f));
            if (ink <= 3) continue;

            using var band = new LinearGradientBrush(
                new RectangleF(box.X - 2f, y - 0.5f, box.Width + 4f, h + 1f),
                Color.FromArgb(0, tint), Color.FromArgb(0, tint), LinearGradientMode.Vertical)
            { InterpolationColors = new ColorBlend
                {
                    Colors = [Color.FromArgb(0, tint), Color.FromArgb(ink, tint), Color.FromArgb(0, tint)],
                    Positions = [0f, 0.5f, 1f],
                } };
            g.FillRectangle(band, box.X - 2f, y, box.Width + 4f, h);
        }
        g.Clip = clip;
        g.SmoothingMode = keep;
    }

    internal readonly record struct Chase(float Phase, float Span, int Count, Color Ink);

    internal static void Chased(Graphics g, RectangleF box, Chase c, float alpha)
    {
        float a = Math.Clamp(alpha, 0f, 1f);
        if (c.Count <= 0 || c.Span <= 0.001f || a <= 0.02f || box.Height < 8f) return;
        var keep = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var head = HeadPath(box);

        head.Flatten(null, 0.6f);
        var pts = head.PathPoints;
        if (pts.Length > 3)
        {

            var len = new float[pts.Length + 1];
            for (int i = 1; i <= pts.Length; i++)
            {
                var q = pts[i % pts.Length];
                var r = pts[i - 1];
                len[i] = len[i - 1] + MathF.Sqrt((q.X - r.X) * (q.X - r.X) + (q.Y - r.Y) * (q.Y - r.Y));
            }
            float total = len[^1];
            if (total > 1f)
            {
                float w = MathF.Max(1.4f, box.Height * 0.105f);
                for (int k = 0; k < c.Count; k++)
                {

                    float head0 = c.Phase + k / (float)c.Count;

                    const int steps = 22;
                    for (int i = 0; i < steps; i++)
                    {
                        float f0 = i / (float)steps, f1 = (i + 1f) / steps;

                        int ink = (int)(238 * a * (f1 * f1));
                        if (ink <= 3) continue;
                        var p0 = At(pts, len, total, (head0 - c.Span * (1f - f0)));
                        var p1 = At(pts, len, total, (head0 - c.Span * (1f - f1)));
                        using var pen = new Pen(Color.FromArgb(ink, c.Ink), w)
                        { StartCap = LineCap.Round, EndCap = LineCap.Round };
                        g.DrawLine(pen, p0, p1);
                    }
                }
            }
        }
        g.SmoothingMode = keep;
    }

        private static PointF At(PointF[] pts, float[] len, float total, float f)
    {
        f -= MathF.Floor(f);
        float want = f * total;
        int lo = 0, hi = len.Length - 1;
        while (lo + 1 < hi)
        {
            int mid = (lo + hi) / 2;
            if (len[mid] <= want) lo = mid; else hi = mid;
        }
        float span = MathF.Max(0.0001f, len[lo + 1] - len[lo]);
        float t = Math.Clamp((want - len[lo]) / span, 0f, 1f);
        var p = pts[lo % pts.Length];
        var q = pts[(lo + 1) % pts.Length];
        return new PointF(p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t);
    }

    private static void Gloss(Graphics g, RectangleF box, int a)
    {
        float sx = box.Width / RefW, sy = box.Height / RefH;
        PointF P(float x, float y) => new(box.X + (x - RefX) * sx, box.Y + (y - RefY) * sy);
        using var sheen = new GraphicsPath();
        sheen.AddBezier(P(607, 443), P(643, 388), P(702, 365), P(768, 365));
        sheen.AddBezier(P(768, 365), P(834, 365), P(893, 388), P(929, 443));
        sheen.AddBezier(P(929, 443), P(887, 407), P(832, 388), P(768, 388));
        sheen.AddBezier(P(768, 388), P(704, 388), P(649, 407), P(607, 443));
        sheen.CloseFigure();
        using var brush = new SolidBrush(Color.FromArgb(Math.Max(0, a * 18 / 255), 255, 255, 255));
        g.FillPath(brush, sheen);
    }

    private static void DrawEyes(Graphics g, RectangleF box, Look look, int a)
    {
        var (left, right) = Eyes(box, look);
        foreach (var eye in new[] { left, right })
        {
            if (eye.Width < 0.6f || eye.Height < 0.6f) continue;
            float r = Math.Min(eye.Width, eye.Height) / 2f;
            using var shape = Capsule(eye, r);

            using var brush = new LinearGradientBrush(
                new RectangleF(eye.X, eye.Y - 0.5f, Math.Max(1f, eye.Width), eye.Height + 1f),
                Blend(EyeTop, a), Blend(EyeBottom, a), LinearGradientMode.Vertical);
            brush.InterpolationColors = new ColorBlend
            {
                Colors = [Blend(EyeTop, a), Blend(EyeMid, a), Blend(EyeBottom, a)],
                Positions = [0f, 0.6f, 1f],
            };
            g.FillPath(brush, shape);
        }
    }

    private static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = Math.Min(radius, Math.Min(r.Width, r.Height) / 2f) * 2f;
        if (d <= 1f) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath Capsule(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
        var path = new GraphicsPath();
        if (d <= 1f) { path.AddRectangle(r); return path; }
        path.AddArc(r.X, r.Y, d, d, 180, 180);
        path.AddArc(r.X, r.Bottom - d, d, d, 0, 180);
        path.CloseFigure();
        return path;
    }

    private static Color Blend(Color c, int a) => Color.FromArgb(Math.Clamp(a, 0, 255), c.R, c.G, c.B);
}
