using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Halo.Widgets;

internal sealed class Marquee
{
    internal const float Gap = 48f, Speed = 42f, Hold = 0.35f;
    internal const float Rest = 1.6f;

    private float _offset;
    private float _hold;

    private volatile bool _scrolling;
    public bool Scrolling => _scrolling;

    public void Park() => _scrolling = false;

    internal static (float offset, float hold) Step(float offset, float hold, float dt, float span,
        float holdFor = Hold)
    {
        if (span <= 0f) return (0f, 0f);
        if (hold < holdFor) return (offset, hold + dt);
        offset += Speed * dt;
        return offset >= span ? (offset - span, 0f) : (offset, hold);
    }

    public void Draw(Graphics g, string text, Font f, Brush b, float x, float y, float w,
        bool hovered, float dt)
    {

        text = Fx.PinRtlDashes(text);
        float textW = g.MeasureString(text, f, int.MaxValue, StringFormat.GenericTypographic).Width;
        if (textW <= w)
        {

            _offset = 0f; _hold = 0f;
            _scrolling = false;
            using var pf = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
            if (Fx.IsRtl(text)) pf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            Fx.Text(g, text, f, b, new RectangleF(x, y, w, f.Height + 4), pf);
            return;
        }
        _scrolling = true;

        float span = textW + Gap;
        (_offset, _hold) = Step(_offset, _hold, dt, span, hovered ? Hold : Rest);

        var state = g.Save();

        var hint = g.TextRenderingHint;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.SetClip(new RectangleF(x, y, w, f.Height + 4));
        bool rtl = Fx.IsRtl(text);

        using var sf = new StringFormat(StringFormat.GenericTypographic) { FormatFlags = StringFormatFlags.NoWrap };
        if (rtl) sf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        float h2 = f.Height + 4;

        var shaped = b is SolidBrush sb ? Shaped(g, text, f, sb.Color, sf, textW + 2f, h2) : null;

        float sc;
        using (var mt = g.Transform) sc = MathF.Abs(mt.Elements[0]);
        if (sc <= 0f) sc = 1f;
        float bw = shaped != null ? shaped.Width / sc : textW + 2f;
        float bh = shaped != null ? shaped.Height / sc : h2;
        var oldInterp = g.InterpolationMode;
        var oldOffset = g.PixelOffsetMode;
        if (shaped != null)
        {

            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
        for (int pass = 0; pass < 2; pass++)
        {

            float ox = rtl ? x + w - textW + (_offset - pass * span)
                           : x - (_offset - pass * span);
            var box = new RectangleF(ox, y, bw, bh);
            if (shaped != null) g.DrawImage(shaped, box);
            else Fx.Text(g, text, f, b, box, sf);
        }
        g.InterpolationMode = oldInterp;
        g.PixelOffsetMode = oldOffset;
        g.TextRenderingHint = hint;
        g.Restore(state);
    }

    private Bitmap? _bmp;
    private string? _bmpText, _bmpFamily;
    private float _bmpScale, _bmpSize;
    private int _bmpColor;
    private FontStyle _bmpStyle;

    private Bitmap? Shaped(Graphics g, string text, Font f, Color c, StringFormat sf, float w, float h)
    {
        try
        {

            if (c.A < 250) return null;

            float s;
            using (var m = g.Transform) s = MathF.Abs(m.Elements[0]);
            if (s <= 0f) s = 1f;
            if (_bmp != null && _bmpText == text && _bmpScale == s && _bmpColor == c.ToArgb()
                && _bmpSize == f.Size && _bmpStyle == f.Style && _bmpFamily == f.FontFamily.Name)
                return _bmp;

            int pw = (int)MathF.Ceiling(w * s), ph = (int)MathF.Ceiling(h * s);

            if (pw <= 0 || ph <= 0 || pw > 8192 || ph > 512) return null;
            var bmp = new Bitmap(pw, ph, PixelFormat.Format32bppPArgb);
            using (var bg = Graphics.FromImage(bmp))
            {
                bg.Clear(Color.Transparent);
                bg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                bg.ScaleTransform(s, s);
                using var brush = new SolidBrush(c);
                Fx.Text(bg, text, f, brush, new RectangleF(0f, 0f, w, h), sf);
            }
            _bmp?.Dispose();
            _bmp = bmp;
            _bmpText = text; _bmpScale = s; _bmpColor = c.ToArgb();
            _bmpSize = f.Size; _bmpStyle = f.Style; _bmpFamily = f.FontFamily.Name;
            return _bmp;
        }
        catch { return null; }
    }

}
