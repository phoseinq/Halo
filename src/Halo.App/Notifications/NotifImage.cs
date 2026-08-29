using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Halo.Notifications;

internal static class NotifImage
{
    internal const int MaxBytes = 8 * 1024 * 1024;

    internal const int MaxW = 512, MaxH = 288;

    internal static Bitmap? Load(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var file = new FileInfo(path);
            if (!file.Exists || file.Length == 0 || file.Length > MaxBytes) return null;

            byte[] bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes, writable: false);
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            if (source.Width <= 0 || source.Height <= 0) return null;
            return Fit(source);
        }
        catch { return null; }
    }

    private static Bitmap Fit(Image source)
    {

        double scale = Math.Min(1.0, Math.Min((double)MaxW / source.Width, (double)MaxH / source.Height));
        int w = Math.Max(1, (int)Math.Round(source.Width * scale));
        int h = Math.Max(1, (int)Math.Round(source.Height * scale));

        var fitted = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(fitted);

        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(source, new Rectangle(0, 0, w, h));
        return fitted;
    }
}
