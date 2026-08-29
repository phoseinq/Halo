using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Halo.Widgets;

internal sealed class BtWidget : IWidget
{
    private const int HoldMs = 7000;
    private static readonly Color White = Color.FromArgb(238, 255, 255, 255);
    private static readonly Color Dim = Color.FromArgb(150, 255, 255, 255);
    private static readonly Color Track = Color.FromArgb(46, 255, 255, 255);
    private static readonly FontFamily Fluent = new("Segoe Fluent Icons");

    private readonly object _lock = new();
    private string _name = "";
    private int _pct;
    private int _glyph = 0xE702;
    private long _until;
    private int _version;
    private float _fillShown = -1f;
    private float _pctIn;

    public void Show(string name, int pct, int major = -1, int minor = -1)
    {
        lock (_lock)
        {
            _name = name;
            _pct = Math.Clamp(pct, 0, 100);

            _glyph = major > 0 ? GlyphForCod(major, minor) : GlyphFor(name);
            _fillShown = 0f;
            _pctIn = 0f;
            _until = Environment.TickCount64 + HoldMs;
            _version++;
        }
    }

    public FaceProp ArrivingProp => FaceProp.Earbud;

    public bool IsActive { get { lock (_lock) return Environment.TickCount64 < _until; } }
    public int Version { get { lock (_lock) return _version; } }
    public bool Animating => IsActive;

    public string Icon => ((char)0xE702).ToString();

    internal const int GlyphBluetooth = 0xE702, GlyphPhone = 0xE8EA, GlyphHeadphone = 0xE7F6,
        GlyphSpeaker = 0xE7F5, GlyphController = 0xE7FC, GlyphKeyboard = 0xE765, GlyphMouse = 0xE962,
        GlyphWatch = 0xEC92, GlyphComputer = 0xE7F8;

    internal static int GlyphForCod(int major, int minor) => major switch
    {
        1 => GlyphComputer,
        2 => GlyphPhone,
        4 => minor switch
        {
            1 or 2 or 6 => GlyphHeadphone,
            5 or 7 or 10 or 8 => GlyphSpeaker,
            18 => GlyphController,
            _ => GlyphSpeaker,
        },

        5 => (minor & 0x10) != 0 ? GlyphKeyboard
           : (minor & 0x20) != 0 ? GlyphMouse
           : (minor & 0x0F) is 1 or 2 ? GlyphController
           : GlyphBluetooth,
        7 => minor == 1 ? GlyphWatch : GlyphBluetooth,
        _ => GlyphBluetooth,
    };

    private static int GlyphFor(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("airpod") || n.Contains("buds") || n.Contains("headphone") || n.Contains("headset")
            || n.Contains("hands-free") || n.Contains(" hf") || n.StartsWith("wh-") || n.StartsWith("wf-")
            || n.Contains("earbud") || n.Contains("pods")) return 0xE7F6;
        if (n.Contains("controller") || n.Contains("dualsense") || n.Contains("dualshock")
            || n.Contains("xbox") || n.Contains("gamepad")) return 0xE7FC;
        if (n.Contains("keyboard")) return 0xE765;
        if (n.Contains("mouse")) return 0xE962;
        if (n.Contains("speaker") || n.Contains("soundbar") || n.Contains("jbl")
            || n.Contains("boom") || n.Contains("sound")) return 0xE7F5;
        if (n.Contains("watch") || n.Contains("band")) return 0xEC92;
        return 0xE8EA;
    }

    private const float ColH = 40f, ExpH = 220f;
    internal static (float cx, float cy, float ring, float disc, float track, float arc) Metrics(int h)
    {
        float t = Math.Clamp((h - ColH) / (ExpH - ColH), 0f, 1f);
        float L(float a, float b) => a + (b - a) * t;
        return (L(23f, 70f), h / 2f, L(13f, 44f), L(9.5f, 36f), L(2.4f, 4f), L(2.8f, 4.6f));
    }

    public void DrawCollapsed(Graphics g, int w, int h, float fade)
    {
        int pct; int glyph;
        lock (_lock) { pct = _pct; glyph = _glyph; }

        if (h < 16) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;

        float target = pct / 100f;
        _fillShown = _fillShown < 0 ? target : _fillShown + (target - _fillShown) * 0.16f;
        if (Math.Abs(target - _fillShown) < 0.004f) _fillShown = target;
        float fill = Math.Clamp(_fillShown, 0f, 1f);
        Color ringCol = Charge(fill);

        _pctIn += (1f - _pctIn) * 0.09f;
        if (_pctIn > 0.995f) _pctIn = 1f;
        float pctIn = 1f - (1f - _pctIn) * (1f - _pctIn);

        var (cx, cy, rr, ir, tw, aw) = Metrics(h);

        Fx.Glow(g, w, h, fade, cx, cy, w * 0.6f, h * 2.0f, 27, ringCol);
        EdgeBand(g, w, h, fade);

        using (var disc = new SolidBrush(Mul(Color.FromArgb(20, 255, 255, 255), fade)))
            g.FillEllipse(disc, cx - ir, cy - ir, ir * 2, ir * 2);
        DrawGlyph(g, new RectangleF(cx - ir, cy - ir, ir * 2, ir * 2), glyph, fade, White);

        using (var tp = new Pen(Mul(Track, fade), tw) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(tp, cx - rr, cy - rr, rr * 2, rr * 2, 0, 360);
        if (fill > 0.001f)
            using (var fp = new Pen(Mul(ringCol, fade), aw) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(fp, cx - rr, cy - rr, rr * 2, rr * 2, -90, 360f * fill);

        float sz = rr * 2f;
        using var pf = new Font("Segoe UI Semibold", h * 0.42f, GraphicsUnit.Pixel);
        using var pb = new SolidBrush(Mul(White, fade * pctIn));
        using var sf = new StringFormat(StringFormat.GenericTypographic)
        { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        float rise = (1f - pctIn) * 3f;
        Fx.Text(g, $"{pct}%", pf, pb, new RectangleF(cx + sz, rise, w - (cx + sz) - 14, h), sf);
    }

    private const float BandPen = 1.6f;

    private static readonly Color Band = Color.FromArgb(150, 34, 138, 76);
    private static GraphicsPath BandPath(int w, int h, float r, float inset)
    {
        float x0 = inset, y0 = inset, x1 = w - inset, y1 = h - inset;
        float d = Math.Min(r, Math.Min(x1 - x0, y1 - y0) / 2f) * 2f;
        var p = new GraphicsPath();
        p.AddLine(x1, y0, x1, y1 - d / 2f);
        p.AddArc(x1 - d, y1 - d, d, d, 0, 90);
        p.AddArc(x0, y1 - d, d, d, 90, 90);
        p.AddLine(x0, y1 - d / 2f, x0, y0);
        return p;
    }

    private void EdgeBand(Graphics g, int w, int h, float fade)
    {

        long left = 0;
        lock (_lock) left = _until - Environment.TickCount64;
        float age = (HoldMs - left) / 380f;
        float a = fade * Math.Clamp(age, 0f, 1f);
        if (a <= 0.01f) return;
        using var path = BandPath(w, h, Math.Min(h / 2f, 20f), BandPen / 2f);
        using var pen = new Pen(Mul(Band, a), BandPen) { LineJoin = LineJoin.Round, EndCap = LineCap.Flat };
        g.DrawPath(pen, path);
    }

    private static Color Charge(float fill)
        => Fx.HsvToRgb(Math.Clamp(fill, 0f, 1f) * 120f, 0.68f, 0.96f);

    public void DrawContent(Graphics g, int w, int h, float fade)
    {
        if (fade <= 0.01f) return;
        int pct, glyph; string name;
        lock (_lock) { pct = _pct; glyph = _glyph; name = _name; }
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float fill = pct / 100f;
        Color ringCol = Charge(fill);

        var (cx, cy, rr, ir, tw, aw) = Metrics(h);
        Fx.Glow(g, w, h, fade, cx, cy, w * 0.7f, h * 1.2f, 36, ringCol);
        using (var disc = new SolidBrush(Mul(Color.FromArgb(20, 255, 255, 255), fade)))
            g.FillEllipse(disc, cx - ir, cy - ir, ir * 2, ir * 2);
        DrawGlyph(g, new RectangleF(cx - ir, cy - ir, ir * 2, ir * 2), glyph, fade, White);
        using (var tp = new Pen(Mul(Track, fade), tw) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(tp, cx - rr, cy - rr, rr * 2, rr * 2, 0, 360);
        using (var fp = new Pen(Mul(ringCol, fade), aw) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawArc(fp, cx - rr, cy - rr, rr * 2, rr * 2, -90, 360f * fill);

        float tx = cx + rr + 22;
        using var nf = new Font("Segoe UI Semibold", 22f, GraphicsUnit.Pixel);
        using var bf = new Font("Segoe UI", 15f, GraphicsUnit.Pixel);
        using (var nb = new SolidBrush(Mul(White, fade)))
            Fx.Text(g, name, nf, nb, tx, cy - 26);
        using (var bb = new SolidBrush(Mul(Dim, fade)))
            Fx.Text(g, $"{pct}% battery", bf, bb, tx, cy + 4);
    }

    public IReadOnlyList<(RectangleF rect, Action<PointF> onClick)> Buttons(int w, int h)
        => Array.Empty<(RectangleF, Action<PointF>)>();

    private static readonly Dictionary<int, Bitmap> _glyphCache = new();
    private static Bitmap GlyphBitmap(int cp)
    {
        lock (_glyphCache)
        {
            if (_glyphCache.TryGetValue(cp, out var cached)) return cached;
            var b = RenderTight(cp);
            _glyphCache[cp] = b;
            return b;
        }
    }

    private static Bitmap RenderTight(int cp)
    {
        const int N = 128;
        var full = new Bitmap(N, N, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(full))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var f = new Font(Fluent, N * 0.7f, GraphicsUnit.Pixel);
            using var br = new SolidBrush(Color.White);
            using var sf = new StringFormat(StringFormat.GenericTypographic)
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            sf.FormatFlags |= StringFormatFlags.NoClip;
            Fx.Text(g, ((char)cp).ToString(), f, br, new RectangleF(0, 0, N, N), sf);
        }
        var ink = InkBounds(full);
        if (ink.Width <= 0 || ink.Height <= 0) return full;
        var tight = full.Clone(ink, PixelFormat.Format32bppArgb);
        full.Dispose();
        return tight;
    }

    private static Rectangle InkBounds(Bitmap b)
    {
        var data = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            var buf = new byte[stride * b.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, buf.Length);
            int minX = b.Width, minY = b.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                    if (buf[y * stride + x * 4 + 3] > 16)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            return maxX < minX ? Rectangle.Empty : Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }
        finally { b.UnlockBits(data); }
    }

    private static void DrawGlyph(Graphics g, RectangleF r, int cp, float fade, Color tint)
    {
        var gb = GlyphBitmap(cp);
        float target = r.Height * 0.58f;
        float scale = target / Math.Max(gb.Width, gb.Height);

        float dw = gb.Width * scale, dh = gb.Height * scale;
        var dst = new RectangleF(r.X + r.Width / 2f - dw / 2f, r.Y + r.Height / 2f - dh / 2f, dw, dh);
        using var ia = new ImageAttributes();

        ia.SetWrapMode(WrapMode.TileFlipXY);
        ia.SetColorMatrix(new ColorMatrix(new[]
        {
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, fade * 0.95f, 0 },
            new float[] { tint.R / 255f, tint.G / 255f, tint.B / 255f, 0, 1 },
        }));

        var oldInterp = g.InterpolationMode;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(gb, new[] { dst.Location, new PointF(dst.Right, dst.Y), new PointF(dst.X, dst.Bottom) },
            new RectangleF(0, 0, gb.Width, gb.Height), GraphicsUnit.Pixel, ia);
        g.InterpolationMode = oldInterp;
    }

    private static Color Mul(Color c, float a)
        => Color.FromArgb((int)Math.Clamp(c.A * a, 0, 255), c.R, c.G, c.B);
}
