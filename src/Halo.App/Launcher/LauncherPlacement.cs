using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace Halo.Launcher;

internal static class LauncherPlacement
{
    internal static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "launcher-pos");

    internal static (int CenterX, int Top) Default(Rectangle monitor, int notchBottom, int gap)
        => (monitor.X + monitor.Width * 3 / 4, notchBottom + gap);

    internal static string Format(int centerX, int top)
        => centerX.ToString(CultureInfo.InvariantCulture) + "," + top.ToString(CultureInfo.InvariantCulture);

    internal static (int CenterX, int Top)? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Trim().Split(',');
        if (parts.Length != 2) return null;
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cx)) return null;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)) return null;
        return (cx, y);
    }

    internal static (int CenterX, int Top) Clamp((int CenterX, int Top) p, Rectangle monitor, int w, int h)
    {

        int half = w / 2;
        int cx = w >= monitor.Width
            ? monitor.X + monitor.Width / 2
            : Math.Clamp(p.CenterX, monitor.X + half, monitor.Right - half);

        int top = h >= monitor.Height
            ? monitor.Y
            : Math.Clamp(p.Top, monitor.Y, monitor.Bottom - h);
        return (cx, top);
    }

    internal static (int CenterX, int Top) Load(Rectangle monitor, int notchBottom, int gap, int w, int h)
    {
        try
        {
            if (File.Exists(DefaultPath))
            {
                var got = Parse(File.ReadAllText(DefaultPath));
                if (got is not null) return Clamp(got.Value, monitor, w, h);
            }
        }
        catch { }
        return Clamp(Default(monitor, notchBottom, gap), monitor, w, h);
    }

    internal static void Save(int centerX, int top)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath)!);
            File.WriteAllText(DefaultPath, Format(centerX, top));
        }
        catch { }
    }
}
