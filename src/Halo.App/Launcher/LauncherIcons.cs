using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace Halo.Launcher;

internal static class LauncherIcons
{
    private static readonly Dictionary<string, Image?> _have = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _asked = new(StringComparer.OrdinalIgnoreCase);

    internal static Action? Arrived;

    internal static Image? Get(string? aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return null;
        lock (_have)
        {
            if (_have.TryGetValue(aumid, out var img)) return img;
            if (!_asked.Add(aumid)) return null;
        }

        ThreadPool.QueueUserWorkItem(_ =>
        {
            Image? bmp = null;
            try { bmp = Halo.Notifications.ShellIcon.ForAumid(aumid); } catch { }
            lock (_have) _have[aumid] = bmp;
            if (bmp is not null) { try { Arrived?.Invoke(); } catch { } }
        });
        return null;
    }
}
