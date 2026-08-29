using System;
using System.Drawing;

namespace Halo.Widgets;

internal enum NetWindow { Hour, Today, Week, Month, Quarter }

internal readonly record struct NetColumn(

    float RatesY, float TotalY, float ShareY,
    float TraceTop, float TraceH,

    float BandRow1, float BandRow2, float BandRow3,
    float MarkCy, float FootY);

internal static class NetPanelLayout
{

    internal const float Pad = 28f;

    internal const float ColRight = 224f;
    internal const float ChartLeft = 252f;

    internal const float RateSplit = 104f;
    internal const float TrackRight = 532f;
    internal const float ChipRight = 528f;
    internal const float ChipTop = 16f;
    internal const float ChipH = 20f;
    internal const float MinSpan = 12f;

    internal static float BaseY(float panelH) => panelH - 16f;

    internal const float SparkHeight = 44f;
    internal const float OpenHeight = 128f;

    internal static (float Top, float BaseY) ChartBand(float panelH, float open)
    {
        float baseY = BaseY(panelH);
        float height = SparkHeight + (OpenHeight - SparkHeight) * Math.Clamp(open, 0f, 1f);
        return (baseY - Math.Max(MinSpan, height), baseY);
    }

    internal static RectangleF ChartZone(float panelW, float panelH)
    {
        float right = Math.Min(TrackRight, panelW - Pad);
        var band = ChartBand(panelH, 0f);
        return new RectangleF(ChartLeft - 6f, band.Top - 34f, right - ChartLeft + 12f,
                              band.BaseY - band.Top + 44f);
    }

    internal static NetColumn Column(float panelH) => new(
        RatesY: 32f, TotalY: 54f, ShareY: 76f,
        TraceTop: 88f, TraceH: 34f,
        BandRow1: 82f, BandRow2: 104f, BandRow3: 126f,

        MarkCy: Math.Max(120f, panelH - 50f), FootY: Math.Max(140f, panelH - 30f));

    internal static RectangleF RatesZone(float panelH)
    {
        var col = Column(panelH);
        return new RectangleF(Pad - 8f, 8f, ColRight - Pad + 16f, col.BandRow3 + 18f - 8f);
    }

        internal static RectangleF LinkZone(float panelH)
    {
        var col = Column(panelH);
        return new RectangleF(Pad - 8f, col.MarkCy - 18f, ColRight - Pad + 16f, 36f);
    }

    internal static int Span(NetWindow window) => window switch
    {
        NetWindow.Hour => 60,
        NetWindow.Week => 7,
        NetWindow.Month => 30,
        NetWindow.Quarter => 90,
        _ => 24,
    };

    internal static int WindowDays(NetWindow window) => window switch
    {
        NetWindow.Week => 7,
        NetWindow.Month => 30,
        NetWindow.Quarter => 90,
        _ => 1,
    };

        internal static string UnitKey(NetWindow window) => window switch
    {
        NetWindow.Hour => "net.avgMinute",
        NetWindow.Today => "net.avgHour",
        _ => "net.avg",
    };

    private static readonly float[] ChipWidths = [46f, 46f, 52f, 60f, 60f];
    private const float ChipGap = 6f;

    internal static RectangleF[] Chips(float panelW)
    {
        var chips = new RectangleF[ChipWidths.Length];
        float right = Math.Min(ChipRight, panelW - 8f);
        for (int i = chips.Length - 1; i >= 0; i--)
        {
            chips[i] = new RectangleF(right - ChipWidths[i], ChipTop, ChipWidths[i], ChipH);
            right -= ChipWidths[i] + ChipGap;
        }
        return chips;
    }

    internal static RectangleF[] Bars(float panelW, float top, float baseY, int count, float[] heights)
    {
        var bars = new RectangleF[Math.Max(0, count)];
        if (bars.Length == 0) return bars;
        float right = Math.Min(TrackRight, panelW - Pad);
        float left = Math.Min(ChartLeft, right - 1f);
        float pitch = Math.Max(1f, (right - left) / bars.Length);
        float barW = Math.Clamp(pitch * FillFraction(bars.Length), 3f, 34f);
        float span = Math.Max(MinSpan, baseY - top);
        for (int i = 0; i < bars.Length; i++)
        {
            float h = i < heights.Length ? Math.Max(0f, span * heights[i]) : 0f;
            float x = left + i * pitch + (pitch - barW) / 2f;
            bars[i] = new RectangleF(x, baseY - h, barW, h);
        }
        return bars;
    }

    internal static float FillFraction(int count)
    {
        const float few = 7f, many = 90f, fat = 0.82f, thin = 0.54f;
        float t = Math.Clamp((count - few) / (many - few), 0f, 1f);
        return fat + (thin - fat) * t;
    }

        internal static int HoverDay(PointF p, RectangleF[] bars, float top, float baseY)
    {
        if (bars.Length == 0) return -1;

        if (p.Y < top - 10f || p.Y > baseY + 10f) return -1;

        float left = float.MaxValue, right = float.MinValue;
        foreach (var bar in bars)
        {
            if (bar.Left < left) left = bar.Left;
            if (bar.Right > right) right = bar.Right;
        }
        if (p.X < left - 4f || p.X > right + 4f) return -1;
        int best = -1;
        float bestDx = float.MaxValue;
        for (int i = 0; i < bars.Length; i++)
        {
            float dx = Math.Abs(p.X - (bars[i].Left + bars[i].Width / 2f));
            if (dx < bestDx) { bestDx = dx; best = i; }
        }
        return best;
    }

        internal static int LitBar(NetWindow window, int count)
        => window == NetWindow.Today && count > 0 ? count - 1 : -1;

    internal static NetWindow Scroll(NetWindow from, int notches)
    {
        int index = Math.Clamp((int)from - Math.Sign(notches), 0, ChipWidths.Length - 1);
        return (NetWindow)index;
    }
}
