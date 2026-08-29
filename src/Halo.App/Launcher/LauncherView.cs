using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Halo.Widgets;

namespace Halo.Launcher;

internal static class LauncherView
{
    internal const int W = 560;
    internal const int Pad = 12;
    internal const int FieldH = 44;
    internal const int RowH = 34;
    internal const int Radius = 16;

    internal const int GaugeSize = 64;

    internal const int BandPadTop = 12;
    internal const int BandH = BandPadTop + GaugeSize + 34;

    internal const int LangBarH = 52;
    internal const int LangBtnH = 32;
    internal const int SwapD = 30;

        internal static float BandHeight(LauncherState s)
        => s.ShowGauges ? BandH : s.ShowLangBar ? LangBarH : 0f;

    internal static int Height(int rowCount, float bandH)
        => Pad + FieldH + (int)bandH + (rowCount > 0 ? Pad / 2 + rowCount * RowH : 0) + Pad;

    internal static int Height(int rowCount, bool band = false) => Height(rowCount, band ? BandH : 0f);

    internal static bool InHeader(float y) => y >= 0 && y < Pad + FieldH;

    internal static float RowY(int index, float bandH)
    {
        float top = Pad + FieldH + Pad / 2f;
        if (bandH <= 0f) return top + index * RowH;
        return index == 0 ? top : top + RowH + bandH + (index - 1) * RowH;
    }

    internal static float RowY(int index, bool band) => RowY(index, band ? BandH : 0f);

    internal static float GaugeTop(bool band) => RowY(0, band) + RowH + BandPadTop;

    internal static int HitRow(float x, float y, int rowCount, bool band = false)
        => HitRow(x, y, rowCount, band ? BandH : 0f);

    internal static int HitRow(float x, float y, int rowCount, float bandH)
    {
        if (rowCount <= 0) return -1;
        if (x < Pad / 2f || x > W - Pad / 2f) return -1;

        float top = Pad + FieldH + Pad / 2f;
        if (y < top) return -1;
        if (bandH <= 0f)
        {
            int flat = (int)((y - top) / RowH);
            return flat >= 0 && flat < rowCount ? flat : -1;
        }

        if (y < top + RowH) return 0;

        float rest = RowY(1, bandH);
        if (y < rest) return -1;
        int i = 1 + (int)((y - rest) / RowH);
        return i >= 1 && i < rowCount ? i : -1;
    }

    internal static int HitGauge(float x, float y, int count, bool band)
    {
        if (!band || count <= 0) return -1;
        float top = GaugeTop(true);
        if (y < top || y >= top + GaugeSize + 16f) return -1;
        float cell = (W - Pad * 2f) / count;
        int i = (int)((x - Pad) / cell);
        return i >= 0 && i < count ? i : -1;
    }

    internal const float StackBand = 3.5f;
    internal const float StackStep = 5.5f;
    internal const float LoneBand = 4.5f;

    internal static int HitRing(float x, float y, int gaugeIndex, int gaugeCount, int rings, bool band)
    {
        if (!band || rings <= 1 || gaugeIndex < 0 || gaugeCount <= 0) return -1;
        float cell = (W - Pad * 2f) / gaugeCount;
        float cx = Pad + cell * gaugeIndex + cell / 2f;
        float cy = GaugeTop(true) + GaugeSize / 2f;
        double d = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        float outer = GaugeSize / 2f - StackBand / 2f;
        for (int i = 0; i < rings; i++)
        {
            float r = outer - i * StackStep;
            if (Math.Abs(d - r) <= StackStep / 2f + 1f) return i;
        }
        return -1;
    }

    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.Ordinal)
    {
        ["Quick Actions"]     = "\uE945",
        ["System Info"]       = "\uE9D9",
        ["Clipboard History"] = "\uE8C8",
        ["Translate"]         = "\uE8C1",
        ["Reminders"]         = "\uE823",
        ["Settings"]          = "\uE713",
    };

    internal static string MenuGlyph(string label) => Glyphs.TryGetValue(label, out var g) ? g : "";

    internal static void Draw(Graphics g, int w, int h, LauncherState state, float fade,
                              Func<string, Image?>? icon, bool caret = true)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        float a = Math.Clamp(fade, 0f, 1f);
        if (a <= 0f) return;

        var body = new RectangleF(0.5f, 0.5f, w - 1f, h - 1f);
        using (var path = Round(body, Radius))
        {
            using var fill = new LinearGradientBrush(
                new RectangleF(body.X, body.Y - 1f, body.Width, body.Height + 2f),
                Color.FromArgb((int)(245 * a), 38, 38, 46),
                Color.FromArgb((int)(248 * a), 26, 26, 33), LinearGradientMode.Vertical);
            g.FillPath(fill, path);
            using var rim = new Pen(Color.FromArgb((int)(30 * a), 255, 255, 255), 1f);
            g.DrawPath(rim, path);
        }

        DrawField(g, w, state, a, caret);

        float bandH = BandHeight(state);
        for (int i = 0; i < state.Rows.Count; i++)
            DrawRow(g, new RectangleF(Pad / 2f, RowY(i, bandH), w - Pad, RowH),
                    state.Rows[i], i == state.Selected, a, icon);
        if (state.ShowGauges) DrawBand(g, w, state, a);
        else if (state.ShowLangBar) DrawLangBar(g, w, state, a);
    }

        internal enum LangHit { None, From, Swap, To }

    internal static (RectangleF From, RectangleF Swap, RectangleF To) LangBoxes(int w, float bandH)
    {
        float top = RowY(0, bandH) + RowH + (LangBarH - LangBtnH) / 2f;
        float cx = w / 2f;
        float half = (w - Pad * 2f - SwapD) / 2f - 10f;
        return (new RectangleF(Pad, top, half, LangBtnH),
                new RectangleF(cx - SwapD / 2f, top + (LangBtnH - SwapD) / 2f, SwapD, SwapD),
                new RectangleF(w - Pad - half, top, half, LangBtnH));
    }

    internal static LangHit HitLangBar(float x, float y, int w, float bandH)
    {
        if (bandH <= 0f) return LangHit.None;
        var (from, swap, to) = LangBoxes(w, bandH);

        var reach = RectangleF.Inflate(swap, 6f, 6f);
        if (reach.Contains(x, y)) return LangHit.Swap;
        if (from.Contains(x, y)) return LangHit.From;
        if (to.Contains(x, y)) return LangHit.To;
        return LangHit.None;
    }

    private static void DrawLangBar(Graphics g, int w, LauncherState state, float a)
    {
        var (from, swap, to) = LangBoxes(w, BandHeight(state));
        var mouse = WidgetInput.Over ? WidgetInput.Mouse : new PointF(-1, -1);
        var hot = HitLangBar(mouse.X, mouse.Y, w, BandHeight(state));

        using var f = new Font("Segoe UI Semibold", 14f, GraphicsUnit.Pixel);
        using var mid = new StringFormat(StringFormat.GenericTypographic)
        { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        Half(g, from, Translator.Name(LauncherPages.SourceLang()), f, mid, a, hot == LangHit.From);
        Half(g, to, Translator.TargetName(LauncherPages.TargetLang()), f, mid, a, hot == LangHit.To);

        bool lit = hot == LangHit.Swap;
        using (var bg = new SolidBrush(Color.FromArgb((int)((lit ? 46 : 24) * a), 255, 255, 255)))
            g.FillEllipse(bg, swap);
        SwapArrows(g, swap, Color.FromArgb((int)((lit ? 245 : 190) * a), 231, 233, 238));
    }

    private static void Half(Graphics g, RectangleF r, string text, Font f, StringFormat mid, float a, bool lit)
    {
        using (var bg = new SolidBrush(Color.FromArgb((int)((lit ? 40 : 22) * a), 255, 255, 255)))
        using (var p = Round(r, r.Height / 2f))
            g.FillPath(bg, p);
        using (var pen = new Pen(Color.FromArgb((int)((lit ? 60 : 30) * a), 255, 255, 255), 1f))
        using (var p = Round(RectangleF.Inflate(r, -0.5f, -0.5f), r.Height / 2f - 0.5f))
            g.DrawPath(pen, p);
        using var ink = new SolidBrush(Color.FromArgb((int)(238 * a), 231, 233, 238));
        Fx.Text(g, text, f, ink, r, mid);
    }

    private static void SwapArrows(Graphics g, RectangleF r, Color c)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        float half = r.Width * 0.26f, gap = r.Height * 0.15f, head = r.Width * 0.13f;
        using var pen = new Pen(c, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };

        g.DrawLine(pen, cx - half, cy - gap, cx + half, cy - gap);
        g.DrawLines(pen, [new PointF(cx + half - head, cy - gap - head),
                          new PointF(cx + half, cy - gap),
                          new PointF(cx + half - head, cy - gap + head)]);

        g.DrawLine(pen, cx + half, cy + gap, cx - half, cy + gap);
        g.DrawLines(pen, [new PointF(cx - half + head, cy + gap - head),
                          new PointF(cx - half, cy + gap),
                          new PointF(cx - half + head, cy + gap + head)]);
    }

    private static void DrawBand(Graphics g, int w, LauncherState state, float a)
    {
        var gauges = state.Gauges;
        if (gauges.Count == 0) return;

        float cell = (w - Pad * 2f) / gauges.Count;
        using var pct = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);

        using var pctSmall = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var cap = new Font("Segoe UI", 11f, GraphicsUnit.Pixel);
        var mid = new StringFormat(StringFormat.GenericTypographic)
        { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        for (int i = 0; i < gauges.Count; i++)
        {
            var gauge = gauges[i];
            bool hot = i == state.HotGauge;
            float cx = Pad + cell * i + cell / 2f;
            var box = new RectangleF(cx - GaugeSize / 2f, GaugeTop(true), GaugeSize, GaugeSize);

            float[] fracs = gauge.Parts is { Length: > 0 } parts
                ? System.Array.ConvertAll(parts, r => r.Frac)
                : [gauge.Frac];

            Color identity = gauge.Tint ?? Fx.VitalCpu;
            var colours = new Color[fracs.Length];
            for (int k = 0; k < fracs.Length; k++)
                colours[k] = Fx.Vital(identity, gauge.Inverted ? 1f - fracs[k] : fracs[k]);

            bool stacked = fracs.Length > 1;

            float grow = hot ? 1.35f : 1f;
            Fx.Gauge(g, box, fracs, colours, a * (hot ? 1f : 0.82f),
                     band: (stacked ? StackBand : LoneBand) * grow,
                     step: stacked ? StackStep : 6f);

            using var ink = new SolidBrush(Color.FromArgb((int)((hot ? 250 : 225) * a), 231, 233, 238));

            var numberBox = gauge.Badge is { Length: > 0 } ? box with { Y = box.Y + 7f } : box;
            Fx.Text(g, (int)Math.Round(gauge.Frac * 100) + "%", fracs.Length > 1 ? pctSmall : pct, ink,
                    numberBox, mid);

            if (gauge.Badge is { Length: > 0 } badge)
            {
                using var bf = new Font("Segoe Fluent Icons", 12f, GraphicsUnit.Pixel);
                var bc = gauge.Tint ?? Fx.VitalBattery;
                using var bb = new SolidBrush(Color.FromArgb((int)(240 * a), bc.R, bc.G, bc.B));
                Fx.GlyphCentred(g, new RectangleF(box.X, box.Y + 12f, box.Width, 12f), badge, bf, bb);
            }

            Color lab = gauge.Tint ?? Fx.VitalCpu;
            using var sub = new SolidBrush(hot
                ? Color.FromArgb((int)(235 * a), lab.R, lab.G, lab.B)
                : Color.FromArgb((int)(150 * a), Blend(lab, 231, 233, 238, 0.55f)));
            Fx.Text(g, gauge.Label, cap, sub,
                new RectangleF(cx - cell / 2f, box.Bottom + 1f, cell, 14f), mid);
        }

        int at = state.HotGauge;
        if (at < 0 || at >= gauges.Count) return;

        var picked = gauges[at];
        Color pickedIdentity = picked.Tint ?? Fx.VitalCpu;
        string readout = picked.Detail;
        Color tint = Fx.Vital(pickedIdentity, picked.Inverted ? 1f - picked.Frac : picked.Frac);
        int ring = state.HotRing;
        if (picked.Parts is { Length: > 0 } rings && ring >= 0 && ring < rings.Length)
        {
            readout = rings[ring].Detail;
            tint = Fx.Vital(pickedIdentity, picked.Inverted ? 1f - rings[ring].Frac : rings[ring].Frac);
        }
        if (readout.Length == 0) return;

        using var rf = new Font("Segoe UI", 12f, GraphicsUnit.Pixel);
        using var rb = new SolidBrush(Color.FromArgb((int)(225 * a), tint.R, tint.G, tint.B));
        Fx.Text(g, readout, rf, rb,
            new RectangleF(Pad, GaugeTop(true) + GaugeSize + 15f, w - Pad * 2f, 16f), mid);
    }

    private static void DrawField(Graphics g, int w, LauncherState state, float a, bool caret)
    {
        var r = new RectangleF(Pad, Pad, w - Pad * 2, FieldH - 4);
        using (var path = Round(r, 10f))
        {
            using var fill = new SolidBrush(Color.FromArgb((int)(14 * a), 255, 255, 255));
            g.FillPath(fill, path);
            using var edge = new Pen(Color.FromArgb((int)(23 * a), 255, 255, 255), 1f);
            g.DrawPath(edge, path);
        }

        float cy = r.Y + r.Height / 2f;
        Magnifier(g, r.X + 14f, cy, 6f, Color.FromArgb((int)(190 * a), 141, 147, 157));

        float textX = r.X + 30f;
        using var font = new Font("Segoe UI", 15f, GraphicsUnit.Pixel);
        using var fmt = new StringFormat(StringFormat.GenericTypographic)
        { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        string shown = state.Query;
        bool empty = shown.Length == 0;
        using var brush = new SolidBrush(empty
            ? Color.FromArgb((int)(215 * a), 125, 130, 140)
            : Color.FromArgb((int)(250 * a), 238, 240, 244));

        float shownX = empty ? textX + 7f : textX;
        var layout = new RectangleF(shownX, r.Y, r.Right - shownX - 14f, r.Height);
        Fx.Text(g, empty ? LauncherState.PlaceholderFor(state.Page) : Fx.CleanText(shown),
                font, brush, layout, fmt);

        float typedW = empty ? 0f : g.MeasureString(Fx.CleanText(shown), font,
            new PointF(0, 0), StringFormat.GenericTypographic).Width;

        string ghost = state.Completion;
        if (ghost.Length > 0 && !empty)
        {
            float ghostX = textX + typedW + 3f;
            using var faded = new SolidBrush(Color.FromArgb((int)(105 * a), 168, 174, 186));
            var tail = new RectangleF(ghostX, r.Y, Math.Max(0f, r.Right - ghostX - 14f), r.Height);
            if (tail.Width > 4f) Fx.Text(g, Fx.CleanText(ghost), font, faded, tail, fmt);
        }

        if (!caret) return;
        float caretX = textX + typedW + 1.5f;
        using var bar = new SolidBrush(Color.FromArgb((int)(235 * a), 213, 217, 224));
        g.FillRectangle(bar, caretX, cy - 8f, 1.5f, 16f);
    }

    private static void DrawRow(Graphics g, RectangleF r, LauncherRow row, bool selected, float a,
                                Func<string, Image?>? icon)
    {
        if (selected)
        {
            using var wash = new SolidBrush(Color.FromArgb((int)(40 * a), 122, 134, 255));
            g.FillRectangle(wash, r);
            using var bar = new SolidBrush(Color.FromArgb((int)(235 * a), 122, 134, 255));
            g.FillRectangle(bar, r.X, r.Y + 4f, 2f, r.Height - 8f);
        }

        float lit = row.Kind switch
        {
            LauncherRowKind.Info => 1f,
            LauncherRowKind.Notice => 0.72f,
            LauncherRowKind.Back => 0.78f,
            _ => row.Enabled ? 1f : 0.45f,
        };
        var ink = Color.FromArgb((int)(245 * a * lit), 231, 233, 238);
        float cy = r.Y + r.Height / 2f;
        float iconX = r.X + 14f;

        string glyph = row.Glyph ?? MenuGlyph(row.Label);

        bool hasIcon = row.Kind is LauncherRowKind.App or LauncherRowKind.Tick || glyph.Length > 0;

        if (row.Kind == LauncherRowKind.App)
        {
            var box = new RectangleF(iconX, cy - 9f, 18f, 18f);
            Image? art = null;
            try { art = icon?.Invoke(row.Aumid ?? ""); } catch { }
            if (art is not null)
            {
                try { g.DrawImage(art, box); } catch { }
            }
            else
            {
                using var ph = new SolidBrush(Color.FromArgb((int)(70 * a), 255, 255, 255));
                using var p = Round(box, 5f);
                g.FillPath(ph, p);
            }
        }
        else if (row.Kind == LauncherRowKind.Tick)
        {

            var box = new RectangleF(iconX + 1f, cy - 8f, 16f, 16f);
            Color ring = row.Tint is { } rt
                ? Color.FromArgb((int)(200 * a * lit), Blend(rt, 231, 233, 238, 0.15f))
                : ink;

            using (var pen = new Pen(Color.FromArgb((int)((selected ? 235 : 150) * a), ring), 1.5f))
                g.DrawEllipse(pen, box);
            if (selected)
            {
                using var check = new Pen(Color.FromArgb((int)(240 * a), ring), 1.8f)
                { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(check, [new PointF(box.X + 4.0f, box.Y + 8.2f),
                                    new PointF(box.X + 6.8f, box.Y + 11.2f),
                                    new PointF(box.X + 12.2f, box.Y + 4.8f)]);
            }
        }
        else if (glyph.Length > 0)
        {
            using var gf = new Font("Segoe Fluent Icons", 15f, GraphicsUnit.Pixel);

            Color gc = row.Tint is { } t
                ? Color.FromArgb((int)(238 * a * lit), Blend(t, 231, 233, 238, 0.28f))
                : ink;
            using var gb = new SolidBrush(gc);
            Fx.GlyphCentred(g, new RectangleF(iconX, cy - 9f, 18f, 18f), glyph, gf, gb);
        }

        bool drills = row.Kind is LauncherRowKind.Inert or LauncherRowKind.Settings or LauncherRowKind.Page;

        string detail = Fx.CleanText(row.Detail ?? "");
        float detailW = 0f;
        if (detail.Length > 0)
        {
            using var df = new Font("Segoe UI", 12f, GraphicsUnit.Pixel);
            detailW = Math.Min(g.MeasureString(detail, df).Width + 10f, r.Width * 0.5f);
        }

        float textX = hasIcon ? iconX + 28f : r.X + 14f;
        float textRight = r.Right - (drills ? 26f : 14f) - detailW;

        using var font = new Font("Segoe UI", 14f, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(ink);
        string label = Fx.CleanText(row.Label);
        using var fmt = new StringFormat(StringFormat.GenericTypographic)
        {
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter,
        };

        if (Fx.IsRtl(label)) fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        Fx.Text(g, label, font, brush, new RectangleF(textX, r.Y, textRight - textX, r.Height), fmt);

        if (detail.Length > 0)
        {
            using var df = new Font("Segoe UI", 12f, GraphicsUnit.Pixel);

            int dAlpha = row.Kind == LauncherRowKind.Info ? 225 : 150;
            using var db = new SolidBrush(Color.FromArgb((int)(dAlpha * a * lit), 231, 233, 238));

            bool chipped = row.Kind is LauncherRowKind.Action or LauncherRowKind.Tick && detail.Length <= 10;
            using var dfmt = new StringFormat(StringFormat.GenericTypographic)
            {
                LineAlignment = StringAlignment.Center,

                Alignment = chipped ? StringAlignment.Center : StringAlignment.Far,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter,
            };
            float dRight = r.Right - (drills ? 26f : 14f);

            float labelEnd = textX + g.MeasureString(label, font, int.MaxValue, fmt).Width;
            float pulled = Math.Min(dRight, labelEnd + 16f + detailW);
            var dRect = new RectangleF(pulled - detailW, r.Y, detailW, r.Height);

            if (chipped)
            {
                var chip = new RectangleF(dRect.X + 2f, cy - 9f, dRect.Width - 4f, 18f);
                using var chipFill = new SolidBrush(Color.FromArgb((int)(28 * a), 255, 255, 255));
                using var chipPath = Round(chip, 9f);
                g.FillPath(chipFill, chipPath);
            }
            Fx.Text(g, detail, df, db, dRect, dfmt);
        }

        if (drills)
            Chevron(g, r.Right - 18f, cy, Color.FromArgb((int)(120 * a * lit), 231, 233, 238));

        if (row.Kind == LauncherRowKind.Back)
        {
            using var rule = new Pen(Color.FromArgb((int)(26 * a), 255, 255, 255), 1f);
            g.DrawLine(rule, r.X + 12f, r.Bottom - 0.5f, r.Right - 12f, r.Bottom - 0.5f);
        }
    }

    private static Color Blend(Color c, int r, int g, int b, float toward)
        => Color.FromArgb((int)(c.R + (r - c.R) * toward),
                          (int)(c.G + (g - c.G) * toward),
                          (int)(c.B + (b - c.B) * toward));

    private static void Magnifier(Graphics g, float cx, float cy, float radius, Color c)
    {
        using var pen = new Pen(c, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
        float d = radius * 0.72f;
        g.DrawLine(pen, cx + d, cy + d, cx + d + 4f, cy + d + 4f);
    }

    private static void Chevron(Graphics g, float x, float cy, Color c)
    {
        using var pen = new Pen(c, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, [new PointF(x, cy - 4f), new PointF(x + 4f, cy), new PointF(x, cy + 4f)]);
    }

    private static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.Left, r.Top, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
