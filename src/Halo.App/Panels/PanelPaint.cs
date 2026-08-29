using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Halo.Widgets;

namespace Halo.Panels;

internal static class PanelPaint
{
    private static readonly Color Ink = Color.FromArgb(244, 255, 255, 255);
    private static readonly Color Dim = Color.FromArgb(170, 255, 255, 255);
    private static readonly Color Accent = Color.FromArgb(255, 167, 139, 250);
    private static readonly Color Rail = Color.FromArgb(56, 255, 255, 255);
    private static readonly Color Vessel = Color.FromArgb(26, 255, 255, 255);
    private static readonly Color VesselEdge = Color.FromArgb(38, 255, 255, 255);

    private const float TitlePx = 19f, LabelPx = 15f, ValuePx = 13.5f, SegPx = 13f;

    internal static void Draw(Graphics g, int w, PanelSpec spec, float alpha, int hoverRow = -1,
        bool closeHover = false)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        int a = (int)(Math.Clamp(alpha, 0f, 1f) * 255);
        if (a <= 0) return;

        using var title = new Font("Segoe UI", TitlePx, FontStyle.Bold, GraphicsUnit.Pixel);
        using var label = new Font("Segoe UI", LabelPx, FontStyle.Regular, GraphicsUnit.Pixel);
        using var value = new Font("Segoe UI", ValuePx, FontStyle.Regular, GraphicsUnit.Pixel);
        using var segment = new Font("Segoe UI", SegPx, FontStyle.Regular, GraphicsUnit.Pixel);

        if (spec.Title.Length > 0)
        {
            using var brush = new SolidBrush(Fade(Ink, a));
            using var fmt = Format(spec.Title);
            g.DrawString(spec.Title, title, brush, PanelLayout.TitleRect(w), fmt);
        }

        Close(g, w, a, closeHover);

        var slots = PanelLayout.Slots(spec, w);
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var row = spec.Rows[i];

            using (var vessel = Fx.Rounded(slot.Row, PanelLayout.RowRadius))
            {
                using var fill = new SolidBrush(Fade(i == hoverRow ? Lift(Vessel) : Vessel, a));
                g.FillPath(fill, vessel);
                using var pen = new Pen(Fade(VesselEdge, a), 1f);
                g.DrawPath(pen, vessel);
            }

            if (row.Label.Length > 0)
            {
                using var brush = new SolidBrush(Fade(Ink, a));
                using var fmt = Format(row.Label);
                var box = new RectangleF(slot.Row.X + 14f, slot.Row.Y,
                    Math.Max(10f, slot.Control.Left - slot.Row.X - 24f), PanelLayout.RowH);
                g.DrawString(row.Label, label, brush, box, fmt);
            }

            switch (row.Kind)
            {
                case PanelRowKind.Text: Text(g, slot, row, value, a); break;
                case PanelRowKind.Slider: Slider(g, slot, row, value, a); break;
                case PanelRowKind.Toggle: Toggle(g, slot, row, a); break;
                case PanelRowKind.Buttons: Buttons(g, slot, row, segment, a); break;
                case PanelRowKind.Meter: Meter(g, slot, row, a); break;
            }
        }
    }

    private static void Close(Graphics g, int w, int a, bool hover)
    {
        var box = PanelLayout.CloseRect(w);
        if (hover)
        {
            using var disc = new SolidBrush(Fade(Color.FromArgb(34, 255, 255, 255), a));
            g.FillEllipse(disc, box);
        }
        float inset = box.Width * 0.32f;
        using var pen = new Pen(Fade(hover ? Ink : Dim, a), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(pen, box.Left + inset, box.Top + inset, box.Right - inset, box.Bottom - inset);
        g.DrawLine(pen, box.Right - inset, box.Top + inset, box.Left + inset, box.Bottom - inset);
    }

    private static void Text(Graphics g, PanelLayout.Slot slot, PanelRow row, Font font, int a)
    {
        if (row.Text.Length == 0) return;
        using var brush = new SolidBrush(Fade(Dim, a));
        using var fmt = Format(row.Text, right: true);
        g.DrawString(row.Text, font, brush, slot.Control, fmt);
    }

    private static void Slider(Graphics g, PanelLayout.Slot slot, PanelRow row, Font font, int a)
    {
        var track = PanelLayout.Track(slot.Control);
        var thumb = PanelLayout.ThumbCentre(row, slot.Control);

        using (var rail = Fx.Rounded(track, PanelLayout.TrackH / 2f))
        using (var brush = new SolidBrush(Fade(Rail, a)))
            g.FillPath(brush, rail);

        float filled = Math.Max(0f, thumb.X - track.X);
        if (filled > 0.5f)
        {
            using var done = Fx.Rounded(new RectangleF(track.X, track.Y, filled, track.Height),
                PanelLayout.TrackH / 2f);
            using var brush = new SolidBrush(Fade(Accent, a));
            g.FillPath(brush, done);
        }

        float d = PanelLayout.ThumbD;
        using (var brush = new SolidBrush(Fade(Color.White, a)))
            g.FillEllipse(brush, thumb.X - d / 2f, thumb.Y - d / 2f, d, d);

        string text = Figure(row);
        if (text.Length == 0) return;
        using var ink = new SolidBrush(Fade(Dim, a));
        using var fmt = Format(text, right: true);
        g.DrawString(text, font, ink,
            new RectangleF(slot.Control.X, slot.Control.Y + 2f, slot.Control.Width, 16f), fmt);
    }

    private static void Toggle(Graphics g, PanelLayout.Slot slot, PanelRow row, int a)
    {
        var box = PanelLayout.ToggleRect(slot.Control);
        bool on = row.Value >= 0.5;
        using (var shell = Fx.Rounded(box, box.Height / 2f))
        {
            using var brush = new SolidBrush(Fade(on ? Accent : Rail, a));
            g.FillPath(brush, shell);
        }
        float d = box.Height - 6f;
        float x = on ? box.Right - d - 3f : box.X + 3f;
        using var knob = new SolidBrush(Fade(Color.White, a));
        g.FillEllipse(knob, x, box.Y + 3f, d, d);
    }

    private static void Buttons(Graphics g, PanelLayout.Slot slot, PanelRow row, Font font, int a)
    {
        int count = row.Options.Count;
        int picked = (int)Math.Round(row.Value);
        for (int i = 0; i < count; i++)
        {
            var cell = PanelLayout.SegmentRect(slot.Control, i, count);
            bool on = i == picked;
            using (var shape = Fx.Rounded(cell, 9f))
            {
                using var brush = new SolidBrush(Fade(on ? Accent : Rail, a));
                g.FillPath(brush, shape);
            }
            using var ink = new SolidBrush(Fade(on ? Color.FromArgb(255, 22, 8, 42) : Ink, a));
            using var fmt = Format(row.Options[i], centre: true);
            g.DrawString(row.Options[i], font, ink, cell, fmt);
        }
    }

    private static void Meter(Graphics g, PanelLayout.Slot slot, PanelRow row, int a)
    {
        var box = PanelLayout.MeterRect(slot.Control);
        using (var rail = Fx.Rounded(box, box.Height / 2f))
        using (var brush = new SolidBrush(Fade(Rail, a)))
            g.FillPath(brush, rail);

        float filled = box.Width * (float)Math.Clamp(row.Value, 0, 1);
        if (filled <= 0.5f) return;
        using var done = Fx.Rounded(new RectangleF(box.X, box.Y, filled, box.Height), box.Height / 2f);
        using var fill = new SolidBrush(Fade(Accent, a));
        g.FillPath(fill, done);
    }

    private static string Figure(PanelRow row)
    {
        double v = row.Value;
        string n = Math.Abs(v - Math.Round(v)) < 0.005
            ? Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return n + row.Unit;
    }

    private static StringFormat Format(string text, bool right = false, bool centre = false)
    {
        var fmt = new StringFormat(StringFormatFlags.NoWrap)
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            Alignment = centre ? StringAlignment.Center : right ? StringAlignment.Far : StringAlignment.Near,
        };

        if (Fx.IsRtl(text))
        {
            fmt.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            if (!centre) fmt.Alignment = right ? StringAlignment.Far : StringAlignment.Near;
        }
        return fmt;
    }

    private static Color Fade(Color c, int a) => Color.FromArgb(c.A * a / 255, c.R, c.G, c.B);
    private static Color Lift(Color c) => Color.FromArgb(Math.Min(255, c.A + 18), c.R, c.G, c.B);
}
