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
    internal const int Radius = 24;

    internal const int IconAxis = Pad + 23;
    internal const int TextAxis = Pad + 44;

    internal const int BandPadTop = 8;
    internal const int CardH = 88;
    internal const int BandH = BandPadTop + CardH + 26;

    internal const int LangBarH = 52;
    internal const int LangBtnH = 32;
    internal const int SwapD = 30;

        internal static float BandHeight(LauncherState s)
        => s.ShowGauges ? BandH : s.ShowLangBar ? LangBarH : 0f;

    internal static int Height(int rowCount, float bandH = 0f)
        => (int)LauncherLayout.Compute(rowCount, false, bandH).Height;

    internal static bool InHeader(float y) => y >= 0 && y < Pad + FieldH;

    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.Ordinal)
    {
        ["Quick Actions"]     = "\uE945",
        ["System Info"]       = "\uE950",
        ["Clipboard History"] = "\uF0E3",
        ["Translate"]         = "\uF2B7",
        ["Reminders"]         = "\uEA8F",
        ["Settings"]          = "\uE713",
    };

    internal static string MenuGlyph(string label) => Glyphs.TryGetValue(label, out var g) ? g : "";

    private static (Color Left, Color Right) Tone => Face.RingTone ?? (Face.HaloLeft, Face.HaloRight);

    internal static void Draw(Graphics g, int w, int h, LauncherState state, float fade,
                              Func<string, Image?>? icon, bool caret = true,
                              LauncherGlass? glass = null, Point glassAt = default, RectangleF? highlight = null)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        float a = Math.Clamp(fade, 0f, 1f);

        var body = new RectangleF(0.5f, 0.5f, w - 1f, h - 1f);
        using (var path = Round(body, Math.Min(Radius, Math.Min(body.Width, body.Height) / 2f)))
            DrawSurface(g, path, body, glass, glassAt, a);
        if (a <= 0f) return;

        var frame = LauncherLayout.Of(state);
        bool form = LauncherLayout.IsForm(state);
        DrawField(g, w, state, a, caret);
        if (form) DrawWhen(g, frame.Band, state, a);

        if (highlight is { } moving) Highlight(g, moving, a, withRail: !form);
        else if (state.Selected >= 0 && state.Selected < frame.Cells.Length && !frame.Cells[state.Selected].IsEmpty)
            Highlight(g, frame.Cells[state.Selected], a, withRail: !form);

        for (int i = 0; i < state.Rows.Count; i++)
        {
            var cell = frame.Cells[i];
            if (cell.IsEmpty) continue;
            var row = state.Rows[i];
            if (row.Kind == LauncherRowKind.Back) BackButton(g, cell, a);
            else if (form) DrawFormCell(g, cell, row, state, a);
            else DrawRow(g, cell, row, i == state.Selected, a, icon, i == state.HotTick, highlight?.Y);
        }

        if (state.ShowGauges) DrawBand(g, frame.Band, state, a);
        else if (state.ShowLangBar) DrawLangBar(g, w, state, a);

        if (LauncherLayout.GroupStart(state) is var split and >= 0 && split < frame.Cells.Length)
        {
            var c = frame.Cells[split];
            float y = c.Y - LauncherLayout.GroupGap / 2f;
            using var rule = new Pen(Color.FromArgb((int)(70 * a), 255, 255, 255), 1f);
            g.DrawLine(rule, c.X + 10f, y, c.Right - 10f, y);
        }
    }

    private static void DrawSurface(Graphics g, GraphicsPath path, RectangleF body,
                                    LauncherGlass? glass, Point glassAt, float a)
    {
        if (glass is null || !glass.Fill(g, path, glassAt))
        {
            if (a <= 0f) return;
            using var fill = new LinearGradientBrush(
                new RectangleF(body.X, body.Y - 1f, body.Width, body.Height + 2f),
                Color.FromArgb((int)(245 * a), 38, 40, 52),
                Color.FromArgb((int)(248 * a), 24, 26, 36), LinearGradientMode.Vertical);
            g.FillPath(fill, path);
        }

        using var rim = new Pen(Color.FromArgb(38, 255, 255, 255), 1f);
        g.DrawPath(rim, path);
    }

    private static void Highlight(Graphics g, RectangleF r, float a, bool withRail = true)
    {
        var (left, right) = Tone;
        bool round = r.Width < 40f;
        using var path = Round(r, round ? r.Height / 2f : 9f);
        using (var wash = new LinearGradientBrush(new RectangleF(r.X - 1f, r.Y, r.Width + 2f, r.Height),
                   Color.FromArgb((int)(52 * a), left), Color.FromArgb((int)(40 * a), right), LinearGradientMode.Horizontal))
            g.FillPath(wash, path);
        if (round || !withRail) return;
        using var bar = new SolidBrush(Color.FromArgb((int)(235 * a), left));
        using var rail = Round(new RectangleF(r.X + 3f, r.Y + 8f, 2.5f, r.Height - 16f), 1.2f);
        g.FillPath(bar, rail);
    }

    private static void BackButton(Graphics g, RectangleF r, float a)
    {
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        using var pen = new Pen(Color.FromArgb((int)(215 * a), 231, 233, 238), 1.6f)
        { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLine(pen, cx - 5.5f, cy, cx + 5.5f, cy);
        g.DrawLines(pen, [new PointF(cx - 1f, cy - 4.5f), new PointF(cx - 5.5f, cy), new PointF(cx - 1f, cy + 4.5f)]);
    }

        internal enum LangHit { None, From, Swap, To }

        internal static (RectangleF From, RectangleF Swap, RectangleF To) LangBoxes(int w)
    {
        float top = Pad + FieldH + Pad / 2f + (LangBarH - LangBtnH) / 2f;
        float cx = w / 2f;
        float half = (w - Pad * 2f - SwapD) / 2f - 10f;
        return (new RectangleF(Pad, top, half, LangBtnH),
                new RectangleF(cx - SwapD / 2f, top + (LangBtnH - SwapD) / 2f, SwapD, SwapD),
                new RectangleF(w - Pad - half, top, half, LangBtnH));
    }

    internal static LangHit HitLangBar(float x, float y, int w, float bandH)
    {
        if (bandH <= 0f) return LangHit.None;
        var (from, swap, to) = LangBoxes(w);

        var reach = RectangleF.Inflate(swap, 6f, 6f);
        if (reach.Contains(x, y)) return LangHit.Swap;
        if (from.Contains(x, y)) return LangHit.From;
        if (to.Contains(x, y)) return LangHit.To;
        return LangHit.None;
    }

    private static void DrawLangBar(Graphics g, int w, LauncherState state, float a)
    {
        var (from, swap, to) = LangBoxes(w);
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

    private static string CardGlyph(LauncherPages.Gauge gauge) => gauge.Label switch
    {
        "CPU" => "\uE950",
        "GPU" => "\uF211",
        "Memory" => "\uE964",
        "Storage" => "\uEDA2",
        "Battery" => BatteryGlyph(gauge.Frac, gauge.Badge is { Length: > 0 }),
        _ => "",
    };

        internal static string BatteryGlyph(float frac, bool charging)
    {
        int level = Math.Clamp((int)Math.Round(frac * 10f), 0, 10);
        return ((char)((charging ? 0xEBAB : 0xEBA0) + level)).ToString();
    }

    private static void DrawBand(Graphics g, RectangleF band, LauncherState state, float a)
    {
        var gauges = state.Gauges;
        if (gauges.Count == 0 || band.IsEmpty) return;

        const float Gap = 8f;
        float cardW = (band.Width - Gap * (gauges.Count - 1)) / gauges.Count;
        using var icon = new Font("Segoe Fluent Icons", 19f, GraphicsUnit.Pixel);
        using var pct = new Font("Segoe UI Semibold", 19f, GraphicsUnit.Pixel);
        using var cap = new Font("Segoe UI", 11.5f, GraphicsUnit.Pixel);
        using var mid = new StringFormat(StringFormat.GenericTypographic)
        { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        for (int i = 0; i < gauges.Count; i++)
        {
            var gauge = gauges[i];
            bool hot = i == state.HotGauge;
            var card = new RectangleF(band.X + (cardW + Gap) * i, band.Y + BandPadTop, cardW, CardH);
            Color identity = gauge.Tint ?? Fx.VitalCpu;
            float strain = gauge.Inverted ? 1f - gauge.Frac : gauge.Frac;
            Color tone = strain >= 0.85f ? Fx.Vital(identity, strain) : identity;

            using (var path = Round(card, 13f))
            {
                using (var fill = new SolidBrush(Color.FromArgb((int)((hot ? 30 : 15) * a), 255, 255, 255)))
                    g.FillPath(fill, path);
                using var edge = new Pen(Color.FromArgb((int)((hot ? 52 : 22) * a), 255, 255, 255), 1f);
                g.DrawPath(edge, path);
            }

            using (var b = new SolidBrush(Color.FromArgb((int)(240 * a), Blend(tone, 236, 238, 243, 0.12f))))
                Fx.GlyphCentred(g, new RectangleF(card.X, card.Y + 8f, card.Width, 22f), CardGlyph(gauge), icon, b);
            using (var ink = new SolidBrush(Color.FromArgb((int)(245 * a), 240, 242, 247)))
                Fx.Text(g, (int)Math.Round(gauge.Frac * 100) + "%", pct, ink, new RectangleF(card.X, card.Y + 32f, card.Width, 24f), mid);
            string name = gauge.Badge is { Length: > 0 } ? "Charging" : gauge.Label;
            using (var sub = new SolidBrush(Color.FromArgb((int)(165 * a), 231, 233, 238)))
                Fx.Text(g, name, cap, sub, new RectangleF(card.X, card.Y + 56f, card.Width, 15f), mid);

            float barW = card.Width - 24f, barY = card.Bottom - 10f;
            using (var track = Round(new RectangleF(card.X + 12f, barY, barW, 3f), 1.5f))
            using (var tb = new SolidBrush(Color.FromArgb((int)(36 * a), 255, 255, 255)))
                g.FillPath(tb, track);
            float filled = Math.Max(3f, barW * Math.Clamp(gauge.Frac, 0f, 1f));
            using (var bar = Round(new RectangleF(card.X + 12f, barY, filled, 3f), 1.5f))
            using (var fb = new SolidBrush(Color.FromArgb((int)(240 * a), tone)))
                g.FillPath(fb, bar);
        }

        int at = state.HotGauge;
        if (at < 0 || at >= gauges.Count || gauges[at].Detail.Length == 0) return;
        using var rf = new Font("Segoe UI", 12f, GraphicsUnit.Pixel);
        using var rb = new SolidBrush(Color.FromArgb((int)(205 * a), 231, 233, 238));
        Fx.Text(g, Cap(gauges[at].Detail), rf, rb, new RectangleF(band.X, band.Y + BandPadTop + CardH + 5f, band.Width, 16f), mid);
    }

    private static void DrawWhen(Graphics g, RectangleF r, LauncherState state, float a)
    {
        using (var path = Round(r, 14f))
        {
            using var fill = new SolidBrush(Color.FromArgb((int)(16 * a), 255, 255, 255));
            g.FillPath(fill, path);
        }
        if (state.EffectiveWhen is not { } when) return;
        var now = DateTimeOffset.Now;
        var local = when.ToLocalTime();
        string day = local.Date == now.Date ? "Today"
                   : local.Date == now.Date.AddDays(1) ? "Tomorrow"
                   : local.ToString("ddd d MMM", System.Globalization.CultureInfo.InvariantCulture);
        var left = when - now;
        string away = left <= TimeSpan.Zero ? "now"
                    : left < TimeSpan.FromHours(1) ? $"in {Math.Max(1, (int)left.TotalMinutes)}m"
                    : left < TimeSpan.FromDays(1) ? $"in {(int)left.TotalHours}h {left.Minutes}m"
                    : $"in {(int)left.TotalDays}d {left.Hours}h";

        using var bell = new Font("Segoe Fluent Icons", 22f, GraphicsUnit.Pixel);
        using (var tint = new SolidBrush(Color.FromArgb((int)(240 * a), Tone.Left)))
            Fx.GlyphCentred(g, new RectangleF(r.X + 14f, r.Y + (r.Height - 30f) / 2f, 30f, 30f), "\uEA8F", bell, tint);

        using var time = new Font("Segoe UI Semibold", 24f, GraphicsUnit.Pixel);
        using var sub = new Font("Segoe UI", 12.5f, GraphicsUnit.Pixel);
        using var ink = new SolidBrush(Color.FromArgb((int)(248 * a), 240, 242, 247));
        using var dim = new SolidBrush(Color.FromArgb((int)(170 * a), 231, 233, 238));
        using var line = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        Fx.Text(g, local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), time, ink, new RectangleF(r.X + 54f, r.Y + 6f, 100f, 34f), line);
        Fx.Text(g, day, sub, dim, new RectangleF(r.X + 55f, r.Y + 39f, 200f, 17f), line);
        using var far = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        Fx.Text(g, away, sub, dim, new RectangleF(r.X, r.Y, r.Width - 16f, r.Height), far);
    }

    private static void DrawFormCell(Graphics g, RectangleF r, LauncherRow row, LauncherState state, float a)
    {
        string id = row.Id ?? "";
        bool chip = id.StartsWith(LauncherPages.WhenPrefix, StringComparison.Ordinal);
        bool picked = chip && long.TryParse(id[LauncherPages.WhenPrefix.Length..], out long unix)
                      && state.EffectiveWhen?.ToUnixTimeSeconds() == unix;
        bool delete = id.StartsWith(ReminderStore.ActPrefix, StringComparison.Ordinal);

        using (var path = Round(r, chip ? r.Height / 2f : 11f))
        {
            var (left, _) = Tone;
            using var fill = new SolidBrush(picked ? Color.FromArgb((int)(70 * a), left) : Color.FromArgb((int)(14 * a), 255, 255, 255));
            g.FillPath(fill, path);
        }

        var ink = delete ? Color.FromArgb(244, 128, 128) : Color.FromArgb(236, 238, 243);
        using var brush = new SolidBrush(Color.FromArgb((int)((row.Enabled ? 240 : 110) * a), ink));
        using var font = new Font(chip ? "Segoe UI" : "Segoe UI Semibold", chip ? 13f : 14f, GraphicsUnit.Pixel);
        using var mid = new StringFormat(StringFormat.GenericTypographic)
        { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
        if (chip) { Fx.Text(g, row.Label, font, brush, r, mid); return; }

        float textW = g.MeasureString(row.Label, font, int.MaxValue, mid).Width;
        float glyphX = r.X + (r.Width - textW - 24f) / 2f;
        using var gf = new Font("Segoe Fluent Icons", 15f, GraphicsUnit.Pixel);
        Fx.GlyphCentred(g, new RectangleF(glyphX, r.Y + (r.Height - 18f) / 2f, 18f, 18f), row.Glyph ?? "", gf, brush);
        Fx.Text(g, row.Label, font, brush, new RectangleF(glyphX + 24f, r.Y, textW + 4f, r.Height),
                new StringFormat(mid) { Alignment = StringAlignment.Near });
    }

    private static void DrawField(Graphics g, int w, LauncherState state, float a, bool caret)
    {
        var r = new RectangleF(Pad, Pad, w - Pad * 2, FieldH - 4);
        if (state.FieldIsTitle)
        {
            using var title = new Font("Segoe UI Semibold", 15f, GraphicsUnit.Pixel);
            using var ink2 = new SolidBrush(Color.FromArgb((int)(245 * a), 240, 242, 247));
            using var line = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center };
            Fx.Text(g, "System Info", title, ink2, new RectangleF(TextAxis, r.Y, r.Right - TextAxis, r.Height), line);
            return;
        }
        using (var path = Round(r, 10f))
        {
            using var fill = new SolidBrush(Color.FromArgb((int)(16 * a), 255, 255, 255));
            g.FillPath(fill, path);
            using var edge = new Pen(Color.FromArgb((int)(26 * a), 255, 255, 255), 1f);
            g.DrawPath(edge, path);
        }

        if (state.ShowClearAll)
        {
            var c = LauncherLayout.ClearCell;
            using (var path = Round(c, c.Height / 2f))
            using (var fill = new SolidBrush(state.ClearArmed ? Color.FromArgb((int)(190 * a), 196, 62, 62)
                                             : Color.FromArgb((int)((state.ClearHot ? 40 : 22) * a), 255, 255, 255)))
                g.FillPath(fill, path);
            using var cf = new Font("Segoe UI", 12f, GraphicsUnit.Pixel);
            using var cb = new SolidBrush(Color.FromArgb((int)(235 * a), 236, 238, 243));
            using var cm = new StringFormat(StringFormat.GenericTypographic)
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            Fx.Text(g, state.ClearArmed ? "Sure?" : "Clear all", cf, cb, c, cm);
        }

        float cy = r.Y + r.Height / 2f;
        bool onPage = state.Rows.Count > 0 && state.Rows[0].Kind == LauncherRowKind.Back;
        if (!onPage) Magnifier(g, IconAxis - 2f, cy - 1.5f, 6f, Color.FromArgb((int)(190 * a), 150, 156, 168));

        float textX = TextAxis;
        using var font = new Font("Segoe UI", 15f, GraphicsUnit.Pixel);
        using var fmt = new StringFormat(StringFormat.GenericTypographic)
        { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

        string shown = state.Query;
        bool empty = shown.Length == 0;
        using var brush = new SolidBrush(empty
            ? Color.FromArgb((int)(190 * a), 176, 182, 196)
            : Color.FromArgb((int)(250 * a), 240, 242, 247));

        bool rtl = !empty && Fx.IsRtl(Fx.CleanText(shown));
        if (rtl) fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        if (shown.Contains('\\')) fmt.Trimming = StringTrimming.EllipsisPath;
        var layout = new RectangleF(textX, r.Y, r.Right - textX - 14f, r.Height);
        Fx.Text(g, empty ? state.Placeholder : Fx.CleanText(shown), font, brush, layout, fmt);

        float typedW = empty ? 0f : g.MeasureString(Fx.CleanText(shown), font,
            new PointF(0, 0), StringFormat.GenericTypographic).Width;

        string ghost = state.Completion;
        if (ghost.Length > 0 && !empty && !rtl)
        {
            float ghostX = textX + typedW + 3f;
            using var faded = new SolidBrush(Color.FromArgb((int)(120 * a), 180, 186, 198));
            var tail = new RectangleF(ghostX, r.Y, Math.Max(0f, r.Right - ghostX - 14f), r.Height);
            if (tail.Width > 4f) Fx.Text(g, Fx.CleanText(ghost), font, faded, tail, fmt);
        }

        if (!caret) return;

        float caretX = empty ? textX - 4f : rtl ? Math.Max(layout.X, layout.Right - typedW - 4f) : Math.Min(layout.Right, textX + typedW + 1.5f);
        using var bar = new SolidBrush(Color.FromArgb((int)(235 * a), Tone.Left));
        g.FillRectangle(bar, caretX, cy - 8f, 1.5f, 16f);
    }

    private static void Magnifier(Graphics g, float cx, float cy, float radius, Color c)
    {
        using var pen = new Pen(c, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
        float d = radius * 0.72f;
        g.DrawLine(pen, cx + d, cy + d, cx + d + 4f, cy + d + 4f);
    }

    private static void DrawRow(Graphics g, RectangleF r, LauncherRow row, bool selected, float a,
                                Func<string, Image?>? icon, bool tickHot = false, float? highlightY = null)
    {

        if ((row.Id == ClipboardHistory.OpenVault || row.Id == LauncherState.PageClipPins) && row.Tint is { } group)
        {
            using var groupPath = Round(r, 9f);
            using var groupWash = new SolidBrush(Color.FromArgb((int)(24 * a), group));
            g.FillPath(groupWash, groupPath);
        }

        float lit = row.Kind switch
        {
            LauncherRowKind.Info => 1f,
            LauncherRowKind.Notice => 0.72f,
            _ => row.Enabled ? 1f : 0.45f,
        };
        var ink = Color.FromArgb((int)(245 * a * lit), 231, 233, 238);
        float cy = r.Y + r.Height / 2f;
        const float IconBox = 18f;
        float iconX = IconAxis - IconBox / 2f;

        string glyph = row.Glyph ?? MenuGlyph(row.Label);
        bool hasIcon = row.Kind is LauncherRowKind.App or LauncherRowKind.Tick || glyph.Length > 0;

        if (row.Kind == LauncherRowKind.App)
        {
            var box = new RectangleF(iconX, cy - IconBox / 2f, IconBox, IconBox);
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

            var box = new RectangleF(IconAxis - 8f, cy - 8f, 16f, 16f);
            Color ring = row.Tint is { } rt
                ? Color.FromArgb((int)(200 * a * lit), Blend(rt, 231, 233, 238, 0.15f))
                : ink;
            using (var pen = new Pen(Color.FromArgb((int)((tickHot ? 235 : 150) * a), ring), 1.5f))
                g.DrawEllipse(pen, box);
            if (tickHot)
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
            using var gf = new Font("Segoe Fluent Icons", 16f, GraphicsUnit.Pixel);
            Color gc = row.Tint is { } t
                ? Color.FromArgb((int)(238 * a * lit), Blend(t, 231, 233, 238, 0.28f))
                : ink;
            using var gb = new SolidBrush(gc);
            Fx.GlyphCentred(g, new RectangleF(iconX, cy - IconBox / 2f, IconBox, IconBox), glyph, gf, gb);
        }

        bool drills = row.Kind is LauncherRowKind.Inert or LauncherRowKind.Settings or LauncherRowKind.Page;
        string rowId = row.Id ?? "";
        bool inVault = rowId.StartsWith(ClipboardHistory.VaultPrefix, StringComparison.Ordinal);
        bool clipRow = inVault || rowId.StartsWith(ClipboardHistory.ActPrefix, StringComparison.Ordinal)
                       || rowId.StartsWith(ClipboardHistory.PinPrefix, StringComparison.Ordinal);
        float rightInset = drills ? 26f : inVault ? ClipOneButton : clipRow ? ClipThreeButtons : 14f;

        bool onlyWhenOn = (drills && !rowId.Equals(LauncherState.PageClipPins, StringComparison.Ordinal)) || clipRow;

        bool arrived = selected && (highlightY is not { } hy || Math.Abs(hy - r.Y) < 3f);
        string detail = row.Kind == LauncherRowKind.Info || (onlyWhenOn && !arrived) ? "" : Fx.CleanText(row.Detail ?? "");

        if (!IsVerbatim(row.Id)) detail = Cap(detail);
        float detailW = 0f;
        if (detail.Length > 0)
        {
            using var df = new Font("Segoe UI", 12f, GraphicsUnit.Pixel);
            detailW = Math.Min(g.MeasureString(detail, df).Width + 10f, r.Width * 0.5f);
        }

        float textX = TextAxis;
        float textRight = r.Right - rightInset - detailW;

        using var font = new Font("Segoe UI", 14f, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(ink);
        string label = Fx.CleanText(row.Label);
        if (row.Kind == LauncherRowKind.Notice) label = Cap(label);
        using var fmt = new StringFormat(StringFormat.GenericTypographic)
        {
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter,
        };

        if (Fx.IsRtl(label)) fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        if (row.Kind == LauncherRowKind.Info)
        {

            const float KeyW = 88f;
            using var key = new Font("Segoe UI", 12.5f, GraphicsUnit.Pixel);
            using var keyInk = new SolidBrush(Color.FromArgb((int)(150 * a), 231, 233, 238));
            float keyX = textX;
            Fx.Text(g, label, key, keyInk, new RectangleF(keyX, r.Y, KeyW, r.Height), fmt);
            using var value = new Font("Segoe UI", 13.5f, GraphicsUnit.Pixel);
            Fx.Text(g, Fx.CleanText(row.Detail ?? ""), value, brush,
                    new RectangleF(keyX + KeyW, r.Y, r.Right - rightInset - keyX - KeyW, r.Height), fmt);
        }
        else Fx.Text(g, label, font, brush, new RectangleF(textX, r.Y, textRight - textX, r.Height), fmt);

        if (detail.Length > 0)
        {
            using var df = new Font("Segoe UI", 12f, GraphicsUnit.Pixel);

            int dAlpha = row.Kind == LauncherRowKind.Info ? 225 : 165;
            using var db = new SolidBrush(Color.FromArgb((int)(dAlpha * a * lit), 231, 233, 238));

            bool chipped = row.Kind is LauncherRowKind.Action or LauncherRowKind.Tick && detail.Length <= 10;
            using var dfmt = new StringFormat(StringFormat.GenericTypographic)
            {
                LineAlignment = StringAlignment.Center,
                Alignment = chipped ? StringAlignment.Center : StringAlignment.Far,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter,
            };
            float dRight = r.Right - rightInset;

            float labelEnd = textX + g.MeasureString(label, font, int.MaxValue, fmt).Width;
            float pulled = row.Id == LauncherPages.ActCopyTranslation && !Fx.IsRtl(label) ? Math.Min(dRight, labelEnd + 16f + detailW) : dRight;
            var dRect = new RectangleF(pulled - detailW, r.Y, detailW, r.Height);

            if (chipped)
            {
                var chip = new RectangleF(dRect.X + 2f, cy - 9f, dRect.Width - 4f, 18f);
                using var chipFill = new SolidBrush(Color.FromArgb((int)(30 * a), 255, 255, 255));
                using var chipPath = Round(chip, 9f);
                g.FillPath(chipFill, chipPath);
            }
            Fx.Text(g, detail, df, db, dRect, dfmt);
        }

        if (drills)
            Chevron(g, r.Right - 18f, cy, Color.FromArgb((int)(120 * a * lit), 231, 233, 238));

        if (clipRow && (arrived || row.Pin || row.KeepPinIcon))
        {
            using var bf = new Font("Segoe Fluent Icons", 14f, GraphicsUnit.Pixel);
            void Button(float right, string g2, Color c)
            {
                using var b = new SolidBrush(c);
                Fx.GlyphCentred(g, new RectangleF(r.Right - right, cy - 9f, 18f, 18f), g2, bf, b);
            }
            var quiet = Color.FromArgb((int)(190 * a), 231, 233, 238);
            if (inVault) Button(31f, "\uE74D", quiet);
            else
            {
                Button(31f, row.Pin ? "\uE840" : "\uE718",
                       row.Pin ? Color.FromArgb((int)(240 * a), Fx.VitalBattery) : quiet);
                if (arrived)
                {
                    Button(57f, "\uE72E", quiet);
                    Button(83f, "\uE74D", quiet);
                }
            }
        }

        if (row.Dots > 0)
        {

            int n = Math.Min(row.Dots, 3);
            float dotX = textX + g.MeasureString(label, font, int.MaxValue, fmt).Width + 9f;
            using var dot = new SolidBrush(Color.FromArgb((int)(235 * a), row.Tint ?? Fx.VitalBattery));
            for (int i = 0; i < n; i++) g.FillEllipse(dot, dotX + i * 8f, cy - 2.5f, 5f, 5f);
        }
    }

    internal const float ClipOneButton = 40f, ClipLockEdge = 66f, ClipThreeButtons = 92f;

    internal static string Cap(string s)
        => s.Length > 0 && char.IsLower(s[0]) ? char.ToUpperInvariant(s[0]) + s[1..] : s;

    private static bool IsVerbatim(string? id)
        => id is not null && (id.StartsWith(QuickActions.CustomPrefix, StringComparison.Ordinal)
                              || id.StartsWith(QuickActions.SavePrefix, StringComparison.Ordinal)
                              || id.StartsWith(LauncherExtras.FilePrefix, StringComparison.Ordinal)
                              || id.StartsWith(LauncherPages.LangPrefix, StringComparison.Ordinal));

    private static Color Blend(Color c, int r, int g, int b, float toward)
        => Color.FromArgb((int)(c.R + (r - c.R) * toward),
                          (int)(c.G + (g - c.G) * toward),
                          (int)(c.B + (b - c.B) * toward));

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
