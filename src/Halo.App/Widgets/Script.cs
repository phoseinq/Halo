using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Halo.Widgets;

internal static class Script
{

    internal readonly record struct Glyph(GraphicsPath[] Strokes, float[] Lengths, float Advance,
                                          float Length, float Depth);

    private const float Space = 17f, Track = 9f;

    internal const float Slant = 0.24f;
    private const float Top = -80f, Rest = 6f;

    private static readonly HashSet<char> Apart = ['\'', 'H'];

    internal static float[] Join(PointF from, PointF to)
    {
        float dx = to.X - from.X;

        if (to.Y < -50f)
            return [from.X, from.Y, from.X + dx * 0.6f + 6f, from.Y - 14f, to.X + 9f, to.Y + 26f, to.X, to.Y];
        if (to.Y < from.Y - 6f && dx > 14f)
        {
            float lift = MathF.Min(to.Y, from.Y) - 10f;
            return [from.X, from.Y, from.X + dx * 0.25f, from.Y - 4f, to.X - dx * 0.45f, lift, to.X, to.Y];
        }
        return [from.X, from.Y, from.X + dx * 0.45f, from.Y - 2f, to.X - dx * 0.35f, to.Y + 4f, to.X, to.Y];
    }

    private static PointF First(Glyph g) => g.Strokes[0].PathPoints[0];
    private static PointF Last(Glyph g)
    {
        var pts = g.Strokes[^1].PathPoints;
        return pts[^1];
    }

    private static List<(GraphicsPath Path, float Len)> Ink(string text, Dictionary<char, Glyph> map)
    {
        var ink = new List<(GraphicsPath, float)>();
        float x = 0f;
        char prev = ' ';
        float prevX = 0f;
        foreach (char c in text)
        {
            if (c == ' ') { x += Space; prev = ' '; continue; }
            if (!map.TryGetValue(c, out var gl)) continue;

            if (prev != ' ' && !Apart.Contains(prev) && !Apart.Contains(c) && map.TryGetValue(prev, out var pg)
                && First(gl).X <= 8f && Last(pg).Y > -20f)

            {
                var a = Last(pg); var b = First(gl);
                var j = Join(new PointF(a.X + prevX, a.Y), new PointF(b.X + x, b.Y));
                var jp = new GraphicsPath();
                jp.AddBezier(j[0], j[1], j[2], j[3], j[4], j[5], j[6], j[7]);
                ink.Add((jp, Measure(jp)));
            }
            using var m = new Matrix();
            m.Translate(x, 0f);
            for (int k = 0; k < gl.Strokes.Length; k++)
            {
                var sp = (GraphicsPath)gl.Strokes[k].Clone();
                sp.Transform(m);
                ink.Add((sp, gl.Lengths[k]));
            }
            prev = c;
            prevX = x;
            x += gl.Advance + Track;
        }
        return ink;
    }

    private static readonly Dictionary<char, (float Advance, float[][] Strokes)> Hand = new()
    {
        ['h'] = (34f, [
            [0, -78, 1, -55, 3, -28, 4, 0],
            [4, -6, 8, -30, 17, -33, 24, -33, 31, -33, 32, -22, 32, -12, 32, -6, 32, -2, 34, 1],
        ]),
        ['a'] = (31f, [
            [26, -30, 20, -37, 7, -36, 5, -21, 3, -7, 12, -1, 19, -6, 24, -10, 26, -21, 26, -30],
            [26, -30, 26, -18, 26, -7, 29, 1],
        ]),
        ['l'] = (16f, [[3, -78, 2, -50, 0, -22, 2, -7, 3, -1, 7, 1, 12, -2]]),
        ['o'] = (30f, [[26, -19, 27, -31, 16, -37, 9, -31, 1, -24, 2, -6, 11, -2, 20, 2, 27, -8, 26, -19]]),
        ['i'] = (15f, [
            [4, -34, 2, -22, 1, -10, 3, -3, 5, 0, 9, 1, 13, -3],
            [5, -47, 6, -47, 7, -46, 6, -45],
        ]),
        ['\''] = (8f, [[3, -78, 5, -72, 4, -66, 1, -61]]),
        ['m'] = (48f, [
            [1, -33, 0, -20, 0, -9, 1, 0],
            [1, -8, 3, -28, 9, -34, 15, -34, 21, -34, 22, -24, 22, -13, 22, -6, 22, -2, 22, 0],
            [22, -9, 24, -28, 30, -34, 36, -34, 43, -34, 44, -23, 44, -12, 44, -6, 44, -2, 46, 1],
        ]),
        ['w'] = (43f, [[0, -33, 2, -14, 7, -3, 11, 1, 15, -3, 18, -19, 20, -30,
                        22, -19, 25, -3, 29, 1, 33, -3, 39, -16, 42, -33]]),
        ['e'] = (29f, [[2, -13, 10, -15, 19, -18, 27, -21, 29, -31, 22, -36, 14, -35,
                        4, -34, -1, -22, 3, -11, 7, -1, 19, 2, 27, -6]]),

        ['u'] = (32f, [
            [2, -34, 1, -26, 0, -20, 1, -14, 2, -6, 7, -2, 14, -2, 20, -2, 25, -12, 26, -24],
            [26, -34, 26, -22, 26, -10, 29, 1],
        ]),
        ['d'] = (32f, [
            [26, -19, 27, -31, 16, -37, 9, -31, 1, -24, 2, -6, 11, -2, 20, 2, 27, -8, 26, -19],
            [26, -78, 26, -55, 26, -30, 26, -12, 26, -5, 28, 1, 31, -2],
        ]),

        ['p'] = (32f, [
            [4, -34, 3, -18, 3, -2, 3, 12, 3, 18, 6, 21, 10, 19],
            [4, -33, 10, -38, 20, -39, 27, -34, 30, -26, 27, -17, 18, -12, 10, -13, 6, -16, 4, -19],
        ]),
        ['t'] = (21f, [
            [10, -56, 8, -40, 6, -24, 5, -11, 5, -4, 9, 1, 15, -3],
            [-2, -35, 4, -36, 11, -37, 18, -38],
        ]),

        ['H'] = (40f, [
            [1, -78, 0, -52, 0, -26, 1, 0],
            [33, -78, 33, -52, 33, -26, 35, 1],
            [1, -42, 12, -43, 23, -43, 33, -44],
        ]),
        ['c'] = (28f, [[26, -26, 22, -34, 9, -36, 4, -23, -1, -10, 6, 1, 15, 1, 20, 1, 24, -3, 27, -7]]),
    };

    private static Dictionary<char, Glyph>? _built;

    private static Dictionary<char, Glyph> Built()
    {
        if (_built is not null) return _built;
        var map = new Dictionary<char, Glyph>();
        foreach (var (c, (advance, strokes)) in Hand)
        {
            var paths = new GraphicsPath[strokes.Length];
            var lens = new float[strokes.Length];
            float total = 0f, depth = 0f;
            for (int k = 0; k < strokes.Length; k++)
            {
                var s = SmoothJoints(strokes[k]);

                for (int i = 1; i < s.Length; i += 2) depth = MathF.Max(depth, s[i]);
                var path = new GraphicsPath();
                var cur = new PointF(s[0], s[1]);
                for (int i = 2; i + 5 < s.Length; i += 6)
                {
                    path.AddBezier(cur, new PointF(s[i], s[i + 1]), new PointF(s[i + 2], s[i + 3]),
                        new PointF(s[i + 4], s[i + 5]));
                    cur = new PointF(s[i + 4], s[i + 5]);
                }
                paths[k] = path;
                lens[k] = Measure(path);
                total += lens[k];
            }
            map[c] = new Glyph(paths, lens, advance, total, depth);
        }
        _built = map;
        return map;
    }

    internal const float CornerDeg = 75f;

    internal static float[] SmoothJoints(float[] s)
    {
        var o = (float[])s.Clone();

        for (int j = 6; j + 7 < o.Length; j += 6)
        {
            float px = o[j], py = o[j + 1];
            float ax = px - o[j - 2], ay = py - o[j - 1];
            float bx = o[j + 2] - px, by = o[j + 3] - py;
            float la = MathF.Sqrt(ax * ax + ay * ay), lb = MathF.Sqrt(bx * bx + by * by);
            if (la < 1e-3f || lb < 1e-3f) continue;
            float cos = (ax * bx + ay * by) / (la * lb);
            if (cos < MathF.Cos(CornerDeg * MathF.PI / 180f)) continue;
            float dx = ax / la + bx / lb, dy = ay / la + by / lb;
            float ld = MathF.Sqrt(dx * dx + dy * dy);
            if (ld < 1e-3f) continue;
            dx /= ld; dy /= ld;
            o[j - 2] = px - dx * la; o[j - 1] = py - dy * la;
            o[j + 2] = px + dx * lb; o[j + 3] = py + dy * lb;
        }

        int n = o.Length;
        if (n >= 8 && MathF.Abs(o[0] - o[n - 2]) < 0.01f && MathF.Abs(o[1] - o[n - 1]) < 0.01f)
        {
            float px = o[0], py = o[1];
            float ax = px - o[n - 4], ay = py - o[n - 3];
            float bx = o[2] - px, by = o[3] - py;
            float la = MathF.Sqrt(ax * ax + ay * ay), lb = MathF.Sqrt(bx * bx + by * by);
            if (la > 1e-3f && lb > 1e-3f
                && (ax * bx + ay * by) / (la * lb) >= MathF.Cos(CornerDeg * MathF.PI / 180f))
            {
                float dx = ax / la + bx / lb, dy = ay / la + by / lb;
                float ld = MathF.Sqrt(dx * dx + dy * dy);
                if (ld > 1e-3f)
                {
                    dx /= ld; dy /= ld;
                    o[n - 4] = px - dx * la; o[n - 3] = py - dy * la;
                    o[2] = px + dx * lb; o[3] = py + dy * lb;
                }
            }
        }
        return o;
    }

    private static float Measure(GraphicsPath p)
    {
        using var probe = (GraphicsPath)p.Clone();
        probe.Flatten(null, 0.15f);
        var pts = probe.PathPoints;
        var kinds = probe.PathTypes;
        float len = 0f;
        for (int i = 1; i < pts.Length; i++)
        {
            if ((kinds[i] & 0x7) == 0) continue;
            len += MathF.Sqrt(MathF.Pow(pts[i].X - pts[i - 1].X, 2) + MathF.Pow(pts[i].Y - pts[i - 1].Y, 2));
        }
        return MathF.Max(len, 0.001f);
    }

    internal static IEnumerable<(char Char, int Stroke, int Numbers)> Strokes()
    {
        foreach (var (c, (_, strokes)) in Hand)
            for (int i = 0; i < strokes.Length; i++)
                yield return (c, i, strokes[i].Length);
    }

    internal static bool Can(string text)
    {
        var map = Built();
        foreach (char c in text)
            if (c != ' ' && !map.ContainsKey(c)) return false;
        return true;
    }

    internal static float Width(string text)
    {
        var map = Built();
        float w = 0f;
        foreach (char c in text)
            w += c == ' ' ? Space : (map.TryGetValue(c, out var gl) ? gl.Advance : 0f) + Track;
        return MathF.Max(w, 1f);
    }

    internal static void Draw(Graphics g, string text, RectangleF box, float written, float alpha,
        Color ink, float weight)
    {
        if (alpha <= 0.004f || written <= 0f || string.IsNullOrEmpty(text)) return;
        var map = Built();

        var strokes = Ink(text, map);
        float total = 0f;
        foreach (var (_, len) in strokes) total += len;
        if (total <= 0f) { foreach (var (pth, _) in strokes) pth.Dispose(); return; }

        float unitsW = Width(text) + Slant * -Top;

        float bottom = Rest;
        foreach (char c in text)
            if (map.TryGetValue(c, out var g2)) bottom = MathF.Max(bottom, g2.Depth + 3f);
        float scale = MathF.Min(box.Width / (unitsW + weight), box.Height / (bottom - Top + weight));

        var save = g.Save();
        try
        {
            g.TranslateTransform(box.X + box.Width / 2f, box.Y + box.Height / 2f);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-unitsW / 2f, -(Top + bottom) / 2f);
            using (var shear = new Matrix(1f, 0f, -Slant, 1f, 0f, 0f)) g.MultiplyTransform(shear);

            using var pen = new Pen(Color.FromArgb((int)(Math.Clamp(alpha, 0f, 1f) * ink.A), ink), weight)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
                DashCap = DashCap.Round,
            };

            float want = total * written, done = 0f;
            foreach (var (path, len) in strokes)
            {
                if (done >= want) break;
                float here = Math.Clamp((want - done) / len, 0f, 1f);
                if (here < 1f)
                {

                    pen.DashPattern = [MathF.Max(0.001f, len * here / weight), len * 2f / weight];
                }
                else pen.DashStyle = DashStyle.Solid;
                g.DrawPath(pen, path);
                done += len;
            }
        }
        finally
        {
            g.Restore(save);
            foreach (var (pth, _) in strokes) pth.Dispose();
        }
    }
}
