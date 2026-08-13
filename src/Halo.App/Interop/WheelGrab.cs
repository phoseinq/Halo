using System;
using System.Runtime.InteropServices;

namespace Halo.Interop;

internal static class WheelGrab
{
    private static Win32.HookProc? _proc;
    private static IntPtr _hook;

        internal static volatile bool WantWheel;

        private static int _notches;

    internal static int TakeNotches() => System.Threading.Interlocked.Exchange(ref _notches, 0);

    internal static void Start()
    {
        if (_hook != IntPtr.Zero) return;
        try
        {
            _proc = Hook;
            _hook = Win32.SetWindowsHookExW(Win32.WH_MOUSE_LL, _proc, IntPtr.Zero, 0);
            if (_hook == IntPtr.Zero) _proc = null;
        }
        catch { _proc = null; _hook = IntPtr.Zero; }
    }

    internal static void Stop()
    {
        if (_hook == IntPtr.Zero) return;
        try { Win32.UnhookWindowsHookEx(_hook); } catch { }
        _hook = IntPtr.Zero;
        _proc = null;
        _notches = 0;
    }

    private static IntPtr Hook(int code, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (code == 0 && (uint)wParam == Win32.WM_MOUSEWHEEL && WantWheel)
            {
                var info = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);

                int delta = (short)(info.mouseData >> 16);
                int notches = delta / Win32.WHEEL_DELTA;
                if (notches == 0) notches = delta > 0 ? 1 : delta < 0 ? -1 : 0;
                if (notches != 0)
                {
                    System.Threading.Interlocked.Add(ref _notches, notches);
                    return new IntPtr(1);
                }
            }
        }
        catch { }
        return Win32.CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }
}
