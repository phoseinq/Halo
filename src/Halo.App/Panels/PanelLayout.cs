using System;
using System.Collections.Generic;
using System.Drawing;

namespace Halo.Panels;

internal static class PanelLayout
{

    internal const int Width = 560, SlotHeight = 220;
    internal const float Pad = 18f;
    internal const float TitleTop = 14f, TitleH = 24f, TitleGap = 4f;
    internal const float RowH = 36f, RowGap = 6f, RowRadius = 11f;
    internal const float BottomPad = 14f;

    internal const float ControlW = 250f;
    internal const float TrackH = 5f, ThumbD = 15f;
    internal const float ToggleW = 40f, ToggleH = 22f;
    internal const float MeterH = 7f;
    internal const float SegH = 24f;

    internal readonly record struct Slot(int Index, RectangleF Row, RectangleF Control);

    internal static float Height(PanelSpec spec)
        => TitleTop + TitleH + TitleGap
           + spec.Rows.Count * RowH + Math.Max(0, spec.Rows.Count - 1) * RowGap
           + BottomPad;

    internal const float CloseD = 22f;

    internal static RectangleF CloseRect(int w)
        => new(w - Pad - CloseD, TitleTop + (TitleH - CloseD) / 2f, CloseD, CloseD);

    internal static RectangleF TitleRect(int w)
        => new(Pad, TitleTop, Math.Max(20f, w - Pad * 2f - CloseD - 10f), TitleH);

    internal static IReadOnlyList<Slot> Slots(PanelSpec spec, int w)
    {
        var slots = new List<Slot>(spec.Rows.Count);
        float y = TitleTop + TitleH + TitleGap;
        for (int i = 0; i < spec.Rows.Count; i++)
        {
            var row = new RectangleF(Pad, y, w - Pad * 2f, RowH);

            float cw = Math.Min(ControlW, row.Width - 60f);
            var control = new RectangleF(row.Right - 14f - cw, y, cw, RowH);
            slots.Add(new Slot(i, row, control));
            y += RowH + RowGap;
        }
        return slots;
    }

    internal static RectangleF Track(RectangleF control)
        => new(control.X + ThumbD / 2f, control.Y + (RowH - TrackH) / 2f,
               Math.Max(1f, control.Width - ThumbD), TrackH);

    internal static float Fraction(PanelRow row)
        => row.Max > row.Min ? (float)((row.Value - row.Min) / (row.Max - row.Min)) : 0f;

    internal static double ValueAt(PanelRow row, RectangleF control, float x)
    {
        var track = Track(control);
        float t = track.Width <= 0 ? 0 : Math.Clamp((x - track.X) / track.Width, 0f, 1f);
        return row.Min + t * (row.Max - row.Min);
    }

    internal static PointF ThumbCentre(PanelRow row, RectangleF control)
    {
        var track = Track(control);
        return new PointF(track.X + track.Width * Fraction(row), track.Y + TrackH / 2f);
    }

    internal static RectangleF ToggleRect(RectangleF control)
        => new(control.Right - ToggleW, control.Y + (RowH - ToggleH) / 2f, ToggleW, ToggleH);

    internal static RectangleF MeterRect(RectangleF control)
        => new(control.X, control.Y + (RowH - MeterH) / 2f, control.Width, MeterH);

    internal static RectangleF SegmentRect(RectangleF control, int index, int count)
    {
        if (count <= 0) return RectangleF.Empty;
        float gap = 4f;
        float each = (control.Width - gap * (count - 1)) / count;
        return new RectangleF(control.X + (each + gap) * index,
                              control.Y + (RowH - SegH) / 2f, each, SegH);
    }

    internal static int SegmentAt(RectangleF control, int count, PointF p)
    {
        for (int i = 0; i < count; i++)
            if (SegmentRect(control, i, count).Contains(p)) return i;
        return -1;
    }
}
