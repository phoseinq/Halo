using System;
using System.Collections.Generic;

namespace Halo.Launcher;

internal static class QuickActions
{
        internal readonly record struct Builtin(string Id, string Label, string Detail, string Glyph);

    internal const string IdMute = "mute";
    internal const string IdLock = "lock";
    internal const string IdSleep = "sleep";
    internal const string IdDownloads = "downloads";
    internal const string IdDesktop = "desktop";
    internal const string IdRecycle = "recycle";
    internal const string IdTheme = "theme";
    internal const string IdSettings = "settings";

    internal static readonly Builtin[] All =
    [
        new(IdMute, "Mute", "toggle system volume", "\uE74F"),
        new(IdLock, "Lock Screen", "sign out to the lock screen", "\uE72E"),
        new(IdSleep, "Sleep", "suspend the machine", "\uE708"),
        new(IdDownloads, "Downloads", "open the Downloads folder", "\uE896"),
        new(IdDesktop, "Show Desktop", "minimise everything", "\uE7F4"),
        new(IdRecycle, "Empty Recycle Bin", "Windows still asks first", "\uE74D"),
        new(IdTheme, "Switch Light/Dark", "flip the Windows app theme", "\uE706"),
        new(IdSettings, "Halo Settings", "open the settings panel", "\uE713"),
    ];

        internal static string EnabledKey(string id) => "quick." + id;

        internal static string CustomKey(int slot) => "quick.custom" + slot.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal const int CustomSlots = 3;

        internal const string Prefix = "act:";
    internal const string CustomPrefix = "act:custom:";

    internal static bool DefaultOn(string id) => id is IdMute or IdLock or IdSleep;

    internal static (string Label, string Target)? ParseCustom(string? line)
    {
        string s = (line ?? "").Trim();
        if (s.Length == 0) return null;
        int bar = s.IndexOf('|');
        if (bar < 0)
        {
            string only = s.Trim();
            return only.Length == 0 ? null : (Short(only), only);
        }
        string label = s[..bar].Trim();
        string target = s[(bar + 1)..].Trim();
        if (target.Length == 0) return null;
        return (label.Length == 0 ? Short(target) : label, target);
    }

        private static string Short(string target)
    {
        try
        {
            if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return new Uri(target).Host;
            string name = System.IO.Path.GetFileName(target.TrimEnd('\\', '/'));
            return name.Length > 0 ? name : target;
        }
        catch { return target; }
    }

    internal static IReadOnlyList<Builtin> Enabled(Func<string, bool> on)
    {
        var kept = new List<Builtin>();
        foreach (var b in All) if (on(b.Id)) kept.Add(b);
        return kept;
    }
}
