using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Halo.Interop;

namespace Halo.Launcher;

internal sealed class LauncherBox : IDisposable
{
    private const string ClassName = "HaloLauncherBox";
    private const float OpenSeconds = 0.40f;
    private const float ContentLagSeconds = 0.16f;
    private const float CloseSeconds = 0.16f;
    private const float CaretPeriod = 1.05f;

    private const float ContentGone = ContentLagSeconds / OpenSeconds;

    private static bool _registered;

    private Win32.WndProc? _proc;
    private IntPtr _hwnd;
    private LauncherState? _state;
    private Rectangle _monitor;
    private int _notchBottom;

    private const int GapBelowNotch = 56;

    private const long HoldMs = 150;

    private const long GaugeRefreshMs = 1000;
    private long _gaugesAt;
    private int _centerX, _top;
    private bool _placed;
    private bool _wasDown;
    private long _downAt;
    private int _pressRow = -1;

    private LauncherView.LangHit _pressLang = LauncherView.LangHit.None;
    private int _lastCx = int.MinValue, _lastCy = int.MinValue;
    private bool _dragging;
    private int _grabDX, _grabDY;

    private float _t;
    private float _caretT;
    private bool _opening;
    private bool _shown;
    private int _drawnRows = -1;
    private string? _drawnQuery;
    private int _drawnSel = -1;
    private bool _drawnCaret;
    private int _drawnX = int.MinValue, _drawnY = int.MinValue;
    private int _drawnHot = int.MinValue, _drawnRing = int.MinValue;
    private LauncherView.LangHit _drawnLang = LauncherView.LangHit.None;

    internal static Action<string>? Trace;

    internal event Action<LauncherRow>? Chosen;
    internal event Action? Dismissed;

    internal event Action<string, string>? Submitted;

    internal Func<LauncherRow, bool>? HandledInPlace;

    internal Func<string, Image?>? IconFor;

    internal bool IsOpen => _opening || _t > 0f;

    internal bool Opening => _opening;
    internal LauncherState? State => _state;

    internal void Open(Rectangle monitor, int notchBottom, LauncherState state)
    {
        try
        {
            _monitor = monitor;
            _notchBottom = notchBottom;
            _state = state;
            Ensure();
            if (_hwnd == IntPtr.Zero) { Trace?.Invoke("box open: no hwnd"); return; }

            _opening = true;
            _caretT = 0f;
            _placed = false;
            _dragging = false;
            _wasDown = false;
            _downAt = -1;
            _pressRow = -1;
            _lastCx = _lastCy = int.MinValue;
            _gaugesAt = 0;
            Invalidate();
            Frame(0f);
            Win32.ShowWindow(_hwnd, Win32.SW_SHOW);
            _shown = true;

            Win32.SetForegroundWindow(_hwnd);
        }
        catch (Exception ex) { Trace?.Invoke("box open threw " + ex); }
    }

    internal void Close()
    {
        if (!_opening) return;
        _opening = false;
    }

    internal void Frame(float dt)
    {
        if (_hwnd == IntPtr.Zero || _state is null) return;

        float target = _opening ? 1f : 0f;
        float speed = _opening ? 1f / OpenSeconds : 1f / CloseSeconds;
        _t = Step(_t, target, dt * speed);
        _caretT = (_caretT + dt) % CaretPeriod;
        if (_opening) Mouse();

        if (_opening && _state.ShowGauges && Environment.TickCount64 - _gaugesAt >= GaugeRefreshMs)
        {
            _gaugesAt = Environment.TickCount64;
            if (_state.RefreshGauges()) Invalidate();
        }

        if (!_opening && _t <= ContentGone)
        {
            _t = 0f;
            if (_shown)
            {
                _shown = false;
                try { Win32.ShowWindow(_hwnd, Win32.SW_HIDE); } catch { }
                Invalidate();
            }
            return;
        }

        Paint();
    }

    private void Mouse()
    {
        var state = _state!;
        bool band = state.ShowGauges;
        var (w, h) = BoxSize(_t, state.Rows.Count, LauncherView.BandHeight(state));
        bool down = (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0;
        if (!Win32.GetCursorPos(out var c)) { _wasDown = down; return; }

        int lx = c.X - (_centerX - w / 2), ly = c.Y - _top;
        bool inside = lx >= 0 && lx < w && ly >= 0 && ly < h;
        float bandH = LauncherView.BandHeight(state);
        int overRow = inside ? LauncherView.HitRow(lx, ly, state.Rows.Count, bandH) : -1;
        var overLang = inside && state.ShowLangBar
            ? LauncherView.HitLangBar(lx, ly, w, bandH) : LauncherView.LangHit.None;
        int overGauge = inside ? LauncherView.HitGauge(lx, ly, state.Gauges.Count, band) : -1;
        int overRing = overGauge >= 0
            ? LauncherView.HitRing(lx, ly, overGauge, state.Gauges.Count,
                                   state.Gauges[overGauge].Parts?.Length ?? 0, band)
            : -1;

        if ((c.X != _lastCx || c.Y != _lastCy) && !_dragging)
        {
            if (overRow >= 0) state.SelectAt(overRow);

            if (overGauge != state.HotGauge || overRing != state.HotRing) state.SetHotGauge(overGauge, overRing);
            if (overRow >= 0 || overGauge >= 0 || state.HotGauge >= 0
                || overLang != LauncherView.LangHit.None || _drawnLang != LauncherView.LangHit.None)
                Invalidate();
            _drawnLang = overLang;
        }
        _lastCx = c.X; _lastCy = c.Y;

        if (down && !_wasDown)
        {

            _downAt = inside && LauncherView.InHeader(ly) ? Environment.TickCount64 : -1;
            _pressRow = overRow;
            _pressLang = overLang;
            _grabDX = lx;
            _grabDY = ly;
        }
        else if (down && _downAt > 0 && !_dragging && Environment.TickCount64 - _downAt >= HoldMs)
        {
            _dragging = true;
            Trace?.Invoke($"box picked up after {Environment.TickCount64 - _downAt}ms");
        }

        if (_dragging && down)
        {
            _centerX = c.X - _grabDX + w / 2;
            _top = c.Y - _grabDY;
            var fixedUp = LauncherPlacement.Clamp((_centerX, _top), _monitor, w, h);
            _centerX = fixedUp.CenterX; _top = fixedUp.Top;
        }
        else if (_dragging && !down)
        {
            _dragging = false;
            LauncherPlacement.Save(_centerX, _top);
            Trace?.Invoke($"box dropped at {_centerX},{_top}");
        }
        else if (!down && _wasDown && _pressLang != LauncherView.LangHit.None && _pressLang == overLang)
        {

            if (_pressLang == LauncherView.LangHit.Swap)
                HandledInPlace?.Invoke(new LauncherRow(string.Empty, null, true, LauncherRowKind.Action,
                                                       LauncherPages.ActSwapLangs));
            else
                state.OpenPicker(_pressLang == LauncherView.LangHit.From
                    ? LauncherState.LangPick.From : LauncherState.LangPick.To);
            Invalidate();
        }
        else if (!down && _wasDown && _pressRow >= 0 && _pressRow == overRow)
        {

            var row = state.Activate(overRow);
            if (row is not null) Picked(row);
        }

        if (!down) { _downAt = -1; _pressRow = -1; _pressLang = LauncherView.LangHit.None; }
        _wasDown = down;
    }

    internal static float Step(float from, float to, float k)
    {
        if (k >= 1f) return to;
        float next = from + (to - from) * Math.Clamp(k, 0f, 1f);
        return Math.Abs(to - next) < 0.001f ? to : next;
    }

    internal static (int W, int H) BoxSize(float t, int rows, bool band = false)
        => BoxSize(t, rows, band ? LauncherView.BandH : 0f);

    internal static (int W, int H) BoxSize(float t, int rows, float bandH)
    {
        float e = EaseOutCubic(Math.Clamp(t, 0f, 1f));
        int fullH = LauncherView.Height(rows, bandH);
        return (Math.Max(8, (int)MathF.Round(LauncherView.W * (0.34f + 0.66f * e))),
                Math.Max(4, (int)MathF.Round(fullH * (0.12f + 0.88f * e))));
    }

    private static float EaseOutCubic(float t)
    {
        float u = 1f - t;
        return 1f - u * u * u;
    }

    internal void Invalidate() { _drawnRows = -1; _drawnQuery = null; _drawnSel = -1; _drawnX = int.MinValue; }

    private void Paint()
    {
        var state = _state!;

        float lag = Math.Clamp((_t - ContentLagSeconds / OpenSeconds) / (1f - ContentLagSeconds / OpenSeconds), 0f, 1f);
        bool caretOn = _caretT < CaretPeriod / 2f;

        bool band = state.ShowGauges;
        (int w, int h) = BoxSize(_t, state.Rows.Count, LauncherView.BandHeight(state));

        bool settled = _t >= 0.999f || _t <= 0.001f;
        if (settled && _drawnRows == state.Rows.Count && _drawnQuery == state.Query
            && _drawnSel == state.Selected && _drawnCaret == caretOn
            && _drawnX == _centerX && _drawnY == _top
            && _drawnHot == state.HotGauge && _drawnRing == state.HotRing) return;
        _drawnRows = state.Rows.Count; _drawnQuery = state.Query;
        _drawnSel = state.Selected; _drawnCaret = caretOn;
        _drawnX = _centerX; _drawnY = _top; _drawnHot = state.HotGauge; _drawnRing = state.HotRing;

        var bmi = new Win32.BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
            biWidth = w,
            biHeight = -h,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
        };
        IntPtr screenDc = Win32.GetDC(IntPtr.Zero);
        IntPtr dib = Win32.CreateDIBSection(screenDc, ref bmi, 0, out var bits, IntPtr.Zero, 0);
        IntPtr memDc = Win32.CreateCompatibleDC(screenDc);
        IntPtr oldObj = Win32.SelectObject(memDc, dib);

        using (var bmp = new Bitmap(w, h, w * 4, PixelFormat.Format32bppPArgb, bits))
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);

            LauncherView.Draw(g, w, h, state, lag, IconFor, caretOn);
        }

        if (!_placed)
        {
            var start = LauncherPlacement.Load(_monitor, _notchBottom, GapBelowNotch,
                LauncherView.W, LauncherView.Height(state.Rows.Count, LauncherView.BandHeight(state)));
            _centerX = start.CenterX; _top = start.Top;
            _placed = true;
        }

        var size = new Win32.SIZE { cx = w, cy = h };
        var src = new Win32.POINT { X = 0, Y = 0 };
        var dst = new Win32.POINT
        {
            X = _centerX - w / 2,
            Y = _top,
        };
        var blend = new Win32.BLENDFUNCTION
        {
            BlendOp = Win32.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = Win32.AC_SRC_ALPHA,
        };
        Win32.UpdateLayeredWindow(_hwnd, screenDc, ref dst, ref size, memDc, ref src, 0, ref blend, Win32.ULW_ALPHA);

        Win32.SelectObject(memDc, oldObj);
        Win32.DeleteObject(dib);
        Win32.DeleteDC(memDc);
        Win32.ReleaseDC(IntPtr.Zero, screenDc);
    }

    private void Ensure()
    {
        if (_hwnd != IntPtr.Zero) return;
        var hInstance = Win32.GetModuleHandle(null);
        _proc = WndProc;

        if (!_registered)
        {
            var wc = new Win32.WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<Win32.WNDCLASSEX>(),
                lpfnWndProc = _proc,
                hInstance = hInstance,
                hCursor = Win32.LoadCursor(IntPtr.Zero, Win32.IDC_ARROW),
                lpszClassName = ClassName,
            };
            if (Win32.RegisterClassEx(ref wc) == 0)
            {
                Trace?.Invoke($"box RegisterClassEx failed err={Marshal.GetLastWin32Error()}");
                return;
            }
            _registered = true;
        }

        int ex = Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TOPMOST;
        _hwnd = Win32.CreateWindowEx(ex, ClassName, "Halo Launcher", Win32.WS_POPUP,
            _monitor.X, _notchBottom + GapBelowNotch, LauncherView.W, LauncherView.Height(6),
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero)
        {
            Trace?.Invoke($"box CreateWindowEx failed err={Marshal.GetLastWin32Error()}");
            return;
        }
        Trace?.Invoke($"box created 0x{_hwnd.ToInt64():X}");

        try
        {
            bool shootable = Environment.GetEnvironmentVariable("HALO_CAPTURABLE") == "1";
            Win32.SetWindowDisplayAffinity(_hwnd, shootable ? 0u : Win32.WDA_EXCLUDEFROMCAPTURE);
        }
        catch { }
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case Win32.WM_CHAR when _state is not null:
                    char c = (char)wParam.ToInt32();

                    if (c >= ' ' && c != (char)0x7F) { _state.Type(c); Invalidate(); }
                    return IntPtr.Zero;

                case Win32.WM_KEYDOWN when _state is not null:
                    return Key(wParam.ToInt32());

                case Win32.WM_SETCURSOR when _dragging:
                    Win32.SetCursor(Win32.LoadCursor(IntPtr.Zero, Win32.IDC_SIZEALL));
                    return new IntPtr(1);

                case Win32.WM_KILLFOCUS:
                    Dismissed?.Invoke();
                    return IntPtr.Zero;
            }
        }
        catch { }
        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void Picked(LauncherRow row)
    {
        if (row.Kind == LauncherRowKind.Page && row.Id is { Length: > 0 } id)
        {
            _state!.GoTo(id);
            Invalidate();
            return;
        }
        if (row.Kind == LauncherRowKind.Back)
        {
            _state!.Back();
            Invalidate();
            return;
        }
        if (HandledInPlace?.Invoke(row) == true)
        {
            _state!.Refresh();
            Invalidate();
            return;
        }
        Invalidate();
        Chosen?.Invoke(row);
    }

    private IntPtr Key(int vk)
    {
        var state = _state!;
        switch (vk)
        {
            case Win32.VK_ESCAPE:

                if (state.Back()) { Invalidate(); return IntPtr.Zero; }
                Dismissed?.Invoke();
                return IntPtr.Zero;
            case Win32.VK_BACK:
                state.Backspace(); Invalidate();
                return IntPtr.Zero;
            case Win32.VK_TAB:
                switch (state.Tab())
                {
                    case LauncherState.TabResult.CycleTranslatePair:

                        state.OpenPicker(LauncherState.LangPick.To);
                        Invalidate();
                        break;
                    case LauncherState.TabResult.Changed:
                        Invalidate();
                        break;
                }
                return IntPtr.Zero;
            case Win32.VK_UP:
                state.Move(-1); Invalidate();
                return IntPtr.Zero;
            case Win32.VK_DOWN:
                state.Move(1); Invalidate();
                return IntPtr.Zero;
            case Win32.VK_RETURN:

                if (state.Page is { } pg && LauncherState.PageTakesText(pg)
                    && state.Query.Trim().Length > 0)
                {
                    Submitted?.Invoke(pg, state.Query.Trim());
                    return IntPtr.Zero;
                }

                if (state.Enter() is { } row) Picked(row);
                return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_hwnd == IntPtr.Zero) return;
        try { Win32.DestroyWindow(_hwnd); } catch { }
        _hwnd = IntPtr.Zero;
    }
}
