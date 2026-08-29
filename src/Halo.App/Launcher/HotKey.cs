using System;
using System.IO;
using Halo.Interop;

namespace Halo.Launcher;

internal sealed class HotKey : IDisposable
{

    internal const int Id = 0x4841;

    internal const int HideId = 0x4842;

    private readonly IntPtr _hwnd;
    private readonly int _id;
    private bool _held;

    internal HotKey(IntPtr hwnd, int id) { _hwnd = hwnd; _id = id; }

    internal bool Held => _held;

    internal bool Register(HotKeyChord chord)
    {
        Unregister();
        try { _held = Win32.RegisterHotKey(_hwnd, _id, chord.Mods, chord.Vk); }
        catch { _held = false; }
        WriteState(_held);
        return _held;
    }

    internal void Unregister()
    {
        if (!_held) return;
        try { Win32.UnregisterHotKey(_hwnd, _id); } catch { }
        _held = false;
    }

    internal static string StatePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Halo", "launcher-hotkey.txt");

    internal static void WriteState(bool ok)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, ok ? "ok" : "taken");
        }
        catch { }
    }

    internal static bool ReadTaken()
    {
        try
        {
            return File.Exists(StatePath)
                && File.ReadAllText(StatePath).Trim().Equals("taken", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public void Dispose() => Unregister();
}
