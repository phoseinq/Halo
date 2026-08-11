using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace Halo.Widgets;

internal sealed class NetWidget : IWidget
{
    private readonly NetMeter _meter;
    private EasedBar _wash;

    internal NetWidget(NetMeter meter) => _meter = meter;

        internal void Settle()
    {

        float target = RingFrac(_meter.DownRate);
        _edgeLevel = target;
        _edgeAt = Environment.TickCount64;
        for (int i = 0; i < 240; i++) _wash.Step(target);
    }

    private const int GlyphDown = 0xE896, GlyphUp = 0xE898, GlyphWifi = 0xEC3F, GlyphLan = 0xE839;

    private const double LoadCeiling = 50.0 * 1024 * 1024;
    private const double LoadKnee = 200.0 * 1024;

    internal static float WashFrac(double bytesPerSec)
    {
        if (bytesPerSec <= 0) return 0f;
        double t = Math.Log(1.0 + bytesPerSec / LoadKnee) / Math.Log(1.0 + LoadCeiling / LoadKnee);
        return (float)Math.Clamp(t, 0.0, 1.0);
    }

    internal static float RingFrac(double bytesPerSec) => WashFrac(bytesPerSec);

    internal static int PulsePeriodMs(float frac)
        => (int)(2600 + (900 - 2600) * Math.Clamp(frac, 0f, 1f));

        internal static bool DownLeads(bool leading, double down, double up)
    {

        if (leading && up > down * 1.15) return false;
        if (!leading && down > up * 1.15) return true;
        return leading;
    }

    public string Icon => ((char)GlyphWifi).ToString();

    public Bitmap? IconImage => Mark(Lan, strip: true);

    private static readonly bool AlwaysOn =
        Environment.GetEnvironmentVariable("HALO_NET_ALWAYS") == "1";

    private static readonly string? ForcedLink =
        Environment.GetEnvironmentVariable("HALO_NET_LINK")?.Trim().ToLowerInvariant();

    private bool Lan => ForcedLink switch
    {
        "wifi" => false,
        "lan" => true,
        _ => _meter.Link == NetLink.Lan,
    };

    public bool IsActive => AlwaysOn || _meter.Busy;

    public int Version => (int)(_meter.DownRate / 1024) ^ ((int)(_meter.UpRate / 1024) << 16);

    public bool Animating => AlwaysOn || _meter.Busy;

    public IReadOnlyList<(RectangleF rect, Action<PointF> onClick)> Buttons(int w, int h)
        => Array.Empty<(RectangleF, Action<PointF>)>();

    private static readonly Color Green = Color.FromArgb(255, 92, 214, 130);

    private static readonly Color White = Color.FromArgb(255, 255, 255, 255);
    private static readonly Color Dim = Color.FromArgb(150, 255, 255, 255);

    private static Color Mul(Color c, float a)
        => Color.FromArgb((int)Math.Clamp(c.A * a, 0, 255), c.R, c.G, c.B);

    private const float EdgeWeight = 1.8f;

    private float _edgeLevel;
    private long _edgeAt;
    private bool _downLeads = true;

    public void DrawCollapsed(Graphics g, int w, int h, float fade)
    {
        if (fade <= 0.01f) return;
        double down = _meter.DownRate, up = _meter.UpRate;
        long nowMs = Environment.TickCount64;

        float frac = _wash.Step(RingFrac(down));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        int period = PulsePeriodMs(frac);
        float pulse = 0.5f - 0.5f * MathF.Cos(nowMs % period / (float)period * MathF.Tau);

        long edgeDt = _edgeAt == 0 ? 16 : Math.Clamp(nowMs - _edgeAt, 1, 250);
        _edgeAt = nowMs;
        _edgeLevel += (frac - _edgeLevel) * (1f - MathF.Exp(-edgeDt / 1600f));

        DrawEdge(g, w, h, _edgeLevel, pulse, fade);

        float cx = w / 2f, cy = h / 2f;

        DrawMark(g, cx, cy, Lan, fade);

        _downLeads = DownLeads(_downLeads, down, up);
        using var rateF = new Font("Segoe UI Semibold", 13f, GraphicsUnit.Pixel);
        DrawRate(g, NetRate.Format(down), rateF, cx - 23f, cy, fade, rising: false, alignRight: true, lead: _downLeads);
        DrawRate(g, NetRate.Format(up), rateF, cx + 23f, cy, fade, rising: true, alignRight: false, lead: !_downLeads);
    }

    private static void DrawRate(Graphics g, string text, Font f, float edgeX, float cy, float fade,
                                 bool rising, bool alignRight, bool lead)
    {
        const float mark = 11f, gap = 4f;
        using var markF = new Font("Segoe Fluent Icons", mark, GraphicsUnit.Pixel);
        string glyph = ((char)(rising ? GlyphUp : GlyphDown)).ToString();
        var size = g.MeasureString(text, f, int.MaxValue, StringFormat.GenericTypographic);
        var markSz = g.MeasureString(glyph, markF, int.MaxValue, StringFormat.GenericTypographic);

        var ink = Mul(lead ? White : Dim, fade);
        float x = alignRight ? edgeX - (markSz.Width + gap + size.Width) : edgeX;
        using var b = new SolidBrush(ink);

        using var mid = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center };
        g.DrawString(glyph, markF, b, new RectangleF(x, cy - markSz.Height, markSz.Width + 1f, markSz.Height * 2), mid);
        g.DrawString(text, f, b, new RectangleF(x + markSz.Width + gap, cy - size.Height, size.Width + 1f, size.Height * 2), mid);
    }

    private static void DrawEdge(Graphics g, int w, int h, float frac, float pulse, float fade)
    {

        var lit = Color.FromArgb(
            (int)(236 + 19 * frac),
            (int)(38 + (24 - 38) * frac),
            (int)(50 + (116 - 50) * frac),
            (int)(58 + (112 - 58) * frac));

        float i = EdgeWeight / 2f + 0.3f, r = Math.Min(h / 2f, 30f) - i;
        float l = i, t = 0f, b = h - i, rt = w - i, d = r * 2f;
        using var path = new GraphicsPath();
        path.AddLine(l, t, l, b - r);
        path.AddArc(l, b - d, d, d, 180f, -90f);
        path.AddLine(l + r, b, rt - r, b);
        path.AddArc(rt - d, b - d, d, d, 90f, -90f);
        path.AddLine(rt, b - r, rt, t);
        using var pen = new Pen(Mul(lit, fade * (0.9f + 0.1f * pulse)), EdgeWeight)
        { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawPath(pen, path);
    }

    private static void DrawArrowText(Graphics g, float x, float y, int glyph, string text,
                                      Font f, Brush b, float arrowPx)
    {
        using var af = new Font("Segoe Fluent Icons", arrowPx, GraphicsUnit.Pixel);

        g.DrawString(((char)glyph).ToString(), af, b, x, y + (f.Size - arrowPx) * 0.5f + 1f);
        g.DrawString(text, f, b, x + arrowPx + 6f, y);
    }

    private static Bitmap BuildMark(bool lan, int size, float fill)
    {
        const int ss = 4;
        int big = size * ss;
        var hi = new Bitmap(big, big, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(hi))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            using var f = new Font("Segoe Fluent Icons", big * 0.5f, GraphicsUnit.Pixel);
            using var b = new SolidBrush(Color.FromArgb(224, 255, 255, 255));
            using var sf = new StringFormat(StringFormat.GenericTypographic)
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(((char)(lan ? GlyphLan : GlyphWifi)).ToString(), f, b,
                         new RectangleF(0, 0, big, big), sf);
        }

        var ink = InkBounds(hi);
        var small = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(small))
        {

            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float side = Math.Max(ink.Width, ink.Height) / Math.Clamp(fill, 0.1f, 1f);
            var src = new RectangleF(ink.X + ink.Width / 2f - side / 2f, ink.Y + ink.Height / 2f - side / 2f,
                                     side, side);
            g.DrawImage(hi, new Rectangle(0, 0, size, size), src.X, src.Y, src.Width, src.Height,
                        GraphicsUnit.Pixel);
        }
        hi.Dispose();
        return small;
    }

    private static Rectangle InkBounds(Bitmap b)
    {
        var data = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly,
                              PixelFormat.Format32bppPArgb);
        int minX = b.Width, minY = b.Height, maxX = -1, maxY = -1;
        try
        {
            unsafe
            {
                for (int y = 0; y < b.Height; y++)
                {
                    byte* row = (byte*)data.Scan0 + y * data.Stride;
                    for (int x = 0; x < b.Width; x++)
                        if (row[x * 4 + 3] > 8)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                }
            }
        }
        finally { b.UnlockBits(data); }

        if (maxX < minX) return new Rectangle(0, 0, b.Width, b.Height);
        return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }

    private static Bitmap? _wifiMark, _lanMark, _wifiIcon, _lanIcon;

    private const float RowFill = 0.68f, StripFill = 0.56f;

    private static Bitmap Mark(bool lan, bool strip)
    {
        if (strip)
            return lan ? _lanIcon ??= BuildMark(true, 32, StripFill)
                       : _wifiIcon ??= BuildMark(false, 32, StripFill);
        return lan ? _lanMark ??= BuildMark(true, 21, RowFill)
                   : _wifiMark ??= BuildMark(false, 21, RowFill);
    }

    private static void DrawMark(Graphics g, float cx, float cy, bool lan, float fade)
    {
        var m = Mark(lan, strip: false);
        using var att = new ImageAttributes();
        var cm = new ColorMatrix { Matrix33 = Math.Clamp(fade, 0f, 1f) };
        att.SetColorMatrix(cm);
        var box = new Rectangle((int)MathF.Round(cx - m.Width / 2f), (int)MathF.Round(cy - m.Height / 2f),
                                m.Width, m.Height);
        g.DrawImage(m, box, 0, 0, m.Width, m.Height, GraphicsUnit.Pixel, att);
    }

    private static void DrawGlyph(Graphics g, string glyph, float cx, float cy, float px, Color c)
    {
        using var f = new Font("Segoe Fluent Icons", px, GraphicsUnit.Pixel);
        using var b = new SolidBrush(c);
        using var sf = new StringFormat(StringFormat.GenericTypographic)
        { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(glyph, f, b, new RectangleF(cx - px, cy - px, px * 2, px * 2), sf);
    }

        internal static float[] ChartBars(IReadOnlyList<(DateOnly Day, long Down, long Up)> series)
    {
        var bars = new float[series.Count];
        long peak = Peak(series);

        if (peak <= 0) return bars;
        for (int i = 0; i < series.Count; i++)
            bars[i] = (float)((series[i].Down + series[i].Up) / (double)peak);
        return bars;
    }

        internal static long Peak(IReadOnlyList<(DateOnly Day, long Down, long Up)> series)
    {
        long peak = 0;
        foreach (var d in series) { long t = d.Down + d.Up; if (t > peak) peak = t; }
        return peak;
    }

    private const int ChartDays = 14;
    private readonly EasedBar[] _bars = new EasedBar[ChartDays];

    public void DrawContent(Graphics g, int w, int h, float fade)
    {
        if (fade <= 0.01f) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var led = _meter.Ledger;

        using var bigF = new Font("Segoe UI Semibold", 22f, GraphicsUnit.Pixel);
        using var labelF = new Font("Segoe UI", 11f, GraphicsUnit.Pixel);
        using var valF = new Font("Segoe UI Semibold", 15f, GraphicsUnit.Pixel);
        using var subF = new Font("Segoe UI", 13f, GraphicsUnit.Pixel);
        using var tinyF = new Font("Segoe UI", 11.5f, GraphicsUnit.Pixel);
        using var ink = new SolidBrush(Mul(White, fade));
        using var dim = new SolidBrush(Mul(Dim, fade));
        using var green = new SolidBrush(Mul(Green, fade * 0.85f));

        g.DrawString(NetRate.Format(_meter.DownRate), bigF, ink, 24f, 18f);
        g.DrawString(NetRate.Format(_meter.UpRate), bigF, dim, 210f, 18f);
        string over = Halo.Localization.Strings.Get(
            Lan ? "net.overLan" : "net.overWifi");
        using (var far = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Far })
            g.DrawString(over, subF, dim, new RectangleF(300f, 26f, 236f, 20f), far);

        (string Key, (long Down, long Up) V)[] cols =
        [
            ("net.today", led.Today(today)),
            ("net.week", led.Week(today)),
            ("net.month", led.Month(today)),
        ];
        for (int i = 0; i < cols.Length; i++)
        {
            float cx = 24f + i * 176f;
            g.DrawString(Halo.Localization.Strings.Get(cols[i].Key), labelF, dim, cx, 70f);
            DrawArrowText(g, cx, 88f, GlyphDown, NetRate.Size(cols[i].V.Down), valF, ink, 12f);
            DrawArrowText(g, cx, 110f, GlyphUp, NetRate.Size(cols[i].V.Up), subF, dim, 11f);
        }

        (string Key, (long Down, long Up) V)[] links =
        [
            ("net.wifi", led.Month(today, NetLink.Wifi)),
            ("net.lan", led.Month(today, NetLink.Lan)),
        ];
        for (int i = 0; i < links.Length; i++)
        {
            float ly = 150f + i * 18f;
            g.DrawString(Halo.Localization.Strings.Get(links[i].Key), tinyF, dim, 366f, ly);
            DrawArrowText(g, 400f, ly, GlyphDown, NetRate.Size(links[i].V.Down), tinyF, dim, 10f);
            DrawArrowText(g, 470f, ly, GlyphUp, NetRate.Size(links[i].V.Up), tinyF, dim, 10f);
        }

        var series = led.Series(today, ChartDays);
        var bars = ChartBars(series);
        float baseY = h - 22f, top = 148f, span = Math.Max(12f, baseY - top);
        for (int i = 0; i < bars.Length && i < _bars.Length; i++)
        {
            float shown = _bars[i].Step(bars[i]);
            if (shown <= 0.001f) continue;
            float bw = 14f, bx = 24f + i * 20f, bh = Math.Max(2f, span * shown);
            using (var p = Fx.Rounded(new RectangleF(bx, baseY - bh, bw, bh), 2f))
                g.FillPath(green, p);

            long total = series[i].Down + series[i].Up;
            if (total > 0 && series[i].Up > 0)
            {
                float cap = Math.Max(1.5f, bh * (series[i].Up / (float)total));
                using var cp = Fx.Rounded(new RectangleF(bx, baseY - bh, bw, cap), 2f);
                using var cb = new SolidBrush(Mul(White, fade * 0.55f));
                g.FillPath(cb, cp);
            }
        }
        using (var line = new Pen(Mul(Color.FromArgb(46, 255, 255, 255), fade), 1f))
            g.DrawLine(line, 24f, baseY + 1f, 24f + ChartDays * 20f - 6f, baseY + 1f);

        long peak = Peak(series);
        string caption = peak <= 0
            ? Halo.Localization.Strings.Get("net.quiet")
            : $"{Halo.Localization.Strings.Get("net.days")} - "
              + Halo.Localization.Strings.Format("net.peak", NetRate.Size(peak));
        g.DrawString(caption, labelF, dim, 24f, baseY + 4f);
    }
}
