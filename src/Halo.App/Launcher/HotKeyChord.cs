using System;
using System.Collections.Generic;

namespace Halo.Launcher;

internal readonly record struct HotKeyChord(uint Mods, uint Vk)
{
    internal const uint ModAlt = 0x0001, ModControl = 0x0002, ModShift = 0x0004, ModWin = 0x0008;

    internal static HotKeyChord Default => new(ModAlt, 0x20);

    private static readonly Dictionary<string, uint> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20, ["Enter"] = 0x0D, ["Tab"] = 0x09, ["Esc"] = 0x1B,
        ["Backspace"] = 0x08, ["Insert"] = 0x2D, ["Delete"] = 0x2E,
        ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
        ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
    };

    private static readonly Dictionary<uint, string> Names = BuildNames();

    private static Dictionary<uint, string> BuildNames()
    {
        var map = new Dictionary<uint, string>();
        foreach (var (name, code) in Keys) map[code] = name;
        return map;
    }

    internal static bool TryParse(string? text, out HotKeyChord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        uint mods = 0, vk = 0;
        foreach (string raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            string part = raw.Trim();
            if (part.Length == 0) return false;

            if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) { mods |= ModAlt; continue; }
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
             || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) { mods |= ModControl; continue; }
            if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) { mods |= ModShift; continue; }
            if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)) { mods |= ModWin; continue; }

            if (vk != 0) return false;

            if (Keys.TryGetValue(part, out uint named)) { vk = named; continue; }
            if (part.Length == 1 && char.IsLetterOrDigit(part[0])) { vk = char.ToUpperInvariant(part[0]); continue; }
            if (part.Length is 2 or 3 && (part[0] is 'F' or 'f')
                && int.TryParse(part.AsSpan(1), out int fn) && fn is >= 1 and <= 24)
            { vk = (uint)(0x70 + fn - 1); continue; }

            return false;
        }

        if (mods == 0 || vk == 0) return false;
        chord = new HotKeyChord(mods, vk);
        return true;
    }

    internal static string Describe(uint mods)
    {
        var parts = new List<string>(4);
        if ((mods & ModControl) != 0) parts.Add("Ctrl");
        if ((mods & ModAlt) != 0) parts.Add("Alt");
        if ((mods & ModShift) != 0) parts.Add("Shift");
        if ((mods & ModWin) != 0) parts.Add("Win");
        return string.Join("+", parts);
    }

    internal string Format()
    {
        var parts = new List<string>(5);
        if ((Mods & ModControl) != 0) parts.Add("Ctrl");
        if ((Mods & ModAlt) != 0) parts.Add("Alt");
        if ((Mods & ModShift) != 0) parts.Add("Shift");
        if ((Mods & ModWin) != 0) parts.Add("Win");

        if (Names.TryGetValue(Vk, out var named)) parts.Add(named);
        else if (Vk is >= 0x70 and <= 0x87) parts.Add("F" + (Vk - 0x70 + 1));
        else parts.Add(((char)Vk).ToString());
        return string.Join("+", parts);
    }
}
