using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using Halo.Interop;

namespace Halo.Launcher;

internal sealed class LauncherGlass : IDisposable
{
    internal const int Scale = 4;

    private const float Brightness = 0.52f;
    private const float Saturation = 1.05f;

    private Bitmap? _plate;
    private TextureBrush? _brush;
    private Rectangle _monitor;

    private Bitmap? _pending;
    internal bool HasPending => Volatile.Read(ref _pending) is not null;

    internal void Snap(Rectangle monitor, int tint)
    {
        _monitor = monitor;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Bitmap? shot = null;
            try
            {
                shot = Grab(monitor);
                if (shot is null) return;
                Interlocked.Exchange(ref _pending, Compose(shot, tint))?.Dispose();
            }
            catch { }
            finally { shot?.Dispose(); }
        });
    }

        internal void Use(Bitmap plate, Rectangle monitor)
    {
        DisposeBrush();
        _plate = plate;
        _monitor = monitor;
        _brush = new TextureBrush(plate, WrapMode.Clamp);
    }

    private static Bitmap? Grab(Rectangle m)
    {
        int w = Math.Max(1, m.Width / Scale), h = Math.Max(1, m.Height / Scale);
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        IntPtr screen = Win32.GetDC(IntPtr.Zero);
        try
        {
            using var g = Graphics.FromImage(bmp);
            IntPtr dc = g.GetHdc();
            try
            {

                Win32.SetStretchBltMode(dc, Win32.HALFTONE);
                Win32.StretchBlt(dc, 0, 0, w, h, screen, m.X, m.Y, m.Width, m.Height, Win32.SRCCOPY);
            }
            finally { g.ReleaseHdc(dc); }
            return bmp;
        }
        catch { bmp.Dispose(); return null; }
        finally { Win32.ReleaseDC(IntPtr.Zero, screen); }
    }

    internal static Bitmap Compose(Bitmap source, int tint)
    {
        int w = source.Width, h = source.Height;
        using var tiny = Resized(source, Math.Max(1, w / 4), Math.Max(1, h / 4), InterpolationMode.HighQualityBilinear);
        using var small = Resized(tiny, Math.Max(1, w / 2), Math.Max(1, h / 2), InterpolationMode.HighQualityBicubic);

        var plate = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(plate);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        using (var attrs = new ImageAttributes())
        {
            attrs.SetColorMatrix(Vivid());
            attrs.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(small, new Rectangle(0, 0, w, h), 0, 0, small.Width, small.Height, GraphicsUnit.Pixel, attrs);
        }
        using var wash = new SolidBrush(Color.FromArgb(Math.Clamp(tint, 0, 255), 13, 15, 24));
        g.FillRectangle(wash, 0, 0, w, h);
        return plate;
    }

    private static Bitmap Resized(Bitmap src, int w, int h, InterpolationMode mode)
    {
        var to = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(to);
        g.InterpolationMode = mode;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        using var attrs = new ImageAttributes();
        attrs.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
        return to;
    }

    private static ColorMatrix Vivid()
    {
        const float lr = 0.2126f, lg = 0.7152f, lb = 0.0722f;
        float s = Saturation, b = Brightness, k = 1f - s;
        return new ColorMatrix(new[]
        {
            new[] { (k * lr + s) * b, k * lr * b,       k * lr * b,       0f, 0f },
            new[] { k * lg * b,       (k * lg + s) * b, k * lg * b,       0f, 0f },
            new[] { k * lb * b,       k * lb * b,       (k * lb + s) * b, 0f, 0f },
            new[] { 0f,               0f,               0f,               1f, 0f },
            new[] { 0f,               0f,               0f,               0f, 1f },
        });
    }

    internal bool Fill(Graphics g, GraphicsPath shape, Point boxOnScreen)
    {
        if (Interlocked.Exchange(ref _pending, null) is { } fresh) Use(fresh, _monitor);
        if (_brush is null) return false;
        try
        {
            using var m = new Matrix(Scale, 0, 0, Scale, _monitor.X - boxOnScreen.X, _monitor.Y - boxOnScreen.Y);
            _brush.Transform = m;
            g.FillPath(_brush, shape);
            return true;
        }
        catch { return false; }
    }

    private void DisposeBrush()
    {
        _brush?.Dispose(); _brush = null;
        _plate?.Dispose(); _plate = null;
    }

    public void Dispose()
    {
        DisposeBrush();
        Interlocked.Exchange(ref _pending, null)?.Dispose();
    }
}
