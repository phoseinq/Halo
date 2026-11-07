using System;
using System.Drawing;

namespace Halo.Widgets;

internal readonly record struct HistoryShift(int OpenRow, float OpenExtra, int GapAt, float GapH)
{
    internal static HistoryShift None => new(-1, 0f, -1, 0f);

        internal float Total => (OpenRow >= 0 ? OpenExtra : 0f) + (GapAt >= 0 ? GapH : 0f);

    internal float Before(int index) =>
        (OpenRow >= 0 && index > OpenRow ? OpenExtra : 0f) + (GapAt >= 0 && index >= GapAt ? GapH : 0f);

        internal float ExtraAt(int index) => index == OpenRow ? OpenExtra : 0f;
}

internal static class HistoryLayout
{

    internal const int BaseW = 510;
    internal static int W => BaseW + Grow;

    internal const int MaxGrow = 300, GrowPerLine = 90;
    internal static int Grow { get; private set; }
    internal const int Visible = 5;

        internal static int GistLines => 1 + Grow / GrowPerLine;

    internal static void SetSize(int grow, int visible = Visible) => Grow = Math.Clamp(grow, 0, MaxGrow);

    private static readonly string SizeFile = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "history-size.txt");

    internal static void LoadSize()
    {
        try
        {
            var parts = System.IO.File.ReadAllText(SizeFile).Split(' ');
            if (parts.Length == 2 && int.TryParse(parts[0], out var g) && int.TryParse(parts[1], out var v))
                SetSize(g, v);
        }
        catch { }
    }

    internal static void SaveSize()
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SizeFile)!);
            System.IO.File.WriteAllText(SizeFile, Grow + " " + Visible);
        }
        catch { }
    }

    internal const float BaseCardW = 450f, BaseCardH = 76f, CardGap = 6f, CardR = 20f, GistLineH = 18f;
    internal static float CardW => BaseCardW + Grow;
    internal static float CardH => BaseCardH + (GistLines - 1) * GistLineH;
    internal static float CardX => (W - CardW) / 2f;
    internal static float RowH => CardH + CardGap;

    internal const float PillH = 40f, HeaderH = 10f, PadBottom = 12f;
    internal static float BodyTop => PillH + HeaderH;

    internal const float DeckH = 36f;

    internal const float OpenExtra = 300f;

    internal const float IconPad = 14f, IconD = 36f;
    internal static float TextX => CardX + IconPad + IconD + 12f;

        internal const float GistY = 50f;

    internal static float MaxH => BodyTop + ExpandedRows * RowH + PadBottom;
    private const int ExpandedRows = Visible + 1;

        internal static int Fits => (int)((MaxH - BodyTop - PadBottom) / RowH);

        internal static RectangleF Card(float rowTop, float extra = 0f) =>
        new(CardX, rowTop, CardW, CardH + Math.Max(0f, extra));

    internal static float Height(int count, float extra = 0f)
    {
        if (count <= 0) return BodyTop + RowH + PadBottom;
        return BodyTop + Math.Min(count, Visible) * RowH + DeckFor(count) + extra + PadBottom;
    }

    internal static float Height(int count, float extra, bool deckOpen) =>
        deckOpen && count > 0
            ? Math.Min(MaxH, BodyTop + count * RowH + extra + PadBottom)
            : Height(count, extra);

    internal static float DeckFor(int count) => count > Visible ? DeckH : 0f;

    internal static int FirstVisible(float scroll) => Math.Max(0, (int)MathF.Floor(scroll / RowH + 0.001f));

        internal static float ScrollFrac(float scroll) =>
        Math.Clamp(scroll / RowH - FirstVisible(scroll), 0f, 1f);

    internal const int MaxBack = 2;

    internal static (float Top, float Inset) Pose(float q)
    {
        float front = Visible - 1;
        if (q <= front) return (BodyTop + q * RowH, 0f);
        float k = Math.Min(q - front, MaxBack);
        return (BodyTop + front * RowH + k * PeekH, k * PeekInset);
    }

    internal static float BandTop(float q) => Pose(Math.Max(q - 1f, 0f)).Top + CardH;

        internal static int Hidden(int count, float scroll = 0f) =>
        Math.Max(0, count - Visible - FirstVisible(scroll));

    private const float RevealLead = 0.075f, RevealMaxStart = 0.30f;

        internal static float Reveal(int index, float t) =>
        Math.Clamp((t - Math.Min(index * RevealLead, RevealMaxStart)) / (1f - RevealMaxStart), 0f, 1f);

    internal const float RevealRise = 22f;

    internal static float RevealTop(float finalTop, float p) =>
        finalTop - (1f - Math.Clamp(p, 0f, 1f)) * RevealRise;

    internal static RectangleF ClearAll() => new(CardX + CardW - 130f, (PillH - 26f) / 2f, 130f, 26f);

    internal static RectangleF MuteAll() => new(CardX, (PillH - 26f) / 2f, 34f, 26f);

        internal static RectangleF MuteApp(float rowTop)
    {
        var x = Dismiss(rowTop);
        return new RectangleF(x.X - 30f, x.Y, 26f, 26f);
    }

    internal static float SwipeOut => CardX + CardW;

    internal const float PeekInset = 10f, PeekH = 10f;

    internal static RectangleF Deck(int count, float scroll, HistoryShift s)
    {
        if (count <= Visible) return RectangleF.Empty;
        float top = BodyTop + Visible * RowH - CardGap + s.Total + 2f;
        return new RectangleF(CardX, top, CardW, DeckH);
    }

    internal static RectangleF DeckPeek(int count, float scroll, HistoryShift s, int layer)
    {
        var d = Deck(count, scroll, s);
        if (d.IsEmpty) return RectangleF.Empty;
        float inset = PeekInset * (layer + 1);
        return new RectangleF(d.X + inset, d.Y + layer * (PeekH - 1f), d.Width - inset * 2f, PeekH);
    }

        internal static float RowTop(int index, float scroll) => RowTop(index, scroll, HistoryShift.None);

    internal static float RowTop(int index, float scroll, HistoryShift s) =>
        BodyTop + index * RowH - scroll + s.Before(index);

        internal static float LeavingTop(float scroll, HistoryShift s) =>
        s.GapAt < 0 ? 0f : RowTop(s.GapAt, scroll, s) - s.GapH;

        internal static float MaxScroll(int count, float extra = 0f) =>
        Math.Max(0f, count * RowH + extra - (MaxH - BodyTop - PadBottom));

    internal static float MaxScroll(int count, float extra, bool deckOpen) =>
        deckOpen ? MaxScroll(count, extra) : Math.Max(0f, (count - Visible) * RowH + extra);

    internal static int RowAt(float y, int count, float scroll) => RowAt(y, count, scroll, HistoryShift.None);

    internal static int RowAt(float y, int count, float scroll, HistoryShift s) =>
        RowAt(y, count, scroll, s, false);

    internal static int RowAt(float y, int count, float scroll, HistoryShift s, bool deckOpen)
    {
        if (y < BodyTop || y > Height(count, s.Total, deckOpen) - PadBottom) return -1;

        int first = deckOpen ? 0 : FirstVisible(scroll);
        int last = deckOpen ? count : Math.Min(count, first + Visible);
        for (int i = first; i < last; i++)
        {
            float top = RowTop(i, scroll, s);
            if (y >= top && y < top + CardH + s.ExtraAt(i)) return i;
        }
        return -1;
    }

    internal static RectangleF Dismiss(float rowTop)
    {
        var c = Card(rowTop);
        return new RectangleF(c.Right - IconPad - 26f, c.Y + 6f, 26f, 26f);
    }

    internal const float BodyPad = 12f;

    internal static RectangleF Body(float rowTop, float extra)
    {
        var c = Card(rowTop, extra);
        float y = c.Y + GistY;
        return new RectangleF(TextX, y, c.Right - IconPad - TextX, c.Bottom - BodyPad - y);
    }

        internal static float ExtraFor(float textH) =>
        Math.Clamp(textH + GistY + BodyPad - CardH, 24f, OpenExtra);
}
