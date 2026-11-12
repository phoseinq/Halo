using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Halo.Interop;

namespace Halo.Launcher;

internal sealed class LauncherBox : IDisposable
{
    private const string ClassName = "HaloLauncherBox";
    private const float OpenSeconds = 0.20f;
    private const float ContentLagSeconds = 0.05f;
    private const float CloseSeconds = 0.11f;
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
    private LauncherGlass? _glass;
    private RectangleF _hl;
    private bool _hlSet;
    private string? _hlKey;
    private float _shownH;
    private int _drawnH = -1;
    private int _filesSeen;
    private int _clipSeen;
    private bool _pressClear;
    private long _clipAt;

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

    internal void Open(Rectangle monitor, int notchBottom, LauncherState state, LauncherGlass? glass = null)
    {
        try
        {
            _glass = glass;
            _hlSet = false;
            _shownH = 0f;
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

        internal void Warm(Rectangle monitor, int notchBottom)
    {
        try
        {
            _monitor = monitor;
            _notchBottom = notchBottom;
            Ensure();
        }
        catch { }
    }

    internal void Close()
    {
        if (!_opening) return;
        _opening = false;
    }

    internal void Frame(float dt)
    {
        if (_hwnd == IntPtr.Zero || _state is null) return;

        if (_opening && _t < 1f) dt = Math.Min(dt, 1f / 60f);
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

        bool onClipboard = _state.Page is LauncherState.PageClipboard or LauncherState.PageClipPins;
        if (_opening && onClipboard && Environment.TickCount64 - _clipAt >= ClipPollMs)
        {
            _clipAt = Environment.TickCount64;
            ClipboardHistory.RefreshSoon();
        }
        if (_opening && ClipboardHistory.Version != _clipSeen)
        {
            _clipSeen = ClipboardHistory.Version;
            if (onClipboard)
            {
                if (_state.Query.Length == 0) _state.Refresh(); else _state.RefreshKeeping();
                Invalidate();
            }
        }

        if (_opening && FileSearch.Version != _filesSeen)
        {
            _filesSeen = FileSearch.Version;
            if (_state.Page is null && _state.Query.Length > 0) { _state.RefreshKeeping(); Invalidate(); }
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

        Ease(dt);
        Paint();
    }

    private const int PageStep = 5;
    private const long ClipPollMs = 500;
    private const float HighlightSeconds = 0.075f, HeightSeconds = 0.045f;

    private void Ease(float dt)
    {
        var state = _state!;
        var frame = LauncherLayout.Of(state);
        var target = state.Selected >= 0 && state.Selected < frame.Cells.Length ? frame.Cells[state.Selected] : RectangleF.Empty;
        string key = (state.Page ?? "") + "|" + state.Picking;
        float k = Math.Clamp(dt / HighlightSeconds, 0f, 1f);
        if (!_hlSet || key != _hlKey || _t < 0.999f || target.IsEmpty || _hl.IsEmpty)
        {
            _hl = target;
            _hlSet = true;
            _hlKey = key;
        }
        else
        {
            _hl = new RectangleF(Near(_hl.X, target.X, k), Near(_hl.Y, target.Y, k),
                                 Near(_hl.Width, target.Width, k), Near(_hl.Height, target.Height, k));
        }

        float full = frame.Height;
        if (_t < 0.999f || _shownH <= 0f) _shownH = full;
        else _shownH = Near(_shownH, full, Math.Clamp(dt / HeightSeconds, 0f, 1f));
    }

    private static float Near(float from, float to, float k)
    {
        float next = from + (to - from) * k;
        return Math.Abs(to - next) < 0.4f ? to : next;
    }

    private void Mouse()
    {
        var state = _state!;
        var frame = LauncherLayout.Of(state);
        var (w, h) = BoxSizeFor(_t, (int)frame.Height);
        bool down = (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0;
        if (!Win32.GetCursorPos(out var c)) { _wasDown = down; return; }

        int lx = c.X - (_centerX - w / 2), ly = c.Y - _top;
        bool inside = lx >= 0 && lx < w && ly >= 0 && ly < h;
        float bandH = LauncherView.BandHeight(state);
        int overRow = inside ? LauncherLayout.Hit(frame, lx, ly) : -1;

        var rowHit = overRow >= 0 ? state.Rows[overRow] : null;
        var cell = overRow >= 0 ? frame.Cells[overRow] : RectangleF.Empty;
        bool overCircle = rowHit is { Kind: LauncherRowKind.Tick } && Math.Abs(lx - LauncherView.IconAxis) <= 14;
        bool overTime = rowHit?.Id?.StartsWith(LauncherPages.AddPrefix, StringComparison.Ordinal) == true && lx >= cell.Right - 104f;
        string hitId = rowHit?.Id ?? "";
        bool hitVault = hitId.StartsWith(ClipboardHistory.VaultPrefix, StringComparison.Ordinal);
        bool hitClip = hitVault || hitId.StartsWith(ClipboardHistory.ActPrefix, StringComparison.Ordinal)
                       || hitId.StartsWith(ClipboardHistory.PinPrefix, StringComparison.Ordinal);
        bool overFar = hitClip && lx >= cell.Right - LauncherView.ClipOneButton;
        bool overLock = hitClip && !hitVault && !overFar && lx >= cell.Right - LauncherView.ClipLockEdge;
        bool overDrop = hitClip && !hitVault && !overFar && !overLock && lx >= cell.Right - LauncherView.ClipThreeButtons;
        bool overRun = rowHit is { Detail: "run" } && !overFar && !overLock && !overDrop && lx >= cell.Right - 154f;

        bool overClear = inside && state.ShowClearAll && LauncherLayout.ClearCell.Contains(lx, ly);
        if (overClear != state.ClearHot || (!overClear && state.ClearArmed))
        {
            state.SetClear(overClear, overClear && state.ClearArmed);
            Invalidate();
        }
        int tickHot = overCircle ? overRow : -1;
        if (tickHot != state.HotTick) { state.SetHotTick(tickHot); Invalidate(); }

        var overLang = inside && state.ShowLangBar
            ? LauncherView.HitLangBar(lx, ly, w, bandH) : LauncherView.LangHit.None;
        int overGauge = inside && state.ShowGauges
            ? LauncherLayout.HitBand(frame, lx, ly, state.Gauges.Count) : -1;
        const int overRing = -1;

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

            _downAt = inside && LauncherView.InHeader(ly) && overRow < 0 && !overClear ? Environment.TickCount64 : -1;
            _pressClear = overClear;
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
        else if (!down && _wasDown && _pressClear && overClear)
        {
            if (state.ClearArmed)
            {
                HandledInPlace?.Invoke(new LauncherRow(string.Empty, null, true, LauncherRowKind.Action, ClipboardHistory.ClearAll));
                state.SetClear(overClear, false);
                state.Refresh();
            }
            else state.SetClear(true, true);
            Invalidate();
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

            if (row is { Kind: LauncherRowKind.Tick } && !overCircle) { EditReminder(row); row = null; }
            if (row is not null && overTime) { state.EditChoice(row); Invalidate(); row = null; }
            if (row is not null && overFar) { ClipButton(row, lockIt: false); row = null; }
            if (row is not null && overLock) { ClipButton(row, lockIt: true); row = null; }
            if (row is not null && overDrop) { Picked(row with { Id = ClipboardHistory.DropPrefix + row.Id, Kind = LauncherRowKind.Action }); row = null; }
            if (row is not null && overRun) Run(row);
            else if (row is not null) Picked(row);
        }

        if (!down) { _downAt = -1; _pressRow = -1; _pressLang = LauncherView.LangHit.None; _pressClear = false; }
        _wasDown = down;
    }

    internal static float Step(float from, float to, float k)
    {
        if (k >= 1f) return to;
        float next = from + (to - from) * Math.Clamp(k, 0f, 1f);
        return Math.Abs(to - next) < 0.001f ? to : next;
    }

    internal static (int W, int H) BoxSize(float t, int rows, bool band = false)
        => BoxSizeFor(t, LauncherView.Height(rows, band ? LauncherView.BandH : 0f));

    internal static (int W, int H) BoxSizeFor(float t, int fullH)
    {
        float e = EaseOutCubic(Math.Clamp(t, 0f, 1f));
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

        var frame = LauncherLayout.Of(state);
        (int w, int h) = BoxSizeFor(_t, (int)frame.Height);
        if (_t >= 0.999f && _shownH > 0f) h = (int)MathF.Round(_shownH);
        var hlTarget = state.Selected >= 0 && state.Selected < frame.Cells.Length ? frame.Cells[state.Selected] : RectangleF.Empty;

        bool settled = _t >= 0.999f || _t <= 0.001f;
        if (settled && _drawnRows == state.Rows.Count && _drawnQuery == state.Query
            && _drawnSel == state.Selected && _drawnCaret == caretOn
            && _drawnX == _centerX && _drawnY == _top
            && _drawnHot == state.HotGauge && _drawnRing == state.HotRing
            && _drawnH == h && _hl == hlTarget && _glass?.HasPending != true) return;
        _drawnH = h;
        _drawnRows = state.Rows.Count; _drawnQuery = state.Query;
        _drawnSel = state.Selected; _drawnCaret = caretOn;
        _drawnX = _centerX; _drawnY = _top; _drawnHot = state.HotGauge; _drawnRing = state.HotRing;

        if (!_placed)
        {
            var start = LauncherPlacement.Load(_monitor, _notchBottom, GapBelowNotch, LauncherView.W, (int)frame.Height);
            _centerX = start.CenterX; _top = start.Top;
            _placed = true;
        }

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

            LauncherView.Draw(g, w, h, state, lag, IconFor, caretOn, _glass,
                              new Point(_centerX - w / 2, _top), _hl.IsEmpty ? null : _hl);
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

            SourceConstantAlpha = _glass is null ? (byte)255
                : (byte)Math.Clamp((_opening ? Math.Min(1f, _t / 0.3f) : lag) * 255f, 0f, 255f),
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

    private void EditReminder(LauncherRow row)
    {
        string id = (row.Id ?? "")[ReminderStore.ActPrefix.Length..];
        foreach (var r in ReminderStore.Load())
            if (r.Id == id)
            {
                _state!.EditReminder(r);
                Invalidate();
                return;
            }
    }

    private void ClipButton(LauncherRow row, bool lockIt)
    {
        string id = row.Id ?? "";
        string act = id.StartsWith(ClipboardHistory.VaultPrefix, StringComparison.Ordinal)
            ? ClipboardHistory.VaultDropPrefix + id[ClipboardHistory.VaultPrefix.Length..]
            : lockIt ? ClipboardHistory.LockPrefix + id
            : ClipboardHistory.FlipPrefix + (row.Pin ? "1" : "0") + id;
        Picked(row with { Id = act, Kind = LauncherRowKind.Action });
    }

    private void Run(LauncherRow row)
    {
        string id = (row.Id ?? "")[ClipboardHistory.ActPrefix.Length..];
        Chosen?.Invoke(row with { Id = ClipboardHistory.RunPrefix + id });
    }

    private IntPtr Key(int vk)
    {
        var state = _state!;
        switch (vk)
        {
            case Win32.VK_ESCAPE:

                if (state.Query.Length > 0 && state.Picking == LauncherState.LangPick.None
                    && state.Page != LauncherState.PageReminderEdit)
                {
                    state.Reset();
                    Invalidate();
                    return IntPtr.Zero;
                }

                if (state.Back()) { Invalidate(); return IntPtr.Zero; }
                Dismissed?.Invoke();
                return IntPtr.Zero;
            case Win32.VK_BACK:
                if ((Win32.GetKeyState(Win32.VK_CONTROL) & 0x8000) != 0 && state.Query.Length > 0) state.Reset();
                else state.BackOrErase();
                Invalidate();
                return IntPtr.Zero;
            case Win32.VK_LEFT when state.Page == LauncherState.PageReminderEdit:
                state.Move(-1); Invalidate();
                return IntPtr.Zero;
            case Win32.VK_RIGHT when state.Page == LauncherState.PageReminderEdit:
                state.Move(1); Invalidate();
                return IntPtr.Zero;
            case Win32.VK_LEFT when state.Query.Length == 0:
                if (state.Back()) Invalidate();
                return IntPtr.Zero;
            case Win32.VK_RIGHT when state.Query.Length == 0:
                if (state.Enter() is { Kind: LauncherRowKind.Page } opens) Picked(opens);
                return IntPtr.Zero;
            case Win32.VK_HOME:
                state.MoveEdge(toEnd: false); Invalidate();
                return IntPtr.Zero;
            case Win32.VK_END:
                state.MoveEdge(toEnd: true); Invalidate();
                return IntPtr.Zero;
            case Win32.VK_PRIOR:
                state.Move(-PageStep); Invalidate();
                return IntPtr.Zero;
            case Win32.VK_NEXT:
                state.Move(PageStep); Invalidate();
                return IntPtr.Zero;

            case Win32.VK_DELETE:
                if (state.Enter() is { Kind: LauncherRowKind.Tick } done) Picked(done);
                else if (state.Enter() is { Id: { } custom } mine && custom.StartsWith(QuickActions.CustomPrefix, StringComparison.Ordinal))
                    Picked(mine with { Id = QuickActions.RemovePrefix + custom[QuickActions.CustomPrefix.Length..] });
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

                if (state.Page == LauncherState.PageReminderEdit)
                {
                    if (state.Enter() is { } chosen) Picked(chosen);
                    return IntPtr.Zero;
                }

                if (state.Page == LauncherState.PageReminders && state.Query.Trim().Length > 0
                    && state.Enter() is { Id: { } when } timed && when.StartsWith(LauncherPages.AddPrefix, StringComparison.Ordinal))
                {
                    Picked(timed);
                    return IntPtr.Zero;
                }

                if (state.Page is { } pg && LauncherState.PageTakesText(pg)
                    && state.Query.Trim().Length > 0)
                {
                    Submitted?.Invoke(pg, state.Query.Trim());
                    return IntPtr.Zero;
                }

                if ((Win32.GetKeyState(Win32.VK_CONTROL) & 0x8000) != 0 && state.Enter() is { Detail: "run" } command)
                {
                    Run(command);
                    return IntPtr.Zero;
                }
                if (state.Enter() is { Kind: LauncherRowKind.Tick } opened) { EditReminder(opened); return IntPtr.Zero; }
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
