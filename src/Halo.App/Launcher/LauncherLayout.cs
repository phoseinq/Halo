using System;
using System.Drawing;

namespace Halo.Launcher;

internal static class LauncherLayout
{
    internal const int BackD = 28;

    internal readonly record struct Frame(RectangleF[] Cells, float Height, RectangleF Band);

        internal static RectangleF BackCell
    {
        get
        {
            const int Pad = LauncherView.Pad;
            float inField = (LauncherView.FieldH - 4 - BackD) / 2f;
            return new RectangleF(LauncherView.IconAxis - BackD / 2f, Pad + inField, BackD, BackD);
        }
    }

    internal const int WhenH = 64, ChipH = 34, ButtonH = 38;

        internal static Frame ComputeForm(int rows)
    {
        const int Pad = LauncherView.Pad;
        var cells = new RectangleF[rows];
        cells[0] = BackCell;
        float grid = LauncherView.W - Pad * 2f, y = Pad + LauncherView.FieldH + Pad / 2f;
        var when = new RectangleF(Pad, y, grid, WhenH);
        y += WhenH + 8f;

        const float Gap = 6f;
        int chips = rows - 3;
        float chipW = (grid - Gap * (chips - 1)) / Math.Max(1, chips);
        for (int i = 0; i < chips; i++) cells[1 + i] = new RectangleF(Pad + i * (chipW + Gap), y, chipW, ChipH);
        y += ChipH + 10f;

        float buttonW = (grid - 8f) / 2f;
        cells[rows - 2] = new RectangleF(Pad, y, buttonW, ButtonH);
        cells[rows - 1] = new RectangleF(Pad + buttonW + 8f, y, buttonW, ButtonH);
        return new Frame(cells, y + ButtonH + Pad, when);
    }

    internal static bool IsForm(LauncherState s)
        => s.Page == LauncherState.PageReminderEdit && s.Rows.Count >= 4 && s.Rows[^1].Id == LauncherPages.ActSaveReminder;

        internal static RectangleF ClearCell
    {
        get
        {
            const float W2 = 76f, H2 = 26f;
            float y = LauncherView.Pad + (LauncherView.FieldH - 4 - H2) / 2f;
            return new RectangleF(LauncherView.W - LauncherView.Pad - 7f - W2, y, W2, H2);
        }
    }

    internal static Frame Of(LauncherState s)
    {
        if (IsForm(s)) return ComputeForm(s.Rows.Count);
        bool back = s.Rows.Count > 0 && s.Rows[0].Kind == LauncherRowKind.Back;
        var f = Compute(s.Rows.Count, back, LauncherView.BandHeight(s));
        int split = GroupStart(s);
        if (split < 0) return f;

        for (int i = split; i < f.Cells.Length; i++) f.Cells[i].Y += GroupGap;
        return f with { Height = f.Height + GroupGap };
    }

    internal const float GroupGap = 9f;

        internal static int GroupStart(LauncherState s)
    {
        if (s.Page != LauncherState.PageClipboard) return -1;
        for (int i = 2; i < s.Rows.Count; i++)
            if (s.Rows[i].Id == LauncherState.PageClipPins || s.Rows[i].Id == ClipboardHistory.OpenVault) return i;
        return -1;
    }

    internal static Frame Compute(int rows, bool hasBack, float bandH)
    {
        const int Pad = LauncherView.Pad, W = LauncherView.W, FieldH = LauncherView.FieldH, RowH = LauncherView.RowH;

        var cells = new RectangleF[Math.Max(0, rows)];
        int first = hasBack ? 1 : 0;
        if (hasBack && rows > 0) cells[0] = BackCell;
        float y = Pad + FieldH + Pad / 2f, grid = W - Pad * 2f;
        var band = bandH > 0f ? new RectangleF(Pad, y, grid, bandH) : RectangleF.Empty;
        y += bandH;
        for (int i = first; i < rows; i++, y += RowH)
            cells[i] = new RectangleF(Pad, y, grid, RowH);
        float listed = rows - first;
        return new Frame(cells, Pad + FieldH + bandH + (listed > 0 ? Pad / 2f + listed * RowH : 0f) + Pad, band);
    }

    internal static int Hit(Frame f, float x, float y)
    {
        for (int i = 0; i < f.Cells.Length; i++)
            if (!f.Cells[i].IsEmpty && f.Cells[i].Contains(x, y)) return i;
        return -1;
    }

        internal static int HitBand(Frame f, float x, float y, int count)
    {
        if (count <= 0 || f.Band.IsEmpty || !f.Band.Contains(x, y)) return -1;
        int i = (int)((x - f.Band.X) / (f.Band.Width / count));
        return i >= 0 && i < count ? i : -1;
    }
}
