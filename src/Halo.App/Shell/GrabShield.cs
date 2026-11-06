using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Halo.Interop;

namespace Halo.Shell;

internal sealed class GrabShield
{
    private IntPtr _hwnd;
    private Win32.WndProc? _proc;
    private Rectangle _zone, _hole;
    private bool _shown;
    private long _zAt;

    private void Ensure()
    {
        if (_hwnd != IntPtr.Zero) return;
        var hInstance = Win32.GetModuleHandle(null);
        _proc = (h, m, w, l) => Win32.DefWindowProc(h, m, w, l);
        var wc = new Win32.WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<Win32.WNDCLASSEX>(),
            lpfnWndProc = _proc,
            hInstance = hInstance,
            hCursor = Win32.LoadCursor(IntPtr.Zero, Win32.IDC_ARROW),
            lpszClassName = "HaloGrabShield",
        };
        Win32.RegisterClassEx(ref wc);
        int ex = Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TOPMOST | Win32.WS_EX_NOACTIVATE;
        _hwnd = Win32.CreateWindowEx(ex, "HaloGrabShield", "", Win32.WS_POPUP, 0, 0, 1, 1,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd != IntPtr.Zero) Win32.SetWindowDisplayAffinity(_hwnd, Win32.WDA_EXCLUDEFROMCAPTURE);
    }

        internal void Update(Rectangle? zone, Rectangle hole, IntPtr above)
    {
        try
        {
            if (zone is not { Width: > 0, Height: > 0 } z)
            {
                if (_shown && _hwnd != IntPtr.Zero) Win32.ShowWindow(_hwnd, Win32.SW_HIDE);
                _shown = false;
                return;
            }
            Ensure();
            if (_hwnd == IntPtr.Zero) return;
            if (z != _zone || hole != _hole || !_shown)
            {
                _zone = z;
                _hole = hole;
                Paint(z, hole);
            }
            bool fresh = !_shown;
            if (!_shown)
            {
                Win32.ShowWindow(_hwnd, Win32.SW_SHOWNOACTIVATE);
                _shown = true;
            }

            long now = Environment.TickCount64;
            if (fresh || now - _zAt > 1000)
            {
                _zAt = now;
                Win32.SetWindowPos(_hwnd, above, 0, 0, 0, 0,
                    Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            }
        }
        catch { }
    }

    private void Paint(Rectangle z, Rectangle hole)
    {
        using var bmp = new Bitmap(z.Width, z.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(1, 0, 0, 0));
            var h = Rectangle.Intersect(z, hole);
            if (!h.IsEmpty)
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                using var clear = new SolidBrush(Color.FromArgb(0, 0, 0, 0));
                g.FillRectangle(clear, h.X - z.X, h.Y - z.Y, h.Width, h.Height);
            }
        }
        IntPtr screen = Win32.GetDC(IntPtr.Zero), mem = Win32.CreateCompatibleDC(screen);
        IntPtr hbmp = bmp.GetHbitmap(Color.FromArgb(0)), old = Win32.SelectObject(mem, hbmp);
        try
        {
            var size = new Win32.SIZE { cx = z.Width, cy = z.Height };
            var src = new Win32.POINT { X = 0, Y = 0 };
            var dst = new Win32.POINT { X = z.X, Y = z.Y };
            var blend = new Win32.BLENDFUNCTION
            {
                BlendOp = Win32.AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = Win32.AC_SRC_ALPHA,
            };
            Win32.UpdateLayeredWindow(_hwnd, screen, ref dst, ref size, mem, ref src, 0, ref blend, Win32.ULW_ALPHA);
        }
        finally
        {
            Win32.SelectObject(mem, old);
            Win32.DeleteObject(hbmp);
            Win32.DeleteDC(mem);
            Win32.ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
