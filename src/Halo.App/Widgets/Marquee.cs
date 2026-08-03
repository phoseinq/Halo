using System.Drawing;

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
            g.DrawString(text, f, b, new RectangleF(x, y, w, f.Height + 4), pf);
            return;
        }
        _scrolling = true;

        float span = textW + Gap;
        (_offset, _hold) = Step(_offset, _hold, dt, span, hovered ? Hold : Rest);

        var state = g.Save();
        g.SetClip(new RectangleF(x, y, w, f.Height + 4));
        bool rtl = Fx.IsRtl(text);
        using var sf = new StringFormat(StringFormatFlags.NoWrap);
        if (rtl) sf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
        float h2 = f.Height + 4;
        for (int pass = 0; pass < 2; pass++)
        {

            float ox = rtl ? x + w - textW + (_offset - pass * span)
                           : x - (_offset - pass * span);
            g.DrawString(text, f, b, new RectangleF(ox, y, textW + 2, h2), sf);
        }
        g.Restore(state);
    }
}
