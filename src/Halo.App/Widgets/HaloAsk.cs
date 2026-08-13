using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Halo.Widgets;

internal static class HaloAsk
{
    internal const int W = 430;
    private const float Pad = 22f;
    private const float TitlePx = 16f, TitleLineH = 22f;
    private const float BodyPx = 12.5f, BodyLineH = 17f;
    private const float TitleGap = 8f, ChipsGap = 18f;
    private const float ChipH = 38f, ChipGap = 10f, ChipPx = 14f;
    private const int TitleMaxLines = 2, BodyMaxLines = 3;

    private static readonly Color Ink = Color.FromArgb(240, 255, 255, 255);
    private static readonly Color Dim = Color.FromArgb(168, 255, 255, 255);

    private static readonly Color Green = Color.FromArgb(62, 207, 92);

    internal readonly record struct Chip(RectangleF Rect, string Label, bool Affirm);

    internal static IReadOnlyList<Chip> Chips(int w, int h, string yes, string no)
    {
        float top = h - Pad - ChipH;
        float half = (w - Pad * 2f - ChipGap) / 2f;
        return
        [
            new Chip(new RectangleF(Pad, top, half, ChipH), yes, true),
            new Chip(new RectangleF(Pad + half + ChipGap, top, half, ChipH), no, false),
        ];
    }

    private static Graphics? _measure;

    private static int Lines(string? text, Font font, float width, int max)
    {
        if (string.IsNullOrWhiteSpace(text) || width <= 1f) return 1;
        try
        {
            _measure ??= Graphics.FromImage(new Bitmap(1, 1, PixelFormat.Format32bppPArgb));
            _measure.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var sf = Wrap();
            _measure.MeasureString(text, font, new SizeF(width, 4000f), sf, out _, out int lines);
            return Math.Clamp(lines, 1, max);
        }
        catch { return 1; }
    }

    internal static int Height(string title, string body)
    {
        float inner = W - Pad * 2f;
        using var tf = new Font("Segoe UI Semibold", TitlePx, GraphicsUnit.Pixel);
        using var bf = new Font("Segoe UI", BodyPx, GraphicsUnit.Pixel);
        float y = Pad + Lines(title, tf, inner, TitleMaxLines) * TitleLineH;
        if (!string.IsNullOrWhiteSpace(body))
            y += TitleGap + Lines(body, bf, inner, BodyMaxLines) * BodyLineH;
        return (int)MathF.Ceiling(y + ChipsGap + ChipH + Pad);
    }

    private static StringFormat Wrap() => new(StringFormat.GenericTypographic)
    {
        Alignment = StringAlignment.Near,
        LineAlignment = StringAlignment.Near,
        FormatFlags = 0,
        Trimming = StringTrimming.EllipsisCharacter,
    };

    internal static void Draw(Graphics g, int w, int h, string title, string body,
        string yes, string no, int hover, float fade)
    {
        if (fade <= 0.004f) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        float inner = w - Pad * 2f;
        using var sf = Wrap();
        using (var tf = new Font("Segoe UI Semibold", TitlePx, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(Mul(Ink, fade)))
        {
            int n = Lines(title, tf, inner, TitleMaxLines);
            Fx.Text(g, Fx.CleanText(title), tf, brush, new RectangleF(Pad, Pad, inner, n * TitleLineH + 3f), sf);
            if (!string.IsNullOrWhiteSpace(body))
            {
                using var bf = new Font("Segoe UI", BodyPx, GraphicsUnit.Pixel);
                using var db = new SolidBrush(Mul(Dim, fade));
                float by = Pad + n * TitleLineH + TitleGap;
                int bn = Lines(body, bf, inner, BodyMaxLines);
                Fx.Text(g, Fx.CleanText(body), bf, db, new RectangleF(Pad, by, inner, bn * BodyLineH + 3f), sf);
            }
        }

        var chips = Chips(w, h, yes, no);
        for (int i = 0; i < chips.Count; i++) DrawChip(g, chips[i], i == hover, fade);
    }

    private static void DrawChip(Graphics g, Chip chip, bool hover, float fade)
    {
        Color tone = chip.Affirm ? Green : Color.FromArgb(255, 255, 255, 255);
        float lift = hover ? 1f : 0f;

        using var path = Pill(chip.Rect);
        using (var fill = new SolidBrush(Mul(Color.FromArgb(chip.Affirm ? 30 : 18, tone), fade * (1f + lift * 0.7f))))
            g.FillPath(fill, path);
        using (var pen = new Pen(Mul(Color.FromArgb(chip.Affirm ? 150 : 92, tone), fade * (1f + lift * 0.35f)), 1.3f))
            g.DrawPath(pen, path);

        using var f = new Font("Segoe UI Semibold", ChipPx, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Mul(chip.Affirm
            ? Color.FromArgb(248, Lighten(Green)) : Color.FromArgb(hover ? 248 : 224, 255, 255, 255), fade));
        using var sf = new StringFormat(StringFormat.GenericTypographic)
        { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        Fx.Text(g, Fx.CleanText(chip.Label), f, brush, chip.Rect, sf);
    }

    private static Color Lighten(Color c) =>
        Color.FromArgb(Math.Min(255, c.R + 70), Math.Min(255, c.G + 30), Math.Min(255, c.B + 70));

    private static GraphicsPath Pill(RectangleF r)
    {
        float d = r.Height;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 90, 180);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        p.CloseFigure();
        return p;
    }

    private static Color Mul(Color c, float a) =>
        Color.FromArgb((int)Math.Clamp(c.A * a, 0f, 255f), c.R, c.G, c.B);
}
