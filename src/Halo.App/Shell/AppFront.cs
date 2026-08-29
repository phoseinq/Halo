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

    internal static IntPtr TopLevelFor(IEnumerable<int> pids) => TopLevelFor(pids, null);

    internal static IntPtr TopLevelFor(IEnumerable<int> pids, string? titleHint)
    {
        var wanted = new HashSet<uint>();
        foreach (var pid in pids) if (pid > 4) wanted.Add((uint)pid);
        return wanted.Count == 0 ? IntPtr.Zero : Pick(Candidates(wanted), titleHint);
    }

    internal static IntPtr VerifiedHwnd(long raw, int pid)
    {
        if (raw == 0 || pid <= 4) return IntPtr.Zero;
        try
        {
            var hwnd = new IntPtr(raw);
            if (!Win32.IsWindow(hwnd)) return IntPtr.Zero;
            Win32.GetWindowThreadProcessId(hwnd, out uint owner);
            return owner == (uint)pid ? hwnd : IntPtr.Zero;
        }
        catch { return IntPtr.Zero; }
    }

    internal static IntPtr TopLevelForPid(int pid) => TopLevelForPid(pid, null);

    internal static IntPtr TopLevelForPid(int pid, string? titleHint)
    {
        if (pid <= 4) return IntPtr.Zero;
        var found = Candidates((uint)pid);
        return Pick(found, titleHint);
    }

    internal static string? PathLeaf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var trimmed = path.TrimEnd('\\', '/');
        int cut = trimmed.LastIndexOfAny(['\\', '/']);
        var leaf = cut >= 0 ? trimmed[(cut + 1)..] : trimmed;

        return leaf.Length >= 2 ? leaf : null;
    }

    internal static IntPtr Pick(IReadOnlyList<(IntPtr Handle, string Title)> found, string? titleHint)
    {
        if (found.Count == 0) return IntPtr.Zero;
        if (found.Count == 1) return found[0].Handle;

        string hint = (titleHint ?? "").Trim();
        if (hint.Length == 0) return IntPtr.Zero;

        IntPtr only = IntPtr.Zero;
        foreach (var (handle, title) in found)
        {
            if (title.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (only != IntPtr.Zero) return IntPtr.Zero;
            only = handle;
        }
        return only;
    }

    internal static IReadOnlyList<(IntPtr Handle, string Title)> WindowsOf(int pid)
        => pid <= 4 ? [] : Candidates((uint)pid);

    private static List<(IntPtr Handle, string Title)> Candidates(uint pid)
        => Candidates(new HashSet<uint> { pid });

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

    private static List<(IntPtr Handle, string Title)> Candidates(HashSet<uint> wanted)
    {
        var found = new List<(IntPtr, string)>();
        try
        {
            Win32.EnumWindows((hwnd, _) =>
            {
                try
                {
                    if (!Win32.IsWindowVisible(hwnd)) return true;
                    Win32.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (!wanted.Contains(pid)) return true;
                    int len = Win32.GetWindowTextLengthW(hwnd);
                    if (len == 0) return true;
                    var sb = new System.Text.StringBuilder(len + 1);
                    Win32.GetWindowTextW(hwnd, sb, sb.Capacity);
                    found.Add((hwnd, sb.ToString()));
                }
                catch { }
                return true;
            }, IntPtr.Zero);
        }
        catch { }
        return found;
    }
}
