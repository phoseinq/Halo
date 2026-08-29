using System;
using System.Collections.Generic;
using System.Drawing;

namespace Halo.Panels;

internal static class PanelHit
{

    internal readonly record struct Target(int Row, string Id, PanelRowKind Kind, RectangleF Area);

    internal static IReadOnlyList<Target> Targets(PanelSpec spec, int w)
    {
        var targets = new List<Target>();
        var slots = PanelLayout.Slots(spec, w);
        for (int i = 0; i < slots.Count; i++)
        {
            var row = spec.Rows[i];
            if (row.Kind is PanelRowKind.Text or PanelRowKind.Meter) continue;
            var area = row.Kind switch
            {
                PanelRowKind.Toggle => PanelLayout.ToggleRect(slots[i].Control),
                _ => slots[i].Control,
            };
            targets.Add(new Target(i, row.Id, row.Kind, area));
        }
        return targets;
    }

    internal static (int Row, string Id, double Value)? Press(
        PanelSpec spec, int w, PointF p, bool dragging = false, int heldRow = -1)
    {
        var slots = PanelLayout.Slots(spec, w);

        if (dragging && heldRow >= 0 && heldRow < spec.Rows.Count
            && spec.Rows[heldRow].Kind == PanelRowKind.Slider)
        {
            var held = spec.Rows[heldRow];
            return (heldRow, held.Id, PanelLayout.ValueAt(held, slots[heldRow].Control, p.X));
        }
        if (dragging) return null;

        foreach (var target in Targets(spec, w))
        {
            if (!target.Area.Contains(p)) continue;
            var row = spec.Rows[target.Row];
            switch (row.Kind)
            {
                case PanelRowKind.Slider:
                    return (target.Row, row.Id, PanelLayout.ValueAt(row, slots[target.Row].Control, p.X));
                case PanelRowKind.Toggle:
                    return (target.Row, row.Id, row.Value >= 0.5 ? 0 : 1);
                case PanelRowKind.Buttons:
                    int seg = PanelLayout.SegmentAt(slots[target.Row].Control, row.Options.Count, p);

                    return seg < 0 ? null : (target.Row, row.Id, seg);
            }
        }
        return null;
    }

    internal static int RowAt(PanelSpec spec, int w, PointF p)
    {
        var slots = PanelLayout.Slots(spec, w);
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].Row.Contains(p)) return i;
        return -1;
    }

    internal static PanelSpec With(PanelSpec spec, int row, double value)
    {
        if (row < 0 || row >= spec.Rows.Count) return spec;
        var rows = new List<PanelRow>(spec.Rows);
        var old = rows[row];
        double clamped = old.Kind switch
        {
            PanelRowKind.Toggle => value >= 0.5 ? 1 : 0,
            PanelRowKind.Buttons => Math.Clamp(value, 0, Math.Max(0, old.Options.Count - 1)),
            PanelRowKind.Meter => Math.Clamp(value, 0, 1),
            _ => Math.Clamp(value, old.Min, old.Max),
        };
        rows[row] = old with { Value = clamped };
        return spec with { Rows = rows };
    }
}
