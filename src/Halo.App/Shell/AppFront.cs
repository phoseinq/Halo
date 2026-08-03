using System;
using System.Collections.Generic;
using Halo.Interop;

namespace Halo.Shell;

internal static class AppFront
{
    internal static bool Front(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        try
        {
            Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
            uint fore = Win32.GetWindowThreadProcessId(Win32.GetForegroundWindow(), out _);
            uint self = Win32.GetCurrentThreadId();
            bool attached = fore != 0 && fore != self && Win32.AttachThreadInput(fore, self, true);
            Win32.SetForegroundWindow(hwnd);
            if (attached) Win32.AttachThreadInput(fore, self, false);
            return Win32.GetForegroundWindow() == hwnd;
        }
        catch { return false; }
    }

    internal static IntPtr TopLevelFor(IEnumerable<int> pids)
    {
        var wanted = new HashSet<uint>();
        foreach (var pid in pids) if (pid > 4) wanted.Add((uint)pid);
        return wanted.Count == 0 ? IntPtr.Zero : Search(wanted);
    }

    internal static IntPtr TopLevelForPid(int pid)
        => pid > 4 ? Search([(uint)pid]) : IntPtr.Zero;

    internal static IntPtr TopLevelForProcess(string name)
    {
        if (string.IsNullOrEmpty(name)) return IntPtr.Zero;
        try
        {
            foreach (var process in System.Diagnostics.Process.GetProcessesByName(name))
            {
                using (process)
                {
                    if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
                }
            }
        }
        catch { }
        return IntPtr.Zero;
    }

    private static IntPtr Search(HashSet<uint> wanted)
    {
        IntPtr found = IntPtr.Zero;
        try
        {
            Win32.EnumWindows((hwnd, _) =>
            {
                try
                {
                    if (!Win32.IsWindowVisible(hwnd)) return true;
                    Win32.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (!wanted.Contains(pid)) return true;
                    if (Win32.GetWindowTextLengthW(hwnd) == 0) return true;
                    found = hwnd;
                    return false;
                }
                catch { return true; }
            }, IntPtr.Zero);
        }
        catch { }
        return found;
    }
}
