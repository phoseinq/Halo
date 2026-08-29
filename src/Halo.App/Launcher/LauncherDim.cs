using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Halo.Interop;

namespace Halo.Launcher;

internal sealed class LauncherDim : IDisposable
{
    private const string ClassName = "HaloLauncherDim";
    private static bool _registered;
    private static Win32.WndProc? _proc;

    private IntPtr _hwnd;
    private byte _alpha;

    internal static Action<string>? Trace;

    internal IntPtr Hwnd => _hwnd;
    internal bool Visible { get; private set; }

    internal void Show(Rectangle monitor)
    {
        try
        {
            Ensure(monitor);
            if (_hwnd == IntPtr.Zero) return;
            Win32.SetWindowPos(_hwnd, Win32.HWND_TOPMOST, monitor.X, monitor.Y,
                monitor.Width, monitor.Height, Win32.SWP_NOACTIVATE);
            SetAlpha(0);
            Win32.ShowWindow(_hwnd, Win32.SW_SHOWNOACTIVATE);
            Visible = true;
        }
        catch (Exception ex) { Trace?.Invoke("dim show threw " + ex); }
    }

    internal void SetAlpha(byte a)
    {
        if (_hwnd == IntPtr.Zero || a == _alpha) return;
        _alpha = a;
        try { Win32.SetLayeredWindowAttributes(_hwnd, 0, a, Win32.LWA_ALPHA); } catch { }
    }

    internal void Hide()
    {
        if (_hwnd == IntPtr.Zero) return;
        try { Win32.ShowWindow(_hwnd, Win32.SW_HIDE); } catch { }
        Visible = false;
        _alpha = 0;
    }

    private void Ensure(Rectangle monitor)
    {
        if (_hwnd != IntPtr.Zero) return;
        var hInstance = Win32.GetModuleHandle(null);

        if (!_registered)
        {
            _proc = (h, m, w, l) => Win32.DefWindowProc(h, m, w, l);
            var wc = new Win32.WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<Win32.WNDCLASSEX>(),
                lpfnWndProc = _proc,
                hInstance = hInstance,
                hCursor = Win32.LoadCursor(IntPtr.Zero, Win32.IDC_ARROW),

                hbrBackground = Win32.CreateSolidBrush(0x000000),
                lpszClassName = ClassName,
            };
            if (Win32.RegisterClassEx(ref wc) == 0)
            {
                Trace?.Invoke($"dim RegisterClassEx failed err={Marshal.GetLastWin32Error()}");
                return;
            }
            _registered = true;
        }

        int ex = Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TOPMOST | Win32.WS_EX_NOACTIVATE;
        _hwnd = Win32.CreateWindowEx(ex, ClassName, "Halo Launcher", Win32.WS_POPUP,
            monitor.X, monitor.Y, monitor.Width, monitor.Height,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            Trace?.Invoke($"dim CreateWindowEx failed err={Marshal.GetLastWin32Error()}");
            return;
        }
        Trace?.Invoke($"dim created 0x{_hwnd.ToInt64():X}");

        try
        {
            bool shootable = Environment.GetEnvironmentVariable("HALO_CAPTURABLE") == "1";
            Win32.SetWindowDisplayAffinity(_hwnd, shootable ? 0u : Win32.WDA_EXCLUDEFROMCAPTURE);
        }
        catch { }
        try { Win32.SetLayeredWindowAttributes(_hwnd, 0, 0, Win32.LWA_ALPHA); } catch { }
    }

    public void Dispose()
    {
        if (_hwnd == IntPtr.Zero) return;
        try { Win32.DestroyWindow(_hwnd); } catch { }
        _hwnd = IntPtr.Zero;
        Visible = false;
    }
}
