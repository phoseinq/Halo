using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Halo.Notifications;

namespace Halo.Widgets;

internal readonly record struct HistoryRow(NotifItem Item, float Slide, bool Open);

internal static class HistoryView
{

    private static readonly Color White = Color.FromArgb(252, 255, 255, 255);
    private static readonly Color Dim = Color.FromArgb(196, 255, 255, 255);
    private static readonly Color Faint = Color.FromArgb(182, 255, 255, 255);

    private static readonly Color CardTop = Color.FromArgb(34, 255, 255, 255);
    private static readonly Color CardBottom = Color.FromArgb(14, 255, 255, 255);
    private static readonly Color CardHotTop = Color.FromArgb(58, 255, 255, 255);
    private static readonly Color CardHotBottom = Color.FromArgb(30, 255, 255, 255);
    private static readonly Color RimTop = Color.FromArgb(104, 255, 255, 255);
    private static readonly Color RimBottom = Color.FromArgb(52, 0, 0, 0);

    private static Font GistFont() => new("Segoe UI", 13.5f, GraphicsUnit.Pixel);

    internal static float BodyExtra(string? body)
    {
        string full = HistoryText.Full(body);
        if (full.Length == 0) return 0f;
        try
        {
            using var bmp = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using var g = Graphics.FromImage(bmp);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            using var f = GistFont();
            float wide = HistoryLayout.Body(0f, HistoryLayout.OpenExtra).Width;
            return HistoryLayout.ExtraFor(g.MeasureString(full, f, (int)wide).Height);
        }
        catch { return HistoryLayout.OpenExtra; }
    }

        internal static void Draw(Graphics g, int w, int h, float fade, HistoryRow[] rows,
                             float scroll, bool deckOpen, int hover, float hoverT, bool overClear,
                             (Color Left, Color Right) accent,
                             HistoryShift shift = default, HistoryRow? leaving = null,
                             float reveal = 1f)
    {
        if (fade <= 0.01f) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        int prevContrast = g.TextContrast;
        g.TextContrast = 0;

        using var head = new Font("Segoe UI Semibold", 13f, GraphicsUnit.Pixel);

        using var appF = new Font("Segoe UI", 11f, GraphicsUnit.Pixel);
        using var titleF = new Font("Segoe UI Semibold", 16f, GraphicsUnit.Pixel);
        using var gistF = GistFont();

        float headX = HistoryLayout.CardX + HistoryLayout.IconPad;

        if (rows.Length > 0)
        {

            var slot = HistoryLayout.ClearAll();
            string words = Localization.Strings.Get("history.clear");
            float tw = g.MeasureString(words, head).Width;
            var bub = new RectangleF(slot.Right - tw - 24f, slot.Y, tw + 24f, slot.Height);
            var giveClear = Give(g, bub, Button.ClearAll);
            using (var bb = new SolidBrush(Mul(Color.FromArgb(overClear ? 40 : 22, 255, 255, 255), fade)))
            using (var bp = Fx.Rounded(bub, bub.Height / 2f))
            {
                g.FillPath(bb, bp);
                using var bpen = new Pen(Mul(Color.FromArgb(overClear ? 70 : 40, 255, 255, 255), fade), 1f);
                g.DrawPath(bpen, bp);
            }
            using var cb = new SolidBrush(Mul(overClear ? White : Dim, fade));
            using var cf = new StringFormat
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(words, head, cb, bub, cf);
            if (giveClear is { } gc) g.Restore(gc);
        }

        {
            var mb = HistoryLayout.MuteAll();
            var giveMute = Give(g, mb, Button.MuteAll);
            using (var bb = new SolidBrush(Mul(Color.FromArgb(MuteAllHover ? 40 : MuteAll ? 34 : 22, 255, 255, 255), fade)))
            using (var bp = Fx.Rounded(mb, mb.Height / 2f))
            {
                g.FillPath(bb, bp);
                using var bpen = new Pen(Mul(Color.FromArgb(MuteAllHover ? 70 : 40, 255, 255, 255), fade), 1f);
                g.DrawPath(bpen, bp);
            }
            DrawBell(g, mb, MuteAll ? BellOff : Bell, Mul(MuteAll ? Color.FromArgb(255, 255, 160, 140) : (MuteAllHover ? White : Dim), fade));
            if (giveMute is { } gm) g.Restore(gm);
        }

        if (rows.Length == 0 && leaving is null)
        {

            using var emptyF = new Font("Segoe UI Semilight", 17f, GraphicsUnit.Pixel);
            using var eb = new SolidBrush(Mul(Color.FromArgb(205, 255, 255, 255), fade));
            using var ef = new StringFormat
            { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            float top = HistoryLayout.BodyTop;
            g.DrawString(Localization.Strings.Get("history.empty"), emptyF, eb,
                         new RectangleF(0, top, w, HistoryLayout.Height(0) - top - HistoryLayout.PadBottom), ef);
            g.TextContrast = prevContrast;
            return;
        }

        var keep = g.Save();
        float bottom = HistoryLayout.Height(rows.Length, shift.Total, deckOpen) - HistoryLayout.PadBottom;
        g.SetClip(new RectangleF(0, HistoryLayout.BodyTop, w, bottom - HistoryLayout.BodyTop),
                  CombineMode.Intersect);

        int first = deckOpen ? 0 : HistoryLayout.FirstVisible(scroll);
        int shown = deckOpen ? rows.Length : Math.Min(rows.Length, first + HistoryLayout.Visible);

        if (!deckOpen)
        {
            for (int d = HistoryLayout.MaxBack; d >= 1; d--)
            {
                int i = first + HistoryLayout.Visible - 1 + d;
                if (i >= rows.Length) continue;
                float q = i - scroll / HistoryLayout.RowH;
                var (top, inset) = HistoryLayout.Pose(q);

                var keepBack = g.Save();
                g.SetClip(new RectangleF(0, HistoryLayout.BandTop(q), w, HistoryLayout.RowH),
                          CombineMode.Intersect);
                DrawRow(g, w, h, rows[i], top, 0f, fade, 0f, false, accent,
                        appF, titleF, gistF, inset);
                g.Restore(keepBack);
            }
        }

        for (int i = first; i < shown; i++)
        {
            float top = HistoryLayout.RowTop(i, scroll, shift);
            if (top > h || top + HistoryLayout.RowH < HistoryLayout.BodyTop) continue;

            float p = HistoryLayout.Reveal(i - first, reveal);
            if (p <= 0f) continue;

            DrawRow(g, w, h, rows[i], HistoryLayout.RevealTop(top, p), shift.ExtraAt(i), fade * p,
                    i == hover ? hoverT : 0f, p >= 1f, accent, appF, titleF, gistF);
        }

        if (!deckOpen)
            DrawDeck(g, rows.Length, scroll, shift, fade,
                     HistoryLayout.Reveal(shown - first, reveal), appF);

        if (leaving is { } gone)
            DrawRow(g, w, h, gone, HistoryLayout.LeavingTop(scroll, shift), 0f, fade, 0f, true, accent,
                    appF, titleF, gistF);
        g.Restore(keep);
        g.TextContrast = prevContrast;
        DrawEdges(g, w, h, fade);
    }

    private static void DrawEdges(Graphics g, int w, int h, float fade)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (EdgeRight > 0.01f)
            using (var p = new Pen(Color.FromArgb((int)(170 * EdgeRight * fade), 255, 255, 255), 3f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float cy = HistoryLayout.BodyTop + (h - HistoryLayout.BodyTop) / 2f, len = Math.Min(44f, h * 0.2f);
                g.DrawLine(p, w - 5f, cy - len / 2f, w - 5f, cy + len / 2f);
            }
    }

    private static void DrawDeck(Graphics g, int count, float scroll, HistoryShift shift, float fade,
                                 float reveal, Font appF)
    {

        int hidden = HistoryLayout.Hidden(count, scroll);
        if (hidden == 0 || reveal <= 0f) return;
        var d = HistoryLayout.Deck(count, scroll, shift);

        using var mf = new Font("Segoe UI Semibold", 11.5f, GraphicsUnit.Pixel);
        using var db = new SolidBrush(Mul(Color.FromArgb(225, 255, 255, 255), fade * reveal));
        using var df = new StringFormat
        { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(string.Format(Localization.Strings.Get("history.more"), hidden), mf, db,
                     new RectangleF(d.X, d.Bottom - 17f, d.Width, 17f), df);
    }

    private const int ShadowPad = 12;

    private static readonly System.Collections.Generic.Dictionary<(int W, int H, bool Hot), Bitmap> Chrome = new();

    private static void PaintCard(Graphics g, RectangleF card, float hotT, float fade, float depth, Color accent)
    {

        bool hot = hotT > 0.99f;
        int cw = (int)MathF.Round(card.Width), ch = (int)MathF.Round(card.Height);
        if (ch != (int)HistoryLayout.CardH || depth > 0.01f || (hotT > 0.01f && hotT < 0.99f))
        {
            DrawCardGlass(g, card, hotT, fade, depth);
        }
        else
        {
            Bitmap chrome;
            var key = (cw, ch, hot);
            lock (Chrome)
            {
                if (!Chrome.TryGetValue(key, out chrome!))
                {

                    if (Chrome.Count >= 6)
                    {
                        foreach (var old in Chrome.Values) old.Dispose();
                        Chrome.Clear();
                    }
                    chrome = new Bitmap(cw + ShadowPad * 2, ch + ShadowPad * 2,
                                        System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                    using (var cg = Graphics.FromImage(chrome))
                    {
                        cg.SmoothingMode = SmoothingMode.AntiAlias;

                        Fx.Supersampled(cg, new RectangleF(0, 0, chrome.Width, chrome.Height), 4, sg =>
                        {
                            sg.SmoothingMode = SmoothingMode.AntiAlias;
                            DrawCardGlass(sg, new RectangleF(ShadowPad, ShadowPad, cw, ch), hot ? 1f : 0f, 1f, 0f);
                        });
                    }
                    if (Chrome.Count > 12) { foreach (var v in Chrome.Values) v.Dispose(); Chrome.Clear(); }
                    Chrome[key] = chrome;
                }
            }
            using var ia = new System.Drawing.Imaging.ImageAttributes();
            ia.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = Math.Clamp(fade, 0f, 1f) });
            g.DrawImage(chrome,
                new Rectangle((int)MathF.Round(card.X) - ShadowPad, (int)MathF.Round(card.Y) - ShadowPad,
                              chrome.Width, chrome.Height),
                0, 0, chrome.Width, chrome.Height, GraphicsUnit.Pixel, ia);
        }

    }

    private static void DrawCardGlass(Graphics g, RectangleF card, float hotT, float fade, float depth)
    {
        float hotF = Math.Clamp(hotT, 0f, 1f);
        using var cp = Fx.Rounded(card, HistoryLayout.CardR);
        using var fill = new SolidBrush(Mul(Color.FromArgb((int)(20 + 12 * hotF), 255, 255, 255), fade));
        g.FillPath(fill, cp);
    }

    private static void DrawRow(Graphics g, int w, int h, HistoryRow row, float top, float extra, float fade,
                                float hotT, bool bloom, (Color Left, Color Right) accent,
                                Font appF, Font titleF, Font gistF, float inset = 0f)
    {
        var n = row.Item;

        var keep = g.Save();
        if (row.Slide != 0f)
        {
            g.TranslateTransform(row.Slide, 0f);
            fade *= 1f - Math.Min(0.85f, Math.Abs(row.Slide) / HistoryLayout.W);
        }

        var card = RectangleF.Inflate(HistoryLayout.Card(top, extra), -inset, 0f);
        float depth = Math.Clamp(inset / HistoryLayout.PeekInset, 0f, 1f);
        PaintCard(g, card, hotT, fade, depth, Fx.AccentOf(n.Icon));

        const float d = HistoryLayout.IconD;

        float ix = MathF.Round(card.X + HistoryLayout.IconPad), iy = MathF.Round(card.Y + (HistoryLayout.CardH - d) / 2f);

        if (n.Icon != null)
        {
            if (bloom)

                Fx.Glow(g, w, h, fade, ix + d / 2f, iy + d / 2f, d * 1.3f, d * 1.3f, 9f, Fx.AccentOf(n.Icon));
            NotifBanner.DrawAppIcon(g, n.Icon, ix, iy, d, fade);
        }
        else
        {

            using var ring = new Pen(Mul(Dim, fade), 1.6f);
            using var rp = Fx.Rounded(new RectangleF(ix, iy, d, d), d * 0.26f);
            g.DrawPath(ring, rp);
            using var f0 = new Font("Segoe UI Semibold", d * 0.5f, GraphicsUnit.Pixel);
            using var b0 = new SolidBrush(Mul(White, fade));
            using var sf0 = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            Fx.Text(g, n.App.Length > 0 ? n.App[..1].ToUpperInvariant() : "•", f0, b0,
                    new RectangleF(ix, iy, d, d), sf0);
        }

        float tx = HistoryLayout.TextX, tr = card.Right - HistoryLayout.IconPad;

        using (var b = new SolidBrush(Mul(Dim, fade)))
        {
            string when = HistoryText.When(n.Time, DateTime.Now);
            float whenW = hotT > 0.5f ? 0f : g.MeasureString(when, appF).Width;
            if (hotT <= 0.5f) g.DrawString(when, appF, b, tr - whenW, card.Y + 11f);
            using var af = NotifBanner.LineFmt(n.App);
            Fx.Text(g, n.App, appF, b,
                    new RectangleF(tx, card.Y + 11f, Math.Max(10f, tr - tx - whenW - 10f), 16f), af);
        }

        using (var fmt = NotifBanner.LineFmt(n.Title))
        {

            using var b = new SolidBrush(Mul(White, fade));
            Fx.Text(g, n.Title, titleF, b, new RectangleF(tx, card.Y + 28f, tr - tx, 22f), fmt);
        }

        using (var b = new SolidBrush(Mul(Faint, fade)))
        {
            if (row.Open)
            {

                string full = HistoryText.Full(n.Body);
                using var wf = new StringFormat(StringFormat.GenericTypographic)
                { Trimming = StringTrimming.EllipsisCharacter };
                if (Fx.IsRtl(full)) wf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
                Fx.Text(g, full, gistF, b, HistoryLayout.Body(top, extra), wf);
            }
            else
            {
                string gist = HistoryText.Gist(n.Body);
                int lines = HistoryLayout.GistLines;
                if (gist.Length > 0 && lines > 1)
                {

                    string full = HistoryText.Full(n.Body);
                    using var wf = new StringFormat(StringFormat.GenericTypographic)
                    { Trimming = StringTrimming.EllipsisWord };
                    if (Fx.IsRtl(full)) wf.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
                    Fx.Text(g, full, gistF, b, new RectangleF(tx, card.Y + HistoryLayout.GistY,
                            Math.Max(10f, tr - tx), lines * HistoryLayout.GistLineH + 1f), wf);
                }
                else if (gist.Length > 0)
                {
                    float right = tr;
                    using var gf = NotifBanner.LineFmt(gist);
                    Fx.Text(g, gist, gistF, b,
                            new RectangleF(tx, card.Y + HistoryLayout.GistY, Math.Max(10f, right - tx), 19f), gf);
                }
            }
        }

        bool appMuted = AppMuted?.Invoke(n) ?? false;
        if (hotT > 0.01f || appMuted)
        {
            float a = Math.Max(hotT, appMuted ? 0.55f : 0f);
            var mr = HistoryLayout.MuteApp(top);
            var giveApp = Give(g, mr, Button.MuteApp, n);
            DrawBell(g, mr, appMuted ? BellOff : Bell,
                     Mul(appMuted ? Color.FromArgb(255, 255, 160, 140) : White, fade * 0.8f * a));
            if (giveApp is { } ga) g.Restore(ga);
        }
        if (hotT > 0.01f)
        {
            var x = HistoryLayout.Dismiss(top);
            var giveX = Give(g, x, Button.Dismiss, n);

            bool onX = HoverButton == Button.Dismiss;
            if (onX)
                using (var disc = new SolidBrush(Mul(Color.FromArgb(46, 255, 120, 110), fade * hotT)))
                    g.FillEllipse(disc, x.X + 4f, x.Y + 4f, x.Width - 8f, x.Height - 8f);
            using var pen = new Pen(Mul(onX ? Color.FromArgb(255, 255, 150, 140) : White,
                                        fade * (onX ? 0.95f : 0.7f) * hotT), 1.6f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            const float m = 9f;
            g.DrawLine(pen, x.Left + m, x.Top + m, x.Right - m, x.Bottom - m);
            g.DrawLine(pen, x.Right - m, x.Top + m, x.Left + m, x.Bottom - m);
            if (giveX is { } gx) g.Restore(gx);
        }
        g.Restore(keep);
    }

    internal static bool MuteAll, MuteAllHover;

    internal static Button HoverButton;

    internal static float EdgeRight;

    internal enum Button { None, MuteAll, ClearAll, MuteApp, Dismiss }

    internal static (Button Which, NotifItem? Item, float T) Pressed;
    private const float PressDepth = 0.12f;

        private static GraphicsState? Give(Graphics g, RectangleF r, Button which, NotifItem? item = null)
    {
        if (Pressed.Which != which || (item is not null && !ReferenceEquals(Pressed.Item, item))
            || Pressed.T <= 0.001f) return null;
        float t = Pressed.T * Pressed.T * (3f - 2f * Pressed.T);
        float k = 1f - PressDepth * t;
        var st = g.Save();
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
        g.TranslateTransform(cx, cy);
        g.ScaleTransform(k, k);
        g.TranslateTransform(-cx, -cy);
        return st;
    }
    internal static Func<NotifItem, bool>? AppMuted;

    private const string Bell = "\uEA8F", BellOff = "\uE7ED";

    private static void DrawBell(Graphics g, RectangleF r, string glyph, Color ink)
    {
        using var f = new Font("Segoe Fluent Icons", 14f, GraphicsUnit.Pixel);
        using var b = new SolidBrush(ink);
        Fx.GlyphCentred(g, r, glyph, f, b);
    }

    private static Color Mul(Color c, float a) => Color.FromArgb((int)(c.A * Math.Clamp(a, 0f, 1f)), c);

    private static Color Blend(Color a, Color b, float t) =>
        Color.FromArgb((int)(a.A + (b.A - a.A) * Math.Clamp(t, 0f, 1f)), a);
}
