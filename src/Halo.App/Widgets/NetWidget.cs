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

        _downLeads = DownLeads(_downLeads, _meter.DownRate, _meter.UpRate);
        float target = RingFrac(Louder(_meter.DownRate, _meter.UpRate, _downLeads));
        _edge.Seed(target, Environment.TickCount64);
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
        Environment.GetEnvironmentVariable("HALO_NET_ALWAYS") == "1" || PinFile;

    private static bool PinFile
    {
        get
        {
            try
            {
                return System.IO.File.Exists(System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Halo", "net-pin"));
            }
            catch { return false; }
        }
    }

    private static readonly string? ForcedLink =
        Environment.GetEnvironmentVariable("HALO_NET_LINK")?.Trim().ToLowerInvariant();

    private bool Lan => ForcedLink switch
    {
        "wifi" => false,
        "lan" => true,
        _ => _meter.Link == NetLink.Lan,
    };

    internal bool Pinned;

    public FaceProp ArrivingProp => FaceProp.Antenna;

    public bool IsActive => AlwaysOn || Pinned || _meter.Busy;

    public int Version => (int)(_meter.DownRate / 1024) ^ ((int)(_meter.UpRate / 1024) << 16)
                          ^ ((int)_window << 12) ^ (_split ? 0x8000 : 0);

    public bool Animating => AlwaysOn || Pinned || _meter.Busy;

    public bool WantsWheel => true;

    public void Wheel(int notches)
    {
        if (notches != 0) Window = NetPanelLayout.Scroll(_window, notches);
    }

    public IReadOnlyList<(RectangleF rect, Action<PointF> onClick)> Buttons(int w, int h)
    {
        var chips = NetPanelLayout.Chips(w);
        var list = new List<(RectangleF, Action<PointF>)>(chips.Length + 2);
        for (int i = 0; i < chips.Length; i++)
        {
            var window = (NetWindow)i;
            list.Add((chips[i], _ => Window = window));
        }
        list.Add((new RectangleF(_markCx - 14f, _markCy - 14f, 28f, 28f), _ => _split = !_split));

        float top = ChartTopFolded;
        float baseY = NetPanelLayout.BaseY(h);
        list.Add((new RectangleF(NetPanelLayout.ChartLeft - 4f, top, NetPanelLayout.TrackRight - NetPanelLayout.ChartLeft + 8f,
                                 baseY - top + 10f),
                  p => PinDay(w, h, top, baseY, p)));
        return list;
    }

    private void PinDay(int w, int h, float top, float baseY, PointF p)
    {
        var points = Points(DateOnly.FromDateTime(DateTime.Now), NetPanelLayout.Span(_window));

        var heights = new float[points.Length];
        for (int i = 0; i < points.Length && i < _bars.Length; i++) heights[i] = _bars[i].Shown;
        int hit = NetPanelLayout.HoverDay(p, NetPanelLayout.Bars(w, top, baseY, points.Length, heights),
                                         top, baseY);
        if (hit < 0 || hit >= points.Length || points[hit].Total <= 0) { _pinned = null; return; }
        _pinned = _pinned == points[hit].Label ? null : points[hit].Label;
    }

    private static readonly Color Green = Color.FromArgb(255, 92, 214, 130);

    private static readonly Color White = Color.FromArgb(255, 255, 255, 255);

    private static readonly Color Dim = Color.FromArgb(150, 255, 255, 255);

    private static Color Mul(Color c, float a)
        => Color.FromArgb((int)Math.Clamp(c.A * a, 0, 255), c.R, c.G, c.B);

    private const float EdgeWeight = 2.2f;

    private Drift _edge;
    private bool _downLeads = true;

    public void DrawCollapsed(Graphics g, int w, int h, float fade)
    {
        if (fade <= 0.01f) return;
        double down = _meter.DownRate, up = _meter.UpRate;
        long nowMs = Environment.TickCount64;

        _downLeads = DownLeads(_downLeads, down, up);
        float frac = _wash.Step(RingFrac(Louder(down, up, _downLeads)));
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        DrawEdge(g, w, h, StepEdge(frac, nowMs), Breath(frac, nowMs), fade);

        float cx = w / 2f, cy = h / 2f;

        DrawMark(g, cx, cy, Lan, fade);

        using var rateF = new Font("Segoe UI Semibold", 13f, GraphicsUnit.Pixel);
        DrawRate(g, NetRate.Format(down), rateF, cx - 23f, cy, fade, rising: false, alignRight: true,
                 lead: _downLeads, ink: FigureInk(down, _downLeads, rising: false));
        DrawRate(g, NetRate.Format(up), rateF, cx + 23f, cy, fade, rising: true, alignRight: false,
                 lead: !_downLeads, ink: FigureInk(up, !_downLeads, rising: true));
    }

    private static void DrawRate(Graphics g, string text, Font f, float edgeX, float cy, float fade,
                                 bool rising, bool alignRight, bool lead, float mark = 11f, float gap = 4f,
                                 Color? ink = null)
    {
        using var markF = new Font("Segoe Fluent Icons", mark, GraphicsUnit.Pixel);
        string glyph = ((char)(rising ? GlyphUp : GlyphDown)).ToString();
        var size = g.MeasureString(text, f, int.MaxValue, StringFormat.GenericTypographic);
        var markSz = g.MeasureString(glyph, markF, int.MaxValue, StringFormat.GenericTypographic);

        var tone = Mul(ink ?? White, fade * (lead ? 1f : 0.82f));
        float x = alignRight ? edgeX - (markSz.Width + gap + size.Width) : edgeX;
        using var b = new SolidBrush(tone);

        using var mid = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center };
        Fx.Text(g, glyph, markF, b, new RectangleF(x, cy - markSz.Height, markSz.Width + 1f, markSz.Height * 2), mid);
        Fx.Text(g, text, f, b, new RectangleF(x + markSz.Width + gap, cy - size.Height, size.Width + 1f, size.Height * 2), mid);
    }

    private static float Breath(float frac, long nowMs)
    {
        int period = PulsePeriodMs(frac);
        return 0.5f - 0.5f * MathF.Cos(nowMs % period / (float)period * MathF.Tau);
    }

    internal const float EdgeSeconds = 1.6f;

    private float StepEdge(float frac, long nowMs) => _edge.Step(frac, EdgeSeconds, nowMs);

    private static void DrawEdge(Graphics g, int w, int h, float frac, float pulse, float fade)
    {

        var hue = EdgeInk(frac);
        var lit = Color.FromArgb((int)(236 + 19 * frac), hue.R, hue.G, hue.B);
        Fx.PillRim(g, w, h, lit, EdgeWeight, fade * (0.9f + 0.1f * pulse));
    }

    private static void DrawArrowText(Graphics g, float x, float y, int glyph, string text,
                                      Font f, Brush b, float arrowPx)
    {
        using var af = new Font("Segoe Fluent Icons", arrowPx, GraphicsUnit.Pixel);

        Fx.Text(g, ((char)glyph).ToString(), af, b, x, y + (f.Size - arrowPx) * 0.5f + 1f);
        Fx.Text(g, text, f, b, x + arrowPx + 6f, y);
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
            Fx.Text(g, ((char)(lan ? GlyphLan : GlyphWifi)).ToString(), f, b,
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

    private static readonly Dictionary<(bool Lan, int Size), Bitmap> _sized = [];

    private static Bitmap MarkSized(bool lan, int size)
    {
        if (_sized.TryGetValue((lan, size), out var cached)) return cached;
        var built = BuildMark(lan, size, RowFill);
        _sized[(lan, size)] = built;
        return built;
    }

    private static void DrawMarkAt(Graphics g, float cx, float cy, bool lan, int size, float alpha)
    {
        var m = MarkSized(lan, size);
        using var att = new ImageAttributes();
        att.SetColorMatrix(new ColorMatrix { Matrix33 = Math.Clamp(alpha, 0f, 1f) });
        var box = new Rectangle((int)MathF.Round(cx - m.Width / 2f), (int)MathF.Round(cy - m.Height / 2f),
                                m.Width, m.Height);
        g.DrawImage(m, box, 0, 0, m.Width, m.Height, GraphicsUnit.Pixel, att);
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
        Fx.Text(g, glyph, f, b, new RectangleF(cx - px, cy - px, px * 2, px * 2), sf);
    }

    private readonly record struct Point(long Down, long Up, string Label, DateOnly? Day, DateTime? Hour,
                                         DateTime? Minute = null)
    {
        internal long Total => Down + Up;
    }

        private Point[] Points(DateOnly today, int count)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        if (_window == NetWindow.Hour)
        {
            var minutes = _meter.Minutes.Series(DateTime.Now, count);
            var live = new Point[minutes.Count];
            for (int i = 0; i < minutes.Count; i++)
                live[i] = new Point(minutes[i].Down, minutes[i].Up,
                                    minutes[i].Minute.ToString("HH:mm", culture), null, null,
                                    minutes[i].Minute);
            return live;
        }
        if (_window == NetWindow.Today)
        {
            var hours = _meter.Hours.Series(DateTime.Now, count);
            var points = new Point[hours.Count];
            for (int i = 0; i < hours.Count; i++)
                points[i] = new Point(hours[i].Down, hours[i].Up,
                                      hours[i].Hour.ToString("HH:mm", culture), null, hours[i].Hour);
            return points;
        }
        var series = _meter.Ledger.Series(today, count);
        var days = new Point[series.Count];
        for (int i = 0; i < series.Count; i++)
            days[i] = new Point(series[i].Down, series[i].Up,
                                series[i].Day.ToString("d MMM", culture), series[i].Day, null);
        return days;
    }

        private long LanBytesAt(in Point p)
    {
        if (p.Minute is { } minute)
        {
            var v = _meter.Minutes.Minute(minute, NetLink.Lan);
            return v.Down + v.Up;
        }
        if (p.Hour is { } hour)
        {
            var v = _meter.Hours.Hour(hour, NetLink.Lan);
            return v.Down + v.Up;
        }
        if (p.Day is { } day)
        {
            var v = _meter.Ledger.Total(day, day, NetLink.Lan);
            return v.Down + v.Up;
        }
        return 0;
    }

        internal static float[] ChartBars(IReadOnlyList<long> totals)
    {
        var bars = new float[totals.Count];
        long peak = 0;
        foreach (var t in totals) if (t > peak) peak = t;
        if (peak <= 0) return bars;
        for (int i = 0; i < totals.Count; i++) bars[i] = (float)(totals[i] / (double)peak);
        return bars;
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

    private const int ChartMaxDays = 90;
    private readonly EasedBar[] _bars = new EasedBar[ChartMaxDays];

    private const float ChartTopFolded = 46f;

    internal static Color RateInk(double bytesPerSec) => RateInkAt(WashFrac(bytesPerSec));

    internal static Color RateInkAt(float t)
    {
        t = Math.Clamp(t, 0f, 1f);

        var hue = t < 0.5f
            ? Lerp(Ramp.Calm, Ramp.Mid, t * 2f)
            : Lerp(Ramp.Mid, Ramp.Peak, (t - 0.5f) * 2f);

        float lit = LevelLit(t);
        return Color.FromArgb(255, (int)(hue.R * lit), (int)(hue.G * lit), (int)(hue.B * lit));
    }

        internal static float LevelLit(float t) => 0.58f + 0.42f * Math.Min(1f, Math.Clamp(t, 0f, 1f) / 0.45f);

    internal static Color EdgeInk(float frac)
    {
        frac = Math.Clamp(frac, 0f, 1f);
        var hue = RateInkAt(frac);
        var deep = Color.FromArgb(255, (int)(hue.R * 0.50f), (int)(hue.G * 0.50f), (int)(hue.B * 0.50f));
        return Lerp(EdgeSlate, deep, frac);
    }

    private static readonly Color EdgeSlate = Color.FromArgb(255, 38, 50, 58);

        internal static double Louder(double down, double up, bool downLeads) => downLeads ? down : up;

    internal static Color FigureInk(double rate, bool leads, bool rising)
        => leads ? RateInk(rate) : White;

    private static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return Color.FromArgb(255,
            (int)MathF.Round(a.R + (b.R - a.R) * t),
            (int)MathF.Round(a.G + (b.G - a.G) * t),
            (int)MathF.Round(a.B + (b.B - a.B) * t));
    }

    internal readonly record struct RampStyle(string Name, Color Calm, Color Mid, Color Peak);

    internal static readonly RampStyle[] Ramps =
    [
        new("1 now - cyan, green, orange",   Color.FromArgb(255,  52, 200, 232),
                                             Color.FromArgb(255,  96, 230, 128),
                                             Color.FromArgb(255, 255, 168,  52)),
        new("2 aurora - teal, violet, pink", Color.FromArgb(255,  46, 214, 208),
                                             Color.FromArgb(255, 150, 120, 255),
                                             Color.FromArgb(255, 255,  92, 176)),
        new("3 ember - steel, coral, red",   Color.FromArgb(255,  96, 148, 220),
                                             Color.FromArgb(255, 255, 150, 108),
                                             Color.FromArgb(255, 255,  70,  62)),
        new("4 neon - cyan, lime, hot pink", Color.FromArgb(255,   0, 224, 255),
                                             Color.FromArgb(255, 176, 255,  48),
                                             Color.FromArgb(255, 255,  40, 150)),
        new("5 sunset - sky, gold, deep",    Color.FromArgb(255,  92, 186, 255),
                                             Color.FromArgb(255, 255, 208,  74),
                                             Color.FromArgb(255, 255, 106,  40)),
        new("6 lagoon - ice, jade, amber",   Color.FromArgb(255, 148, 226, 255),
                                             Color.FromArgb(255,  40, 210, 168),
                                             Color.FromArgb(255, 255, 186,  64)),
    ];

    internal static RampStyle Ramp = Ramps[0];

    private static readonly Color CalmInk = Color.FromArgb(255, 52, 200, 232);

    private static readonly Color DownInk = Color.FromArgb(255, 96, 230, 128);

    private static readonly Color UpInk = Color.FromArgb(255, 172, 146, 255);
    private static readonly Color PeakInk = Color.FromArgb(255, 255, 168, 52);

    private NetWindow _window = NetWindow.Today;
    private bool _split;
    private float _splitT, _chartFade = 1f, _chipX, _chipW;

    private float _ratesT, _chartT, _linkT;
    private long _panelAt;

    private float _markCx = NetPanelLayout.Pad + 11f;
    private float _markCy = 178f;

    private string? _pinned;

    private bool _markHot;
    private const int HourSpan = 24;

    internal NetWindow Window
    {
        get => _window;
        set { if (_window != value) { _window = value; Swap(); } }
    }

    internal bool SplitOpen
    {
        get => _split;
        set { _split = value; }
    }

        internal void SettlePanel(int panelW = 560, int panelH = 220)
    {
        _chartFade = 1f;
        var chips = NetPanelLayout.Chips(560f);
        _chipX = chips[(int)_window].X;
        _chipW = chips[(int)_window].Width;

        bool over = WidgetInput.Over;
        _ratesT = over && NetPanelLayout.RatesZone(panelH).Contains(WidgetInput.Mouse) ? 1f : 0f;
        _chartT = over && NetPanelLayout.ChartZone(panelW, panelH).Contains(WidgetInput.Mouse) ? 1f : 0f;
        _markHot = over && NetPanelLayout.LinkZone(panelH).Contains(WidgetInput.Mouse);

        _splitT = _split || _markHot ? 1f : 0f;
        _linkT = _markHot || _split ? 1f : 0f;
    }

    private void Swap()
    {
        for (int i = 0; i < _bars.Length; i++) _bars[i] = default;
        _chartFade = 0f;
    }

    private static readonly string[] ChipKeys =
        ["net.hour", "net.today", "net.week", "net.month", "net.quarter"];
    private static readonly Color Track = Color.FromArgb(38, 255, 255, 255);

    public void DrawContent(Graphics g, int w, int h, float fade)
    {
        if (fade <= 0.01f) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        long nowMs = Environment.TickCount64;
        float dt = _panelAt == 0 ? 0.016f : Math.Clamp((nowMs - _panelAt) / 1000f, 0.001f, 0.25f);
        _panelAt = nowMs;

        _markHot = WidgetInput.Over && NetPanelLayout.LinkZone(h).Contains(WidgetInput.Mouse);
        _splitT = Math.Clamp(_splitT + (_split || _markHot ? 1 : -1) * dt / 0.20f, 0f, 1f);
        _chartFade = Math.Clamp(_chartFade + dt / 0.12f, 0f, 1f);

        bool overRates = WidgetInput.Over && NetPanelLayout.RatesZone(h).Contains(WidgetInput.Mouse);
        bool overChart = WidgetInput.Over && NetPanelLayout.ChartZone(w, h).Contains(WidgetInput.Mouse);

        _ratesT = Math.Clamp(_ratesT + (overRates ? 1 : -1) * dt / 0.16f, 0f, 1f);
        _chartT = Math.Clamp(_chartT + (overChart || _pinned != null ? 1 : -1) * dt / 0.16f, 0f, 1f);
        _linkT = Math.Clamp(_linkT + (_markHot || _split ? 1 : -1) * dt / 0.16f, 0f, 1f);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var led = _meter.Ledger;
        int count = NetPanelLayout.Span(_window);
        var points = Points(today, count);

        (long Down, long Up) Window(NetLink? link) => _window == NetWindow.Hour
            ? _meter.Minutes.Total(DateTime.Now, NetPanelLayout.Span(NetWindow.Hour), link)
            : led.Total(today.AddDays(-(NetPanelLayout.WindowDays(_window) - 1)), today, link);

        using var rateF = new Font("Segoe UI Semibold", 17f, GraphicsUnit.Pixel);
        using var subF = new Font("Segoe UI", 13f, GraphicsUnit.Pixel);

        using var tinyF = new Font("Segoe UI", 11f, GraphicsUnit.Pixel);

        using var colF = new Font("Segoe UI", 12.5f, GraphicsUnit.Pixel);

        DrawEdge(g, w, h, StepEdge(RingFrac(Louder(_meter.DownRate, _meter.UpRate, _downLeads)), nowMs),
                 Breath(RingFrac(_meter.DownRate), nowMs), fade);

        DrawChips(g, w, tinyF, fade, dt);

        var col = NetPanelLayout.Column(h);
        _downLeads = DownLeads(_downLeads, _meter.DownRate, _meter.UpRate);
        string downText = NetRate.Format(_meter.DownRate), upText = NetRate.Format(_meter.UpRate);

        DrawRate(g, downText, rateF, NetPanelLayout.Pad, col.RatesY, fade,
                 rising: false, alignRight: false, lead: _downLeads, mark: 12f, gap: 5f,
                 ink: FigureInk(_meter.DownRate, _downLeads, rising: false));
        DrawRate(g, upText, rateF, NetPanelLayout.Pad + NetPanelLayout.RateSplit, col.RatesY, fade,
                 rising: true, alignRight: false, lead: !_downLeads, mark: 12f, gap: 5f,
                 ink: FigureInk(_meter.UpRate, !_downLeads, rising: true));

        long peak = 0, sum = 0;
        var totals = new long[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            totals[i] = points[i].Total;
            if (totals[i] > peak) peak = totals[i];
            sum += totals[i];
        }
        var win = Window(null);
        using (var quiet = new SolidBrush(Mul(Dim, fade)))
            Fx.Text(g, peak <= 0
                             ? Halo.Localization.Strings.Get("net.quiet")
                             : Halo.Localization.Strings.Format("net.window", NetRate.Size(win.Down + win.Up),
                                                               Halo.Localization.Strings.Get(ChipKeys[(int)_window])),
                         colF, quiet, NetPanelLayout.Pad, col.TotalY);

        float linkBand = Math.Max(_linkT, _splitT);
        if (_ratesT > 0.01f) DrawLiveBand(g, col, win, subF, colF, fade * _ratesT);
        else if (linkBand > 0.01f) DrawSplit(g, Window, col, subF, fade * linkBand);
        else if (_chartT > 0.01f)
            DrawHistoryBand(g, col, points, totals, sum, today, win, colF, fade * _chartT);

        DrawUsual(g, col, today, colF, fade * (1f - Math.Clamp(Math.Max(_ratesT, linkBand), 0f, 1f)));

        var band = NetPanelLayout.ChartBand(h, _chartT);
        float top = band.Top, baseY = band.BaseY;
        var targets = ChartBars(totals);
        var heights = new float[targets.Length];
        for (int i = 0; i < targets.Length && i < _bars.Length; i++) heights[i] = _bars[i].Step(targets[i]);
        var bars = NetPanelLayout.Bars(w, top, baseY, targets.Length, heights);

        if (_chartT < 0.99f) DrawSpark(g, bars, points, fade * (1f - _chartT));
        if (_chartT > 0.01f)
        {
            DrawBars(g, bars, points, fade * _chartT);
            float right = Math.Min(NetPanelLayout.TrackRight, w - NetPanelLayout.Pad);
            using var line = new Pen(Mul(Color.FromArgb(46, 255, 255, 255), fade * _chartT), 1f);
            g.DrawLine(line, NetPanelLayout.ChartLeft, baseY + 1f, right, baseY + 1f);

            if (peak > 0 && totals.Length > 0)
            {
                float avgFrac = (float)(sum / (double)totals.Length / peak);
                float y = baseY - (baseY - top) * Math.Clamp(avgFrac, 0f, 1f);
                using var rule = new Pen(Mul(Color.FromArgb(150, 255, 255, 255), fade * _chartT * 0.5f), 1f)
                { DashStyle = DashStyle.Dash };
                g.DrawLine(rule, NetPanelLayout.ChartLeft, y, right, y);
            }
        }

        DrawLinkRow(g, col, colF, fade);
        DrawDayCard(g, w, h, bars, top, baseY, points, tinyF, fade * _chartT);
    }

    private bool UsualRowShown() => Learned() is not null || Learning() is not null;

    private void DrawUsual(Graphics g, NetColumn col, DateOnly today, Font tinyF, float fade)
    {

        if (fade <= 0.01f) return;
        if (Learned() is { } usual)
        {

            var todayBytes = _meter.Ledger.Today(today);
            bool heavy = usual.IsHeavy(todayBytes.Down + todayBytes.Up);
            using var ink = new SolidBrush(Mul(heavy ? PeakInk : Dim, fade));
            Fx.Text(g, Halo.Localization.Strings.Format(
                             heavy ? "net.overUsual" : "net.usual", NetRate.Size(usual.Typical)),
                         tinyF, ink, NetPanelLayout.Pad, col.BandRow2);
        }

        else if (Learning() is { } sofar)
        {
            using var dim = new SolidBrush(Mul(Dim, fade));
            Fx.Text(g, Halo.Localization.Strings.Format("net.learning",
                             sofar.Have.ToString(System.Globalization.CultureInfo.InvariantCulture),
                             sofar.Need.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                         tinyF, dim, NetPanelLayout.Pad, col.BandRow2);
        }
    }

    private void DrawLiveBand(Graphics g, NetColumn col, (long Down, long Up) win, Font subF, Font tinyF,
                              float fade)
    {
        DrawShare(g, col, win.Down, win.Up, fade);
        DrawTrace(g, col, fade);
        using var down = new SolidBrush(Mul(RateInk(_meter.DownRate), fade));
        using var up = new SolidBrush(Mul(UpInk, fade * 0.92f));
        DrawArrowText(g, NetPanelLayout.Pad, col.BandRow3, GlyphDown, NetRate.Size(win.Down), subF, down, 12f);
        DrawArrowText(g, NetPanelLayout.Pad + 96f, col.BandRow3, GlyphUp, NetRate.Size(win.Up), subF, up, 12f);
    }

    private void DrawHistoryBand(Graphics g, NetColumn col, Point[] points, long[] totals, long sum,
                                 DateOnly today, (long Down, long Up) win, Font tinyF, float fade)
    {

        using var dim = new SolidBrush(Mul(Dim, fade));

        if (Busiest(points) is { } busiest)
            using (var peakInk = new SolidBrush(Mul(PeakInk, fade * 0.92f)))
                Fx.Text(g, Halo.Localization.Strings.Format("net.busiest", busiest.Label,
                                                              NetRate.Size(busiest.Total)),
                             tinyF, peakInk, NetPanelLayout.Pad, col.BandRow1);

        if (!UsualRowShown())

            Fx.Text(g, Halo.Localization.Strings.Format(
                             NetPanelLayout.UnitKey(_window),
                             NetRate.Size(totals.Length > 0 ? sum / totals.Length : 0)),
                         tinyF, dim, NetPanelLayout.Pad, col.BandRow2);

        if (PreviousWindow(today) is not { } before || before <= 0) return;
        long now = win.Down + win.Up;
        int percent = (int)Math.Round((now - before) * 100.0 / before);
        using var deltaInk = new SolidBrush(Mul(percent >= 0 ? PeakInk : DownInk, fade));
        Fx.Text(g, Halo.Localization.Strings.Format(
                         _window == NetWindow.Today ? "net.vsYesterday" : "net.vsPrev",
                         (percent >= 0 ? "+" : "") + percent.ToString(
                             System.Globalization.CultureInfo.InvariantCulture) + "%"),
                     tinyF, deltaInk, NetPanelLayout.Pad, col.BandRow3);
    }

    private NetForecast.Usage? _usual;
    private DateOnly _usualDay;

    private int _usualDays;

    private void RefreshUsual()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (_usualDay == today) return;
        _usualDay = today;

        var days = new List<long>(NetForecast.LearnDays);
        if (_meter.Ledger.Oldest is { } oldest)
            foreach (var d in _meter.Ledger.Series(today, NetForecast.LearnDays + 1))
                if (d.Day >= oldest && d.Day < today) days.Add(d.Down + d.Up);
        _usualDays = days.Count;
        _usual = NetForecast.Learn(days);
    }

    private NetForecast.Usage? Learned()
    {
        RefreshUsual();
        return _usual;
    }

        private NetForecast.Progress? Learning()
    {
        RefreshUsual();
        return NetForecast.Learning(_usualDays);
    }

    private long? PreviousWindow(DateOnly today)
    {

        if (_window == NetWindow.Hour) return null;

        int span = NetPanelLayout.WindowDays(_window);
        var to = today.AddDays(-span);
        var from = to.AddDays(-(span - 1));
        if (_meter.Ledger.Oldest is not { } oldest || oldest > from) return null;
        var v = _meter.Ledger.Total(from, to);
        return v.Down + v.Up;
    }

    private static Point? Busiest(Point[] points)
    {
        Point? best = null;
        foreach (var p in points)
            if (p.Total > 0 && (best is null || p.Total >= best.Value.Total)) best = p;
        return best;
    }

    private static void DrawShare(Graphics g, NetColumn col, long down, long up, float fade)
    {
        long total = down + up;
        if (total <= 0 || fade <= 0.01f) return;
        const float h = 4f;
        float w = NetPanelLayout.ColRight - NetPanelLayout.Pad;
        float downW = Math.Clamp((float)(down / (double)total) * w, 0f, w);

        using (var track = new SolidBrush(Mul(UpInk, fade * 0.55f)))
        using (var path = Fx.Rounded(new RectangleF(NetPanelLayout.Pad, col.ShareY, w, h), h / 2f))
            g.FillPath(track, path);
        if (downW < 2f) return;
        using (var lead = new SolidBrush(Mul(DownInk, fade * 0.85f)))
        using (var path = Fx.Rounded(new RectangleF(NetPanelLayout.Pad, col.ShareY, downW, h), h / 2f))
            g.FillPath(lead, path);
    }

    private void DrawTrace(Graphics g, NetColumn col, float fade)
    {
        if (fade <= 0.01f) return;
        var trace = _meter.TraceSnapshot();
        if (trace.Length < 2) return;
        double max = 0;
        foreach (double v in trace) if (v > max) max = v;
        if (max <= 0) return;

        float left = NetPanelLayout.Pad, right = NetPanelLayout.ColRight;
        float baseY = col.TraceTop + col.TraceH;
        var pts = new PointF[trace.Length];
        for (int i = 0; i < trace.Length; i++)
        {

            float x = left + (right - left) * (trace.Length == 1 ? 0f : i / (float)(trace.Length - 1));
            pts[i] = new PointF(x, baseY - (float)(trace[i] / max) * (col.TraceH - 2f));
        }
        var tint = Lan ? LanTone : Green;
        using (var line = new Pen(Mul(tint, fade * 0.75f), 1.2f) { LineJoin = LineJoin.Round })
        using (var path = new GraphicsPath())
        {
            path.AddCurve(pts, 0.35f);
            g.DrawPath(line, path);
        }

        using (var ceil = new Pen(Mul(Color.FromArgb(26, 255, 255, 255), fade), 1f))
            g.DrawLine(ceil, left, col.TraceTop, right, col.TraceTop);

        using var f = new Font("Segoe UI", 9f, GraphicsUnit.Pixel);
        using var ink = new SolidBrush(Mul(Dim, fade * 0.8f));
        string ceiling = NetRate.Format(max);
        float cw = g.MeasureString(ceiling, f, int.MaxValue, StringFormat.GenericTypographic).Width;

        using (var scrim = new SolidBrush(Mul(Color.FromArgb(190, 16, 16, 20), fade)))
        using (var chip = Fx.Rounded(new RectangleF(right - cw - 4f, col.TraceTop + 0.5f, cw + 6f, 12f), 3f))
            g.FillPath(scrim, chip);
        Fx.Text(g, ceiling, f, ink, right - cw - 1f, col.TraceTop + 1f);
    }

    private void DrawSpark(Graphics g, RectangleF[] bars, Point[] points, float fade)
    {
        if (bars.Length == 0 || fade <= 0.01f) return;
        float baseY = bars[0].Bottom;
        float left = bars[0].Left + bars[0].Width / 2f, right = bars[^1].Left + bars[^1].Width / 2f;
        if (right - left < 2f) return;

        var path = new GraphicsPath();
        var top = new PointF[bars.Length + 2];
        top[0] = new PointF(left, baseY);
        for (int i = 0; i < bars.Length; i++)
            top[i + 1] = new PointF(bars[i].Left + bars[i].Width / 2f, bars[i].Top);
        top[^1] = new PointF(right, baseY);
        using (path)
        {

            path.AddCurve(top, 0.4f);
            path.CloseFigure();
            var tint = Lan ? LanTone : Green;

            using (var wash = new SolidBrush(Mul(tint, fade * 0.20f)))
                g.FillPath(wash, path);
            using var edge = new Pen(Mul(tint, fade * 0.85f), 1.4f) { LineJoin = LineJoin.Round };
            using var line = new GraphicsPath();
            line.AddCurve(top[1..^1], 0.4f);
            g.DrawPath(edge, line);
        }

        var newest = bars[^1];
        using var mark = new SolidBrush(Mul(White, fade * 0.9f));
        g.FillEllipse(mark, newest.Left + newest.Width / 2f - 2.2f, newest.Top - 2.2f, 4.4f, 4.4f);
    }

    private void DrawChips(Graphics g, int w, Font f, float fade, float dt)
    {
        var chips = NetPanelLayout.Chips(w);
        var target = chips[(int)_window];

        if (_chipW <= 0f) { _chipX = target.X; _chipW = target.Width; }
        else
        {
            float k = 1f - MathF.Exp(-dt / 0.18f);
            _chipX += (target.X - _chipX) * k;
            _chipW += (target.Width - _chipW) * k;
        }
        using (var pill = Fx.Rounded(new RectangleF(_chipX, target.Y, _chipW, target.Height), target.Height / 2f))
        using (var b = new SolidBrush(Mul(Color.FromArgb(36, 255, 255, 255), fade)))
            g.FillPath(b, pill);

        using var sf = new StringFormat(StringFormat.GenericTypographic)
        { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        for (int i = 0; i < chips.Length; i++)
        {
            bool on = i == (int)_window;
            bool hot = !on && WidgetInput.Over && chips[i].Contains(WidgetInput.Mouse);
            using var b = new SolidBrush(Mul(on ? White : Color.FromArgb(hot ? 210 : 150, 255, 255, 255), fade));
            Fx.Text(g, Halo.Localization.Strings.Get(ChipKeys[i]), f, b, chips[i], sf);
        }
    }

    private void DrawSplit(Graphics g, Func<NetLink?, (long Down, long Up)> window, NetColumn col,
                           Font subF, float fade)
    {
        for (int i = 0; i < 2; i++)
        {
            bool lan = i == 1;
            float y = i == 0 ? col.BandRow1 : col.BandRow2;
            float a = fade * (lan == Lan ? 1f : 0.75f);
            var v = window(lan ? NetLink.Lan : NetLink.Wifi);
            DrawMarkAt(g, NetPanelLayout.Pad + 8f, y + 9f, lan, 16, a);
            using var down = new SolidBrush(Mul(RateInk(_meter.DownRate), a));
            using var up = new SolidBrush(Mul(UpInk, a * 0.92f));
            DrawArrowText(g, NetPanelLayout.Pad + 24f, y, GlyphDown, NetRate.Size(v.Down), subF, down, 12f);
            DrawArrowText(g, NetPanelLayout.Pad + 110f, y, GlyphUp, NetRate.Size(v.Up), subF, up, 12f);
        }
    }

    private void DrawLinkRow(Graphics g, NetColumn col, Font f, float fade)
    {
        _markCx = NetPanelLayout.Pad + 11f;
        _markCy = col.MarkCy;
        DrawMark(g, _markCx, _markCy, Lan, fade);

        if (_splitT > 0.01f)
            using (var ring = new Pen(Mul(White, fade * 0.40f * _splitT), 1f))
                g.DrawEllipse(ring, _markCx - 15f, _markCy - 15f, 30f, 30f);

        if (_linkT <= 0.01f) return;
        float a = fade * _linkT, slide = 6f * (1f - _linkT);
        using var dim = new SolidBrush(Mul(Dim, a));
        if (LinkLabel() is { Length: > 0 } link)
            Fx.Text(g, link, f, dim, _markCx + 18f + slide, _markCy - 7f);

        string foot = FootLine();
        if (foot.Length == 0) return;
        using var footInk = new SolidBrush(Mul(Color.FromArgb(126, 255, 255, 255), a));
        Fx.Text(g, foot, f, footInk, NetPanelLayout.Pad, col.FootY + slide);
    }

    private string LinkLabel()
    {
        if (_meter.LinkSpeed is { } bits && bits > 0) return NetRate.LinkSpeed(bits);
        return _meter.Adapter is { Length: > 0 } name && name.Length <= 22 ? name : "";
    }

        private string FootLine()
    {
        var parts = new List<string>(3);
        if (Halo.ClaudeCode.NetMon.LatestNetMs() is { } ms)
            parts.Add(Halo.Localization.Strings.Format("net.rtt", ms));
        if (_meter.LocalIp is { Length: > 0 } ip) parts.Add(ip);

        if (Halo.ClaudeCode.IpCountry.Cc is { Length: > 0 } country) parts.Add(country);
        return string.Join("  ·  ", parts);
    }

    private static readonly Color LanTone = Color.FromArgb(255, 96, 186, 214);

    private void DrawBars(Graphics g, RectangleF[] bars, Point[] points, float fade)
    {
        float a = fade * _chartFade;
        using var green = new SolidBrush(Mul(Green, a * 0.85f));
        using var blue = new SolidBrush(Mul(LanTone, a * 0.85f));

        using var cap = new SolidBrush(Mul(UpInk, a * 0.9f));
        for (int i = 0; i < bars.Length && i < points.Length; i++)
        {
            if (bars[i].Height <= 0.5f) continue;
            long total = points[i].Total;

            float lanFrac = total > 0 ? Math.Clamp(LanBytesAt(points[i]) / (float)total, 0f, 1f) : 0f;
            using (var p = Fx.Rounded(bars[i], 2f)) g.FillPath(green, p);
            if (lanFrac > 0.01f)
            {

                float lanH = Math.Max(1.5f, bars[i].Height * lanFrac);
                using var lp = Fx.Rounded(new RectangleF(bars[i].X, bars[i].Y, bars[i].Width, lanH), 2f);
                g.FillPath(blue, lp);
            }

            if (total > 0 && points[i].Up > 0)
            {
                float capH = Math.Max(1.5f, bars[i].Height * (points[i].Up / (float)total));
                using var cp = Fx.Rounded(new RectangleF(bars[i].X, bars[i].Y, bars[i].Width, capH), 2f);
                g.FillPath(cap, cp);
            }
        }

        int peakBar = -1;
        long peakBytes = 0;
        for (int i = 0; i < bars.Length && i < points.Length; i++)
            if (points[i].Total > peakBytes) { peakBytes = points[i].Total; peakBar = i; }
        if (peakBar >= 0 && bars[peakBar].Height > 0.5f)
        {
            float cx = bars[peakBar].Left + bars[peakBar].Width / 2f, ty = bars[peakBar].Top - 5f;
            using var caret = new SolidBrush(Mul(White, a * 0.65f));
            g.FillPolygon(caret, [new PointF(cx, ty + 4f), new PointF(cx - 3.5f, ty), new PointF(cx + 3.5f, ty)]);
        }

        int lit = NetPanelLayout.LitBar(_window, bars.Length);
        if (lit < 0 || lit >= bars.Length) return;
        float tw = Math.Min(bars[lit].Width, 8f);
        var tick = new RectangleF(bars[lit].Left + (bars[lit].Width - tw) / 2f, bars[lit].Bottom + 3f, tw, 2f);
        using var nowMark = new SolidBrush(Mul(White, a * 0.55f));
        using var tp = Fx.Rounded(tick, 1f);
        g.FillPath(nowMark, tp);
    }

    private void DrawDayCard(Graphics g, int w, int h, RectangleF[] bars, float top, float baseY,
                             Point[] points, Font f, float fade)
    {
        if (_chartFade < 0.99f) return;
        int i = WidgetInput.Over ? NetPanelLayout.HoverDay(WidgetInput.Mouse, bars, top, baseY) : -1;

        if (i < 0 && _pinned is { } pin)
            for (int k = 0; k < points.Length; k++)
                if (points[k].Label == pin) { i = k; break; }
        if (i < 0 || i >= points.Length) return;
        if (points[i].Total <= 0) return;

        float gx = bars[i].Left + bars[i].Width / 2f;
        using (var guide = new Pen(Mul(White, fade * 0.30f), 1f) { DashStyle = DashStyle.Dot })
            g.DrawLine(guide, gx, top, gx, baseY);

        string day = points[i].Label;
        string down = NetRate.Size(points[i].Down), up = NetRate.Size(points[i].Up);
        const float arrow = 11f, gap = 5f, between = 14f, pad = 8f, lineH = 16f;
        float wDown = g.MeasureString(down, f).Width, wUp = g.MeasureString(up, f).Width;
        float wValues = arrow + gap + wDown + between + arrow + gap + wUp;
        float bw = Math.Max(g.MeasureString(day, f).Width, wValues) + pad * 2f;
        float bh = lineH * 2f + pad * 2f - 2f;
        float right = Math.Min(NetPanelLayout.TrackRight, w - NetPanelLayout.Pad);
        float bx = Math.Clamp(gx - bw / 2f, NetPanelLayout.Pad, Math.Max(NetPanelLayout.Pad, right - bw));

        float by = baseY + 8f;
        if (by + bh > h - 4f) by = top + 4f;

        using (var card = Fx.Rounded(new RectangleF(bx, by, bw, bh), 7f))
        {
            using (var bg = new SolidBrush(Mul(Color.FromArgb(255, 16, 16, 18), fade))) g.FillPath(bg, card);
            using (var edge = new Pen(Mul(Track, fade), 1f)) g.DrawPath(edge, card);
        }
        using var ink = new SolidBrush(Mul(White, fade));
        using var dim = new SolidBrush(Mul(Dim, fade));
        Fx.Text(g, day, f, dim, bx + pad, by + pad - 2f);
        float vy = by + pad + lineH - 3f;
        DrawArrowText(g, bx + pad, vy, GlyphDown, down, f, ink, arrow);
        DrawArrowText(g, bx + pad + arrow + gap + wDown + between, vy, GlyphUp, up, f, dim, arrow);
    }
}
