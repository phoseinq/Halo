using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Halo.ClaudeCode;
using Halo.Codex;
using Halo.Interop;
using Halo.Widgets;
using Windows.System;

namespace Halo.Shell;

internal enum NotchVisibilityAction
{
    None,
    Hide,
    ShowAndRender,
}

internal readonly record struct NotchVisibilityDecision(
    NotchVisibilityAction Action,
    bool ReturnEarly,
    bool HiddenForFullscreen);

internal static class NotchVisibility
{

    internal static NotchVisibilityDecision Decide(bool fullscreen, bool hiddenForFullscreen)
    {
        if (fullscreen)
            return new(hiddenForFullscreen ? NotchVisibilityAction.None : NotchVisibilityAction.Hide,
                ReturnEarly: true, HiddenForFullscreen: true);

        return new(hiddenForFullscreen ? NotchVisibilityAction.ShowAndRender : NotchVisibilityAction.None,
            ReturnEarly: false, HiddenForFullscreen: false);
    }

    internal static (bool Empty, float Shrink) Settled(int activeCount)
        => (activeCount == 0, activeCount == 0 ? 1f : 0f);
}

internal static class HoverHold
{

    internal const double GraceSeconds = 2.5;

    internal static bool Holding(bool over, float progress, bool banner, bool dropping)
        => over && progress > 0.9f && !banner && !dropping;

    internal static int[] Keep(int[] active, int primary, bool holding)
    {
        if (!holding || primary < 0 || Array.IndexOf(active, primary) >= 0) return active;
        var kept = new List<int>(active.Length + 1);
        foreach (int i in active)
        {
            if (i > primary && !kept.Contains(primary)) kept.Add(primary);
            kept.Add(i);
        }
        if (!kept.Contains(primary)) kept.Add(primary);
        return [.. kept];
    }
}

internal static class FaceInterrupt
{
        internal static bool Allowed(bool faceWanted, bool expanded, bool banner, bool asking,
                                 bool greeting, bool privacy, bool moving, bool alreadyBusy)
        => faceWanted && !expanded && !banner && !asking && !greeting && !privacy && !moving && !alreadyBusy;
}

internal sealed class AgentNoticeCoordinator
{
    private readonly Dictionary<int, AgentNotice> _previous = new();
    private readonly Dictionary<int, NoticeWindow> _pending = new();
    private long _nextOrder;
    private int _restore = -1;

    internal AgentNoticeCoordinator(int primary) => Primary = primary;

    internal int Primary { get; private set; }

    internal bool IsOpen(DateTimeOffset now) => _pending.Values.Any(window => window.Until >= now);

    internal void SetPrimary(int primary)
    {
        if (_restore < 0)
            Primary = primary;
    }

    internal void Observe(int widgetIndex, AgentNotice notice, DateTimeOffset now,
        bool desktopBacked = false, bool allowSelection = true)
    {
        _previous.TryGetValue(widgetIndex, out var previous);
        _previous[widgetIndex] = notice;

        bool started = notice.State == "working" && previous.State != "working";

        bool compacted = notice.CompactedAt is { } doneAt && doneAt != previous.CompactedAt &&
            now - doneAt < TimeSpan.FromSeconds(30);

        if (compacted)
            _pending[widgetIndex] = new NoticeWindow(now.AddSeconds(4), desktopBacked, _nextOrder++);

        if (started && allowSelection && _pending.Count == 0 && _restore < 0)
            Primary = widgetIndex;

        if (allowSelection)
            Select(now, static _ => true);
    }

    internal void Hold(DateTimeOffset until)
    {
        foreach (var (index, window) in _pending.ToArray())
            if (window.Until < until)
                _pending[index] = window with { Until = until };
    }

    internal void Tick(DateTimeOffset now, Func<int, bool>? isActive = null, bool allowSelection = true)
    {
        foreach (var (index, window) in _pending.ToArray())
            if (window.Until < now)
                _pending.Remove(index);

        if (allowSelection)
            Select(now, isActive ?? (static _ => true));
    }

    private void Select(DateTimeOffset now, Func<int, bool> isActive)
    {
        if (_pending.Count > 0)
        {
            if (_restore < 0)
                _restore = Primary;

            Primary = _pending
                .OrderBy(pair => pair.Key == _restore ? 0 : pair.Value.DesktopBacked ? 1 : 2)
                .ThenBy(pair => pair.Value.Order)
                .First().Key;
            return;
        }

        if (_restore >= 0)
        {
            if (isActive(_restore))
                Primary = _restore;
            _restore = -1;
        }
    }

    private readonly record struct NoticeWindow(DateTimeOffset Until, bool DesktopBacked, long Order);
}

internal sealed partial class NotchController
{
    private const int CollapsedW = 220, CollapsedH = 40, CollapsedR = 20;
    private const int ExpandedW = 560, ExpandedH = 220, ExpandedR = 30;
    private const int TintDeskCollapsed = 255;
    internal const int TintDeskExpanded = 245;

    internal const int TintAppCollapsed = 120, TintAppExpanded = 48;

    internal const int TintAskDesk = 150, TintAskApp = 34;

    private const int AskSwipeStrip = 26, AskSwipeDist = 30;

    internal const float BannerClarity = 0.8f;
    private const float OpenSeconds = 0.30f, CloseSeconds = 0.38f;

    private const float HoldSeconds = 0.75f;

    private const int CaptureOpenMs = 16, CaptureCollapsedMs = 50;
    private const int EmptyCatchAlpha = 1;

    private readonly LayeredNotch _notch;
    private readonly StatusStore _claudeStore;
    private readonly CodexStatusStore _codexStore;
    private readonly CodexDesktopRuntime _codexDesktopRuntime;
    private readonly IWidget[] _widgets;
    private readonly MediaSessions _mediaSessions;
    private readonly AgentNoticeCoordinator _agentNotices;
    private readonly DispatcherQueueTimer _timer;

    private float S => _notch.Zoom;
    private int Sc(int v) => (int)MathF.Round(v * S);
    private int _cl => _notch.WorkLeft + (_notch.WorkWidth - Sc(CollapsedW)) / 2 + (int)_offsetX;
    private int _el => _notch.WorkLeft + (_notch.WorkWidth - Sc(ExpandedW)) / 2 + (int)_offsetX;

    private int _ct => _notch.WorkTop + (int)MathF.Round(_notch.OffsetY * S);
    private int _et => _ct;

    private int _primary;
    private int _userPicked = -1;
    private float _progress;
    private float _menu;
    private float _drop = -1f;
    private float _arrive = -1f;
    private int _pending;
    private float _dropCX, _dropCY;
    private bool _dropOut;
    private string _dropIcon = "";
    private Bitmap? _dropImage;
    private readonly bool[] _prevActive;
    private int _row = -1;
    private float _rowOpen;
    private float _stripT;
    private int _widgetVersion = -1;
    private int _lastSec = -1;
    private bool _lastMouseDown;
    private bool _prevDragActive;

    private static readonly bool PinOpen = Environment.GetEnvironmentVariable("HALO_PIN_OPEN") == "1";

    private long _trayShowUntil;

    private string? _trayPressPath;
    private Win32.POINT _trayPressAt;
    private int _trayMode = -1;
    private bool _lastTrayDown;
    private bool _resizing;
    private Win32.POINT _resizeFrom;
    private float _scale0, _handle;
    private bool _hiddenForFullscreen;

    private bool[]? _featureMask;

    private bool _overFullscreen;

    private float _offsetX;
    private bool _moving;
    private float _holdT;
    private DateTime _holdStart = DateTime.MaxValue;
    private Win32.POINT _holdAnchor;
    private int _moveGrabDX;
    private bool _pinned;

    private bool _userHidden;

    private static bool Pinned(bool userPin) => userPin || FileTray.Holding;

    private bool TrayFront => FileTray.DragActive || (!_empty && _widgets[_primary] is FileTray);
    private float _pinHov;
    private float _shrink;
    private bool _empty;

    private readonly Halo.Notifications.NotifSource _notifSrc = new();
    private Halo.Notifications.BtBattery? _bt;
    private readonly Widgets.BtWidget _btWidget = new();
    private System.Threading.Timer? _testTrigger;
    private Halo.Notifications.NotifItem? _notif;

    private readonly AskStore _asks;
    private PendingAsk? _ask;

    private float _panelT;
    private int _panelH = 120;
    private int _panelHover = -1;
    private bool _panelCloseHover;
    private int _panelHeld = -1;
    private Halo.Panels.PanelStore.Snapshot? _panelGhost;

    private float _faceT;
    private float _faceAge;

    private float _handT = -1f;
    private Halo.Widgets.FaceProp _handProp = Halo.Widgets.FaceProp.None;

    private bool[] _wasActive = [];

    private bool _handDone;

    private System.Drawing.Image? _handIcon;
    private string? _handAumid;
    private int _eatPick = -1;
    private string _eatName = "";

    private bool _handSolo;

    private bool _notifFloat;

    private float _catGrip, _catDuck;

    private float _catAge;
    private object? _catFor;

    private bool _catShow;
    private CatMood _catMood;
    private float _catAnchor = 1f;

    private enum CatMood { Read, Doze, Bored, Thrilled }

    private const int CatOdds = 3;

    private void CatCast(Halo.Notifications.NotifItem? toast)
    {
        int h = 17;
        foreach (var part in new[] { toast?.App, toast?.Title, toast?.Body })
            if (part is { Length: > 0 })
                foreach (char c in part) h = unchecked(h * 31 + c);
        h &= 0x7fffffff;

        _catShow = h % CatOdds == 0;
        _catMood = (CatMood)(h / CatOdds % 4);

        _catAnchor = (h / (CatOdds * 4) % 3) switch { 0 => 0f, 1 => 0.5f, _ => 1f };
    }
    private long _faceDrewAt;
    private long _deskPolledAt;
    private const long DeskPollMs = 300;

    private const float FrostMix = 0.6f;

    private float _askT;
    private int _askH = 120;
    private int _askHover = -1;
    private bool _askCloseHover;

    private string? _askDismissed;

    private float? _askSwipeY;

    private PendingAsk? _askGhost;
    private System.Collections.Generic.List<(RectangleF Rect, Halo.ClaudeCode.AskOption Option)> _askChips = [];

    private string? _askTyped;
    private string? _drawnTyped;
    private string _askDraft = "";
    private string? _askDraftNonce;

    private GreetingKind _greet;
    private float _greetT;
    private bool _greetArmed;
    private float _greetHeld, _greetWaited;

    private readonly StripOrder _stripOrder = StripOrder.Load(StripOrderPath);

    private readonly StripOrder _sessionOrder = StripOrder.Load(SessionOrderPath);
    private List<string> _stripKinds = [];
    private int _dragRow = -1;
    private float _dragFromY;
    private float _dragHeld;
    private float _carryDY;
    private float _carryDX, _carryWantX;
    private int _dragSess = -1;
    private int _dragFromX;
    private float _carryWant;
    private float _drawnCarryDY, _drawnCarryDX;
    private float[] _sessShift = [];
    private int _drawnDragRow = -1;
    private float[] _rowShift = [];
    private readonly Halo.Interop.KeyGrab _keys = new();

    private float _notifT;
    private bool _notifClosing;
    private bool _notifDetailOn;
    private float _notifDetail;
    private int _notifDetailH = NotifBanner.SummaryH + 60;
    private int _notifInk, _drawnNotifInk;

    private float _notifFold = 1f;
    private const float FoldSecs = 0.34f;
    private DateTime _notifDeadline;
    private int _curW = CollapsedW, _curH = CollapsedH;
    private bool _lastDesktop = true;
    private IntPtr _lastFg = IntPtr.Zero;
    private uint _lastLangId;
    private IntPtr _langFg;
    private long _langFgSince;
    private IntPtr _behind = IntPtr.Zero;
    private long _lastCaptureAt;
    private long _animDrewAt;
    private int _lastCaptureVer;

    private long _alertAt;

    private readonly Dictionary<string, (DateTimeOffset reset, DateTime at)> _limitFired = LoadLimitFired();
    private static readonly string LimitFiredPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "limit-fired.txt");

    private static Dictionary<string, (DateTimeOffset, DateTime)> LoadLimitFired()
    {
        var d = new Dictionary<string, (DateTimeOffset, DateTime)>();
        try
        {
            foreach (var line in System.IO.File.ReadAllLines(LimitFiredPath))
            {
                var p = line.Split('|');
                if (p.Length == 3 && DateTimeOffset.TryParse(p[1], out var r) && DateTime.TryParse(p[2], null,
                        System.Globalization.DateTimeStyles.AdjustToUniversal, out var a))
                    d[p[0]] = (r, a);
            }
        }
        catch { }
        return d;
    }

    private void SaveLimitFired()
    {
        try
        {
            var lines = new List<string>();
            foreach (var (k, v) in _limitFired) lines.Add($"{k}|{v.reset:o}|{v.at:o}");
            System.IO.File.WriteAllLines(LimitFiredPath, lines);
        }
        catch { }
    }
    public NotchController(LayeredNotch notch)
    {
        _notch = notch;
        _notch.ClipboardImage += OnClipboardImage;
        _notch.WantsHandCursor = OverPressable;
        _claudeStore = new StatusStore();

        _asks = new AskStore(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "notch"));
        _claudeStore.AfterLoad = _asks.Rescan;
        _asks.Rescan();
        _keys.OnChar = TypedChar;
        _keys.OnKey = TypedKey;

        _codexStore = new CodexStatusStore();
        _codexDesktopRuntime = CodexDesktopRuntime.Shared;
        CodexLimits.Attach(_codexStore);
        CodexLimits.UpdateFrom(_codexStore.Current);

        _settings = new Halo.Settings.SettingsStore();
        Halo.Settings.SettingsStore.Shared = _settings;

        _greet = GreetingGate.Take(GreetedPath, DateOnly.FromDateTime(DateTime.Now),
                                   arriving: !ScreenWatchable(), GreetingWanted);
        _appliedSettings = _settings.Version;

        ApplyLanguage(_settings.Current);
        _api = new Halo.Api.HaloApi(ApiConfig, this);
        _api.Reconcile();
        _startupApplied = _settings.Current.Bool(Halo.Settings.SettingsKeys.StartWithWindows, true);
        ReconcileAutostart(_startupApplied);

        _silenceApplied = _settings.Current.Bool("notifications.silence", true);
        if (_silenceApplied) System.Threading.ThreadPool.QueueUserWorkItem(
            _ => { try { Halo.Notifications.BannerGate.Enable(); } catch { } });

        _mediaSessions = new MediaSessions();
        var widgets = new List<IWidget>();

        widgets.Add(new NetWidget(_net));

        Halo.Interop.WheelGrab.Start();
        widgets.Add(new DownloadWidget());
        for (int s = 0; s < MediaSessions.MaxSlots; s++)
            widgets.Add(new MediaWidget(_mediaSessions, s));
        widgets.Add(new VlcWidget(_mediaSessions));
        widgets.Add(new FileTray());
        widgets.Add(_btWidget);
        Privacy.Poke();
        for (int s = 0; s < StatusStore.MaxSessions; s++)
        {
            int slot = s;
            widgets.Add(new ClaudeCodeWidget(_claudeStore, slot, () => CancelClaude(slot)));
        }
        widgets.Add(new CodexWidget(_codexStore, CodexSurface.Desktop, () => CancelCodex(CodexSurface.Desktop),
            () => _codexDesktopRuntime.Presence.Running));
        widgets.Add(new CodexWidget(_codexStore, CodexSurface.Cli, () => CancelCodex(CodexSurface.Cli)));
        var agentStore = GenericAgentWidget.NewStore();
        for (int s = 0; s < StatusStore.MaxSessions; s++)
            widgets.Add(new GenericAgentWidget(agentStore, s));
        _widgets = [.. widgets];
        StartLauncher();
        FlushNet = _net.Flush;

        var active = ActiveIndices();
        LoadOffset();
        LoadRecordable();
        _notch.SetCapturable(_recordable);
        _empty = active.Length == 0;
        _shrink = _empty ? 1f : 0f;
        if (!_empty) _primary = PreferredPrimary(active);
        _prevActive = new bool[_widgets.Length];
        for (int i = 0; i < _widgets.Length; i++) _prevActive[i] = Live(i);
        Apply(0f);
        _agentNotices = new AgentNoticeCoordinator(_primary);

        _bt = new Halo.Notifications.BtBattery((name, pct, major, minor) => _btWidget.Show(name, pct, major, minor));
        _testTrigger = new System.Threading.Timer(_ => PollTestNotif(), null, 1000, 1000);

        Dispatcher.Ensure();
        var dq = DispatcherQueue.GetForCurrentThread();
        _timer = dq.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(IntervalMs(MaxFps));
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(DispatcherQueueTimer sender, object args)
    {

        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        try { Frame(); } catch (Exception ex) { CrashLog(ex); }
        FrameStat(t0);
    }

    private bool _sheetDbg;
    private string _raiseDbg = "";

    private int _fdbgN;
    private double _fdbgMs;
    private long _fdbgSince;

    private void FrameStat(long t0)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        _fdbgN++;
        _fdbgMs += (now - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        if (_fdbgSince == 0) { _fdbgSince = now; return; }
        double span = (now - _fdbgSince) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        if (span < 1000.0) return;
        if (_sheetDbg)
            LayeredNotch.GlassNote($"loop {_fdbgN / (span / 1000.0):0.0}fps avg={_fdbgMs / _fdbgN:0.0}ms "
                + $"asked={_cadence} raised={_timerRaised} capped={_timerCapped} {_raiseDbg}");
        _fdbgN = 0;
        _fdbgMs = 0;
        _fdbgSince = now;
    }

    private static void CrashLog(Exception ex)
    {
        try
        {
            var p = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "frame-errors.txt");
            System.IO.File.WriteAllText(p, $"{DateTime.Now:HH:mm:ss}\n{ex}");
        }
        catch { }
    }

    private long _cpuIdle, _cpuBusyBase, _cpuAt;
    private int _fps = MaxFps;

    private bool _heavy;

    private static readonly int[] CpuTiers = { 50, 70, 85, 95 };
    private static readonly int[] RamTiers = { 70, 85, 95 };

    internal static int[] Tiers(int[] fixedTiers, int first)
    {
        var tiers = new List<int> { first };
        foreach (var tier in fixedTiers) if (tier > first) tiers.Add(tier);
        return [.. tiers];
    }
    private int _cpuTierFired = -1, _ramTierFired = -1;
    private int _cpuStreak, _ramStreak;
    internal bool Heavy => _heavy;

    private string PrimaryWidgetName()
        => _empty || _primary < 0 || _primary >= _widgets.Length ? "none" : _widgets[_primary].GetType().Name;

    private string[] LiveWidgetNames()
    {
        var names = new System.Collections.Generic.List<string>(_widgets.Length);
        foreach (var w in _widgets)
        {
            try { if (w.IsActive) names.Add(w.GetType().Name); } catch { }
        }
        return [.. names];
    }

    private void AdaptFrameRate()
    {
        long now = Environment.TickCount64;
        if (now - _cpuAt < 1000) return;
        _cpuAt = now;
        if (!Win32.GetSystemTimes(out long idle, out long kern, out long user)) return;
        long total = kern + user;

        bool watching = !_hiddenForFullscreen
                        && (_progress > 0.02f || _drop >= 0f || _faceT > 0f
                            || (_notif != null && !_overFullscreen)
                            || _cue.Alive(Environment.TickCount64));
        int target = _fps;
        if (_cpuBusyBase != 0 && total > _cpuBusyBase)
        {
            float busy = 1f - (float)(idle - _cpuIdle) / (total - _cpuBusyBase);
            Halo.Interop.CpuLoad.Observe(busy);
            target = Tier(busy, watching, target);

            bool heavy = !watching && (_heavy ? busy > 0.40f : busy > 0.50f);
            if (heavy != _heavy)
            {
                _heavy = heavy;
                try { System.Diagnostics.Process.GetCurrentProcess().PriorityClass =
                    heavy ? System.Diagnostics.ProcessPriorityClass.BelowNormal
                          : System.Diagnostics.ProcessPriorityClass.Normal; } catch { }
            }
            int pctNow = (int)(busy * 100);
            int tier = TierOf(Tiers(CpuTiers, Halo.Settings.SettingsStore.Percent("alert.cpuAt", 50)), pctNow);
            _cpuStreak = tier > _cpuTierFired ? _cpuStreak + 1 : 0;

            if (_cpuStreak >= 10)
            {
                _cpuTierFired = tier; _cpuStreak = 0;
                if (Alert("cpu")) QueueCpuNotice(pctNow);
            }
            CheckRam();
        }
        _cpuIdle = idle; _cpuBusyBase = total;
        if (target != _fps) { _fps = target; ApplyCadence(); }
    }

    internal const float MiniOut = 0.50f, ContentIn = 0.20f;

    internal const float StripSwallowOut = 0.30f;
    internal static float ContentFade(float t) => Math.Clamp((t - ContentIn) / (1f - ContentIn), 0f, 1f);
    internal static float MiniFade(float t) => Math.Clamp(1f - t / MiniOut, 0f, 1f);

    internal static bool MorphHasContent(float t) => ContentFade(t) + MiniFade(t) > 0.3f;

    internal const int MaxFps = 240;

    internal static int Reach(int ceiling) => ceiling > 0 ? ceiling : MaxFps;

    internal static int CadenceFps(bool morphing, int tier, int ceiling = 0)
        => morphing || tier == MaxFps ? Reach(ceiling) : tier;

    internal static double IntervalMs(int fps) => 1000.0 / Math.Max(1, fps);

    internal static int Capped(int fps, int ceiling) => ceiling > 0 && fps > ceiling ? ceiling : fps;

    internal static int Tier(float busy, bool watching, int current)
    {

        if (watching) return busy > 0.90f ? 60 : MaxFps;
        if (busy > 0.90f) return 30;
        if (busy > 0.55f) return 60;
        if (busy < 0.45f) return MaxFps;
        return current;
    }

    internal static int AutoCeiling(int displayHz) => displayHz is >= 24 and <= 1000 ? displayHz : MaxFps;

    private int FpsCeiling => _settings.Current.Text(Halo.Settings.SettingsKeys.FrameRate, "Auto") switch
    {
        "280" => 280,
        "240" => 240,
        "144" => 144,
        "120" => 120,
        "60" => 60,
        "30" => 30,
        _ => AutoCeiling(_displayHz),
    };

    private bool _morphing;
    private int _displayHz;
    private long _displayAt;
    private MorphRate _morphRate;
    private SteadyRate _steadyRate;

    private void PollDisplay()
    {
        long now = Environment.TickCount64;
        if (_displayAt != 0 && now - _displayAt < 5000) return;
        _displayAt = now;
        var info = Halo.Interop.Display.Probe(_notch.Hwnd);
        if (info.Dpi > 0f) _notch.SetDpi(info.Dpi);
        if (info.Hz != _displayHz)
        {
            _displayHz = info.Hz;
            ApplyCadence();
        }
        RateReport.Write(_morphRate.Measured, _displayHz, _steadyRate.Measured);
    }

    private int _drawnSs = 2;
    private bool _timerRaised, _timerCapped;
    private int _ptrX = int.MinValue, _ptrY = int.MinValue;
    private long _timerRaisedAt;
    internal const long TimerRaiseCapMs = 600_000;

    internal static (bool Raise, bool Capped, long RaisedAt) TimerLatch(
        bool want, bool raised, bool capped, long raisedAt, long now, bool inputEdge)
    {
        if (!want) return (false, false, raisedAt);
        if (capped && inputEdge) capped = false;
        else if (raised && now - raisedAt >= TimerRaiseCapMs) capped = true;
        if (capped) return (false, true, raisedAt);
        return (true, false, raised ? raisedAt : now);
    }

    internal const int GlassLiveStreak = 30;

    internal static bool GlassWantsFineTimer(bool panelOpen, bool watched, bool overDesktop, int staleStreak)
        => panelOpen && watched && !overDesktop && staleStreak < GlassLiveStreak;

    private void RaiseTimer(bool want, bool inputEdge)
    {
        long now = Environment.TickCount64;

        var next = TimerLatch(want, _timerRaised, _timerCapped, _timerRaisedAt, now, inputEdge);
        _timerCapped = next.Capped;
        _timerRaisedAt = next.RaisedAt;
        if (next.Raise == _timerRaised) return;
        try
        {
            if (next.Raise) Win32.timeBeginPeriod(1); else Win32.timeEndPeriod(1);
            _timerRaised = next.Raise;
        }
        catch { }
    }

    private int _cadence = MaxFps;
    private void ApplyCadence()
    {

        int ceiling = FpsCeiling;
        int fps = _morphing ? Reach(ceiling) : Capped(_fps, ceiling);
        if (fps == _cadence) return;
        _cadence = fps;
        _timer.Interval = TimeSpan.FromMilliseconds(IntervalMs(fps));
    }

    private static int TierOf(int[] tiers, int pct)
    {
        int t = -1;
        for (int i = 0; i < tiers.Length; i++) if (pct >= tiers[i]) t = i;
        return t;
    }

    private void CheckRam()
    {
        var ms = new Win32.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.MEMORYSTATUSEX>() };
        if (!Win32.GlobalMemoryStatusEx(ref ms)) return;
        int pct = (int)ms.dwMemoryLoad;
        int tier = TierOf(Tiers(RamTiers, Halo.Settings.SettingsStore.Percent("alert.memoryAt", 70)), pct);
        _ramStreak = tier > _ramTierFired ? _ramStreak + 1 : 0;
        if (_ramStreak >= 10)
        {
            _ramTierFired = tier; _ramStreak = 0;
            if (Alert("memory")) QueueRamNotice(pct);
        }
    }

    private void QueueRamNotice(int pct)
        => QueueLoadNotice(cpu: false, pct, TopRamProcess, Halo.Localization.Strings.Get("notice.load.ramFallback"));

    private void QueueLoadNotice(bool cpu, int pct, Func<string?> topProcess, string? fallbackBody)
    {
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            string? top = topProcess();
            string? body = top != null ? Halo.Localization.Strings.Format("notice.load.body", top) : fallbackBody;
            if (body == null) return;
            _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
            {
                App = Halo.Localization.Strings.Get("notice.app.system"),
                Title = Halo.Localization.Strings.Format("notice.load.title",
                    Halo.Localization.Strings.Get(cpu ? "notice.load.cpu" : "notice.load.memory"), pct),

                Body = body, Kind = cpu ? "cpu" : "memory", Duration = 7,
                Icon = cpu ? Badges.Cpu() : Badges.Memory(),
            });
        });
    }

    private static string? TopRamProcess()
    {
        try
        {
            var procs = System.Diagnostics.Process.GetProcesses();
            string? best = null; long bestWs = 0; int self = Environment.ProcessId;
            foreach (var p in procs)
            {
                try { if (p.Id != self && p.Id > 4 && p.WorkingSet64 > bestWs) { bestWs = p.WorkingSet64; best = p.ProcessName; } }
                catch { }
            }
            foreach (var p in procs) { try { p.Dispose(); } catch { } }
            return best == null || best.Length == 0 ? null : char.ToUpperInvariant(best[0]) + best[1..];
        }
        catch { return null; }
    }

    private void QueueCpuNotice(int sysPct)
        => QueueLoadNotice(cpu: true, sysPct, TopCpuProcess, null);

    private static string? TopCpuProcess()
    {
        try
        {
            var procs = System.Diagnostics.Process.GetProcesses();
            var t0 = new Dictionary<int, TimeSpan>();
            foreach (var p in procs) { try { t0[p.Id] = p.TotalProcessorTime; } catch { } }
            System.Threading.Thread.Sleep(450);
            string? best = null; double bestMs = 0; int self = Environment.ProcessId;
            foreach (var p in procs)
            {
                try
                {
                    if (p.Id == self || p.Id <= 4 || !t0.TryGetValue(p.Id, out var a)) continue;
                    p.Refresh();
                    double ms = (p.TotalProcessorTime - a).TotalMilliseconds;
                    if (ms > bestMs) { bestMs = ms; best = p.ProcessName; }
                }
                catch { }
            }
            foreach (var p in procs) { try { p.Dispose(); } catch { } }
            return best == null || best.Length == 0 ? null : char.ToUpperInvariant(best[0]) + best[1..];
        }
        catch { return null; }
    }

    private bool Alert(string name, bool on = true) => _settings.Current.Bool("alert." + name, on);

    private int _appliedSettings = -1;
    private bool _startupApplied;
    private bool _silenceApplied;
    private readonly Halo.Api.HaloApi _api;

    private readonly Halo.Panels.PanelStore _panels = new();

    private Halo.Api.HaloApi.Config ApiConfig()
    {
        var current = _settings.Current;
        bool on = current.Bool("api.enabled", false);
        string token = current.Text("api.token", "");
        if (on && token.Length == 0)
        {
            token = Guid.NewGuid().ToString("n");
            _settings.Set("api.token", token);
        }
        return new Halo.Api.HaloApi.Config(
            on,
            int.TryParse(current.Text("api.port", ""), out var port) && port is > 1023 and < 65536
                ? port : Halo.Api.HaloApi.DefaultPort,
            token,
            current.Bool("api.notify", true),
            current.Bool("api.ask", true),
            current.Bool("api.state", false),

            current.Bool("api.control", false),
            current.Bool("api.settings", false),
            current.Bool("api.panel", true));
    }

    private static void ApplyLanguage(Halo.Settings.SettingsFile current)
    {
        string forced = Environment.GetEnvironmentVariable("HALO_LANG") ?? "";
        Halo.Localization.Strings.Use(forced.Length > 0
            ? Halo.Localization.Strings.Name(forced)
            : current.Text("general.language", Halo.Localization.Strings.SystemLabel));
    }

    private void SyncSettings()
    {
        int version = _settings.Version;
        if (version == _appliedSettings) return;
        _appliedSettings = version;
        var current = _settings.Current;

        ApplyLanguage(current);

        ApplyCadence();

        bool startup = current.Bool(Halo.Settings.SettingsKeys.StartWithWindows, true);
        if (startup != _startupApplied)
        {
            _startupApplied = startup;
            Autostart(startup);
        }

        ReconcileHotkeys(current);

        bool pin = current.Bool(Halo.Settings.SettingsKeys.OverFullscreen, _pinned);
        if (pin != _pinned)
        {
            _pinned = pin;
            try { System.IO.File.WriteAllText(PinPath, _pinned ? "1" : "0"); } catch { }
        }

        _api.Reconcile();

        bool silence = current.Bool("notifications.silence", true);
        if (silence != _silenceApplied)
        {
            _silenceApplied = silence;
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try { if (silence) Halo.Notifications.BannerGate.Enable(); else Halo.Notifications.BannerGate.Restore(); }
                catch { }
            });
        }

        bool recordable = current.Bool(Halo.Settings.SettingsKeys.InCaptures, _recordable);
        if (recordable != _recordable)
        {
            _recordable = recordable;
            try { System.IO.File.WriteAllText(RecordablePath, _recordable ? "1" : "0"); } catch { }
            try { _notch.SetCapturable(_recordable); } catch { }
        }

        if (Scale(current.Text(Halo.Settings.SettingsKeys.Scale, "")) is { } scale
            && Math.Abs(scale - _notch.Scale) > 0.001f)
        {
            _notch.Scale = scale;
            try { _notch.SaveScale(); } catch { }
        }
    }

    private static void ReconcileAutostart(bool want)
    {
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                string hooks = System.IO.Path.Combine(AppContext.BaseDirectory, "Halo.Hooks.exe");
                if (!System.IO.File.Exists(hooks)) return;
                var psi = new System.Diagnostics.ProcessStartInfo(hooks)
                { UseShellExecute = false, CreateNoWindow = true };
                psi.ArgumentList.Add("query-autostart");
                using var p = System.Diagnostics.Process.Start(psi);
                if (p == null) return;

                if (!p.WaitForExit(20_000)) { try { p.Kill(entireProcessTree: true); } catch { } return; }

                if (p.ExitCode == 3) return;
                if ((p.ExitCode == 0) != want) Autostart(want);
            }
            catch { }
        });
    }

    private static void Autostart(bool on)
    {
        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                string hooks = System.IO.Path.Combine(AppContext.BaseDirectory, "Halo.Hooks.exe");
                if (!System.IO.File.Exists(hooks)) return;
                var psi = new System.Diagnostics.ProcessStartInfo(hooks)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add(on ? "install-autostart" : "uninstall-autostart");
                if (on) psi.ArgumentList.Add(Environment.ProcessPath ?? "");
                using var p = System.Diagnostics.Process.Start(psi);
                if (p != null && !p.WaitForExit(20_000)) { try { p.Kill(entireProcessTree: true); } catch { } }
            }
            catch { }
        });
    }

    private float MotionScale => _settings.Current.Text(Halo.Settings.SettingsKeys.Motion, "Soft") switch
    {
        "Reduced" => 0.35f,
        "Standard" => 1.55f,
        _ => 1f,
    };

        internal static int TintFor(int baseAlpha, float scale)
        => Math.Clamp((int)(baseAlpha * scale), 0, 255);

    private float GlassScale => _settings.Current.Text(Halo.Settings.SettingsKeys.Glass, "Balanced") switch
    {
        "Light" => 0.66f,
        "Strong" => 1.34f,
        _ => 1f,
    };

    private static float? Scale(string text)
    {
        if (text.Length == 0) return null;
        string digits = text.TrimEnd('%');
        return float.TryParse(digits, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var pct)
            ? Math.Clamp(pct / 100f, 0.7f, 1.6f)
            : null;
    }

    private void CheckAlerts()
    {
        long now = Environment.TickCount64;
        if (now - _alertAt < 1000) return;
        _alertAt = now;
        SyncSettings();
        if (Pinned(_pinned)) _notch.AssertTopmost();
        FireDueReminders();

        ReloadOffset();
        if (Alert("battery")) CheckBattery();
        if (Alert("limit"))
        {
            CheckLimit("Claude", ClaudeCode.Limits.FiveHour, ClaudeCode.Limits.FiveHourReset, "5-hour");
            CheckLimit("Claude", ClaudeCode.Limits.Week, ClaudeCode.Limits.WeekReset, "weekly");

            CheckLimit("Codex", CodexLimits.PrimaryFrac, CodexLimits.PrimaryReset, "primary");
            CheckLimit("Codex", CodexLimits.SecondaryFrac, CodexLimits.SecondaryReset, "secondary");
        }
        if (Alert("internet")) CheckInternet();
        if (Alert("context")) CheckContext();
        CheckCompact();
        CheckApiRetry();
        if (Alert("hourly")) CheckHourly();
        if (Alert("weather", on: false)) CheckHeat();
        _net.Poll();
        Almanac.Poke();
    }

    private readonly HashSet<string> _ctxWarned = new(StringComparer.Ordinal);
    private readonly List<string> _ctxLive = new();

    private void CheckContext()
    {
        _ctxLive.Clear();
        foreach (var widget in _widgets)
        {
            if (widget is not Widgets.ClaudeCodeWidget cc) continue;
            var (id, frac) = cc.ContextState();
            if (id is null) continue;
            _ctxLive.Add(id);
            if (frac < Widgets.ClaudeCodeWidget.ContextWarnAt)
            {
                _ctxWarned.Remove(id);
                continue;
            }
            if (!_ctxWarned.Add(id)) continue;
            _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
            {
                App = Halo.Localization.Strings.Get("notice.app.claude"),
                Title = Halo.Localization.Strings.Format("notice.context.title", (int)(frac * 100)),
                Body = Halo.Localization.Strings.Get("notice.context.body"),
                Kind = "ctx-" + id, Duration = 8, Icon = Badges.Context(),
            });
        }

        if (_ctxWarned.Count > _ctxLive.Count) _ctxWarned.IntersectWith(_ctxLive);
    }

    private void CheckCompact()
    {
        int pid = 0;
        string? key = null;
        for (int s = 0; s < StatusStore.MaxSessions && pid == 0; s++)

            if (_claudeStore.SessionLive(s) is { State: "compacting", Pid: > 0 } st
                && Widgets.ClaudeCodeWidget.Compacting(st))
            {
                pid = st.Pid;
                key = st.StartedAt;
            }
        if (pid > 0) ClaudeCode.CompactProgress.Poke(pid, key);
        else ClaudeCode.CompactProgress.Done();
    }

    private const int QuietBeforeRetryCheck = 12;

    private void CheckApiRetry()
    {
        int pid = 0;
        var now = DateTimeOffset.UtcNow;
        for (int s = 0; s < StatusStore.MaxSessions && pid == 0; s++)
            if (_claudeStore.SessionLive(s) is { State: "working", Pid: > 0 } st
                && string.IsNullOrEmpty(st.CurrentTool)
                && Widgets.ClaudeCodeWidget.WatchForRetry(st, now, ClaudeCode.ApiRetry.LiveFor(st.Pid)))
                pid = st.Pid;
        if (pid > 0) ClaudeCode.ApiRetry.Poke(pid);
        else ClaudeCode.ApiRetry.Done();
    }

    private readonly HeatWatch _heat = new();

    private readonly NetMeter _net = new();

    internal static Action? FlushNet;
    private void CheckHeat()
    {
        if (Almanac.Latest is not { } wx) return;
        if (_heat.Observe(wx.TempC, DateTime.Now) is not { } rise) return;
        _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.weather.app"),
            Title = Halo.Localization.Strings.Format("notice.heat.title", wx.TempC),
            Body = Halo.Localization.Strings.Format("notice.heat.body", rise),
            Kind = "weather", Duration = 6, Icon = Badges.Hot(),
        });
    }

    private int _chimedHour = DateTime.Now.Hour;
    private void CheckHourly()
    {
        Almanac.SyncZone();
        var t = DateTime.Now;
        if (t.Minute != 0 || t.Hour == _chimedHour) return;
        _chimedHour = t.Hour;
        _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
        {
            App = Almanac.Label, Title = Almanac.Headline(t), Body = Almanac.Detail(t),
            Kind = "hourly", Duration = 6, Icon = Badges.Hourly(),
        });
    }

    private static readonly string TestNotifPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "notif-test.txt");
    private void PollTestNotif()
    {
        try
        {
            if (!System.IO.File.Exists(TestNotifPath)) return;
            var line = System.IO.File.ReadAllText(TestNotifPath).Trim();
            System.IO.File.Delete(TestNotifPath);
            if (line.Length == 0) return;
            var parts = line.Split('|');
            string type = parts[0].Trim().ToLowerInvariant();
            string arg = parts.Length > 1 ? parts[1].Trim() : "";
            string proc = parts.Length > 2 && parts[2].Trim().Length > 0 ? parts[2].Trim() : "";
            switch (type)
            {

                case "cpu": case "sys": case "system":
                    QueueLoadNotice(cpu: true, int.TryParse(arg, out var cp) ? cp : 92,
                        () => proc.Length > 0 ? proc : TopCpuProcess() ?? "Chrome", null);
                    break;
                case "ram": case "mem": case "memory":
                    QueueLoadNotice(cpu: false, int.TryParse(arg, out var rp) ? rp : 88,
                        () => proc.Length > 0 ? proc : TopRamProcess() ?? "Chrome", null);
                    break;

                case "hooks": case "hook":
                    {
                        bool codex = arg.StartsWith("codex", StringComparison.OrdinalIgnoreCase);
                        string agent = codex ? "Codex" : "Claude Code";
                        string path = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            codex ? ".codex" : ".claude", codex ? "hooks.json" : "settings.json");
                        bool fail = proc.StartsWith("fail", StringComparison.OrdinalIgnoreCase);
                        var (napp, ntitle, nbody) = fail
                            ? Halo.ClaudeCode.HookConnect.Failed(agent, "access denied")
                            : Halo.ClaudeCode.HookConnect.Notice(agent, path);
                        _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
                        {
                            App = napp, Title = ntitle, Body = nbody, Kind = "hooks", Duration = 8,
                            Icon = fail ? Badges.HookFailed() : Badges.Hooked(),
                        });
                    }
                    break;
                case "heat": case "weather":
                    int hot = int.TryParse(arg, out var hc) ? hc : 34;
                    _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
                    {
                        App = Halo.Localization.Strings.Get("notice.weather.app"),
                        Title = Halo.Localization.Strings.Format("notice.heat.title", hot),
                        Body = Halo.Localization.Strings.Format("notice.heat.body", HeatWatch.RiseC + 1),
                        Kind = "weather", Duration = 6, Icon = Badges.Hot(),
                    });
                    break;
                case "clock": case "hour": case "hourly":
                    var t = int.TryParse(arg, out var hr) && hr is >= 0 and <= 23 ? DateTime.Today.AddHours(hr) : DateTime.Now;
                    Almanac.Poke();
                    _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
                    {
                        App = Almanac.Label, Title = Almanac.Headline(t), Body = Almanac.Detail(t),
                        Kind = "hourly", Duration = 6, Icon = Badges.Hourly(),
                    });
                    break;
            }
        }
        catch { }
    }

    private static readonly int[] BatteryTiers = [20, 10];

    private static int[] BatteryLadder()
    {
        int low = Halo.Settings.SettingsStore.Percent("alert.batteryAt", 20);
        return low <= 10 ? [low] : [low, 10];
    }
    private int _battTier = -1;

    private void CheckBattery()
    {
        if (!Win32.GetSystemPowerStatus(out var s)) return;
        bool onBattery = s.ACLineStatus == 0;
        int pct = s.BatteryLifePercent;
        if (!onBattery || pct > 100) { _battTier = -1; return; }
        int tier = BatteryTier(pct, BatteryLadder());
        if (tier <= _battTier) { if (tier < 0) _battTier = -1; return; }
        _battTier = tier;
        bool dead = tier >= 1;
        _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.app.battery"),
            Title = Halo.Localization.Strings.Format("notice.battery.title",
                Halo.Localization.Strings.Get(dead ? "notice.battery.critical" : "notice.battery.low"), pct),
            Body = Halo.Localization.Strings.Get("notice.battery.body"),
            Kind = "battery", Duration = 8, OnActivate = EnablePowerSaver,
            Icon = dead ? Badges.BatteryDead() : Badges.BatteryLow(),
        });
    }

    internal static int BatteryTier(int pct) => BatteryTier(pct, BatteryTiers);

    internal static int BatteryTier(int pct, int[] ladder)
    {
        int t = -1;
        for (int i = 0; i < ladder.Length; i++) if (pct <= ladder[i]) t = i;
        return t;
    }

    private static void EnablePowerSaver()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powercfg", Arguments = "/setactive a1841308-3541-4fab-bc81-f71556f20b4a",
                UseShellExecute = false, CreateNoWindow = true,
            });
        }
        catch { }
    }

    private void CheckLimit(string app, float util, DateTimeOffset reset, string window)
    {
        if (util < Halo.Settings.SettingsStore.Percent("alert.limitAt", 80) / 100f) return;
        string key = app + window;
        if (_limitFired.TryGetValue(key, out var f)
            && (DateTime.UtcNow - f.at < TimeSpan.FromHours(6)
                || (reset != default && f.reset != default && (reset - f.reset).Duration() < TimeSpan.FromMinutes(30))))
            return;
        _limitFired[key] = (reset, DateTime.UtcNow);
        SaveLimitFired();
        int p = (int)(util * 100);
        _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
        {

            App = app,
            Title = Halo.Localization.Strings.Format("limit.title", app, p),
            Body = Halo.Localization.Strings.Format("limit.body", p, Halo.Localization.Strings.Get("limit.window." + window)),
            Kind = $"limit-{app}-{window}", Duration = 8,
            Icon = LongWindow(window) ? Badges.LimitLong() : Badges.Limit(),
        });
    }

    internal static bool LongWindow(string window) => window is "weekly" or "secondary";

    private string? _netShown;
    private void CheckInternet()
    {
        var trouble = NetTrouble(ClaudeCode.NetMon.NetDown, ClaudeCode.NetMon.ApiDown, ClaudeCode.NetMon.Slow,
                                 _net.Busy);
        if (trouble == _netShown) return;
        _netShown = trouble;
        if (trouble is null) return;
        var item = trouble switch
        {
            "offline" => new Halo.Notifications.NotifItem
            {
                App = Halo.Localization.Strings.Get("notice.app.network"),
                Title = Halo.Localization.Strings.Get("notice.net.down.title"),
                Body = Halo.Localization.Strings.Get("notice.net.down.body"),
                Kind = "net", Duration = 7, Icon = Badges.NetDown(),
            },
            "api" => new Halo.Notifications.NotifItem
            {
                App = Halo.Localization.Strings.Get("notice.app.claude"),
                Title = Halo.Localization.Strings.Get("notice.api.down.title"),
                Body = Halo.Localization.Strings.Get("notice.api.down.body"),
                Kind = "net", Duration = 7, Icon = Badges.ApiDown(),
            },
            _ => new Halo.Notifications.NotifItem
            {
                App = Halo.Localization.Strings.Get("notice.app.network"),
                Title = Halo.Localization.Strings.Get("notice.net.slow.title"),
                Kind = "net", Duration = 6, Icon = Badges.NetSlow(),
            },
        };
        _notifSrc.EnqueueLocal(item);
    }

    internal static string? NetTrouble(bool netDown, bool apiDown, bool slow, bool busy)
        => netDown ? "offline" : apiDown ? "api" : slow && !busy ? "slow" : null;

    internal static readonly TimeSpan WakeGap = TimeSpan.FromSeconds(90);
    private DateTime _lastTickUtc = DateTime.UtcNow;

    private bool GreetingWanted =>
        _settings.Current.Bool(Halo.Settings.SettingsKeys.Greeting, true);

    private bool FaceWanted =>
        _settings.Current.Bool(Halo.Settings.SettingsKeys.Face, true);

    private bool _fgFullscreen;

    private float _gazeX, _gazeY, _near;

    private void GazeFrame()
    {
        float wantX = 0f, wantY = 0f, wantNear = 0f;
        if (_faceT > 0.01f)
        {
            try
            {
                if (Win32.GetCursorPos(out var c))
                {

                    float cx = _cl + Sc(_curW) / 2f, cy = _ct + Sc(_curH) / 2f;
                    float dx = c.X - cx, dy = c.Y - cy;

                    wantX = Math.Clamp(dx / 420f, -1f, 1f);
                    wantY = Math.Clamp(dy / 300f, -1f, 1f);

                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    wantNear = 1f - Math.Clamp(d / 340f, 0f, 1f);
                }
            }
            catch { }
        }

        float rate = Math.Min(1f, _dt / 0.16f);
        _gazeX += (wantX - _gazeX) * rate;
        _gazeY += (wantY - _gazeY) * rate;
        _near += (wantNear - _near) * Math.Min(1f, _dt / 0.22f);
    }

    private Halo.Widgets.Face.Look FaceLook()
    {
        var look = Halo.Widgets.FaceDirector.At(_faceAge);
        float own = _handT >= 0f ? 0f : 1f;
        if (own <= 0f) return look;
        return look with
        {
            GazeX = look.GazeX * (1f - own) + _gazeX * own,
            GazeY = look.GazeY * (1f - own) + _gazeY * own,
            Open = look.Open * (1f + 0.22f * _near * own),
            Glow = look.Glow * (1f + 0.30f * _near * own),
        };
    }

    private bool FaceOverFullscreen => _fgFullscreen && Pinned(_pinned) && _handT >= 0f;

    private bool FaceWakes =>
        (FacePinned || (_empty && (_lastDesktop || FaceOverFullscreen)))
        && !_handDone && FaceWanted && !Privacy.Active && !_moving
        && _notif == null && _ask == null && _askGhost == null && _panelGhost == null
        && _greet == GreetingKind.None;

    private static bool FacePinned =>
        Environment.GetEnvironmentVariable("HALO_FACEPIN") == "1";

    private Halo.Launcher.AppIndex? _appIndex;
    private Halo.Launcher.LaunchStats? _launchStats;
    private Halo.Launcher.HotKey? _hotKey;
    private Halo.Launcher.HotKey? _hideKey;
    private Halo.Launcher.LauncherDim? _dim;
    private Halo.Launcher.LauncherBox? _box;
    private float _dimT;

    private const float DimTarget = 140f / 255f;
    private const float DimSeconds = 0.22f;

    private static void LaunchDebug(string line)
    {
        if (Environment.GetEnvironmentVariable("HALO_LAUNCHDEBUG") != "1") return;
        try
        {
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "launcher-debug.txt"),
                $"{DateTime.Now:HH:mm:ss.fff} {line}\r\n");
        }
        catch { }
    }

    private void StartLauncher()
    {
        if (!_settings.Current.Bool(Halo.Settings.SettingsKeys.LauncherEnabled, true)) return;
        try
        {
            _appIndex = new Halo.Launcher.AppIndex(
                Halo.Launcher.AppCache.DefaultPath, Halo.Launcher.AppScan.Enumerate);
            _appIndex.Start();
            _launchStats = Halo.Launcher.LaunchStats.Read(Halo.Launcher.LaunchStats.DefaultPath);

            Halo.Launcher.LauncherDim.Trace = LaunchDebug;
            Halo.Launcher.LauncherBox.Trace = LaunchDebug;

            Halo.Interop.GpuLoad.Refresh();
            _dim = new Halo.Launcher.LauncherDim();
            _box = new Halo.Launcher.LauncherBox();
            _box.Chosen += LauncherChose;
            _box.Dismissed += CloseLauncher;
            _box.Submitted += LauncherSubmitted;
            _box.HandledInPlace = LauncherInPlace;
            _box.IconFor = Halo.Launcher.LauncherIcons.Get;
            var box = _box;
            Halo.Launcher.LauncherIcons.Arrived = () => box.Invalidate();

            string stored = _settings.Current.Text(
                Halo.Settings.SettingsKeys.LauncherHotkey, Halo.Launcher.HotKeyChord.Default.Format());
            var chord = Halo.Launcher.HotKeyChord.TryParse(stored, out var parsed)
                ? parsed : Halo.Launcher.HotKeyChord.Default;

            _hotKey = new Halo.Launcher.HotKey(_notch.Hwnd, Halo.Launcher.HotKey.Id);
            bool held = _hotKey.Register(chord);

            _chordApplied = stored;
            string hideText = _settings.Current.Text(Halo.Settings.SettingsKeys.HideHotkey, "Ctrl+Alt+H");
            _hideChordApplied = hideText;
            bool hideHeld = false;
            if (Halo.Launcher.HotKeyChord.TryParse(hideText, out var hideChord))
            {
                _hideKey = new Halo.Launcher.HotKey(_notch.Hwnd, Halo.Launcher.HotKey.HideId);
                hideHeld = _hideKey.Register(hideChord);
            }

            _notch.HotKeyPressed += id =>
            {
                LaunchDebug($"WM_HOTKEY id={id} mine={Halo.Launcher.HotKey.Id}");
                if (id == Halo.Launcher.HotKey.Id) OpenLauncher();
                else if (id == Halo.Launcher.HotKey.HideId) ToggleHidden();
            };
            LaunchDebug($"started hwnd=0x{_notch.Hwnd.ToInt64():X} chord={chord.Format()} held={held} "
                        + $"hide={hideText} hideHeld={hideHeld}");
        }
        catch (Exception ex) { LaunchDebug("start FAILED " + ex); }
    }

    private string? _chordApplied, _hideChordApplied;

    private void ReconcileHotkeys(Halo.Settings.SettingsFile current)
    {
        string want = current.Text(Halo.Settings.SettingsKeys.LauncherHotkey,
                                   Halo.Launcher.HotKeyChord.Default.Format());
        if (want != _chordApplied)
        {
            _chordApplied = want;
            if (_hotKey is { } key)
            {
                if (Halo.Launcher.HotKeyChord.TryParse(want, out var chord)) key.Register(chord);
                else key.Unregister();
            }
        }

        string hide = current.Text(Halo.Settings.SettingsKeys.HideHotkey, "Ctrl+Alt+H");
        if (hide == _hideChordApplied) return;
        _hideChordApplied = hide;

        bool parsed = Halo.Launcher.HotKeyChord.TryParse(hide, out var hideChord);
        if (parsed)
        {
            _hideKey ??= new Halo.Launcher.HotKey(_notch.Hwnd, Halo.Launcher.HotKey.HideId);
            parsed = _hideKey.Register(hideChord);
        }
        else _hideKey?.Unregister();

        if (!parsed) _userHidden = false;
    }

    private void ToggleHidden()
    {
        _userHidden = !_userHidden;
        if (_userHidden && _box is { IsOpen: true }) CloseLauncher();
    }

    private void OpenLauncher()
    {
        if (_box is null || _dim is null || _appIndex is null) return;
        if (_box.IsOpen) { CloseLauncher(); return; }
        try
        {
            var mon = new System.Drawing.Rectangle(
                _notch.WorkLeft, _notch.WorkTop, _notch.WorkWidth, _notch.WorkHeight);

            _dim.Show(mon);

            _notch.AssertTopmost();

            var state = new Halo.Launcher.LauncherState(
                () => _appIndex.Apps, () => _appIndex.Ready,
                _launchStats ?? new Halo.Launcher.LaunchStats(), () => DateTimeOffset.Now);

            state.PageRows = (id, q) => Halo.Launcher.LauncherPages.For(
                id, q, () => (_launcherAudio ??= new Halo.Widgets.AudioMeter()).Muted());
            state.PageGauges = Halo.Launcher.LauncherPages.SystemGauges;

            state.LanguageRows = () => Halo.Launcher.LauncherPages.LanguageRows(
                state.Picking == Halo.Launcher.LauncherState.LangPick.From);
            _box.Open(mon, _ct + Sc(_curH), state);
            LaunchDebug($"opened mon={mon} top={_ct + Sc(_curH)} apps={_appIndex.Apps.Count} ready={_appIndex.Ready}");
        }
        catch (Exception ex) { LaunchDebug("open FAILED " + ex); }
    }

    private void CloseLauncher()
    {
        try
        {
            _box?.Close();

            _appIndex?.RefreshSoon();
        }
        catch { }
    }

    private static void Open(string target)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            { FileName = target, UseShellExecute = true });
        }
        catch { }
    }

    private static void OpenSettingsPanel() => Program.OpenSettingsFromLauncher();

    private static void ToggleWindowsTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: true);
            if (key is null) return;
            int light = key.GetValue("AppsUseLightTheme") is int v ? v : 1;
            key.SetValue("AppsUseLightTheme", light == 0 ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);
            Win32.SendMessageTimeout(Win32.HWND_BROADCAST, Win32.WM_SETTINGCHANGE, IntPtr.Zero,
                                     "ImmersiveColorSet", 2, 1000, out _);
        }
        catch { }
    }

    private Halo.Widgets.AudioMeter? _launcherAudio;

    private void RunLauncherAction(string? id)
    {
        if (string.IsNullOrEmpty(id)) return;
        try
        {
            if (id == Halo.Launcher.LauncherPages.ActMute)
            {
                (_launcherAudio ??= new Halo.Widgets.AudioMeter()).ToggleMute();
                return;
            }
            if (id == Halo.Launcher.LauncherPages.ActLock) { Win32.LockWorkStation(); return; }
            if (id == Halo.Launcher.LauncherPages.ActSleep)
            {

                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { Win32.SetSuspendState(false, false, false); } catch { }
                });
                return;
            }
            if (id == Halo.Launcher.LauncherPages.ActCopyTranslation)
            {
                Halo.Launcher.LauncherPages.CopyTranslation();
                return;
            }

            if (id.StartsWith(Halo.Launcher.QuickActions.CustomPrefix, StringComparison.Ordinal))
            {
                string slot = id[Halo.Launcher.QuickActions.CustomPrefix.Length..];
                string raw = _settings.Current.Text(
                    Halo.Launcher.QuickActions.CustomKey(int.Parse(slot,
                        System.Globalization.CultureInfo.InvariantCulture)), "");
                if (Halo.Launcher.QuickActions.ParseCustom(raw) is { } custom)
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        { FileName = custom.Target, UseShellExecute = true });
                    }
                    catch { }
                return;
            }
            if (id == Halo.Launcher.QuickActions.Prefix + Halo.Launcher.QuickActions.IdDownloads)
            {
                Open(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads");
                return;
            }
            if (id == Halo.Launcher.QuickActions.Prefix + Halo.Launcher.QuickActions.IdDesktop)
            {

                Win32.keybd_event((byte)Win32.VK_LWIN, 0, 0, UIntPtr.Zero);
                Win32.keybd_event((byte)Win32.VK_D, 0, 0, UIntPtr.Zero);
                Win32.keybd_event((byte)Win32.VK_D, 0, 2, UIntPtr.Zero);
                Win32.keybd_event((byte)Win32.VK_LWIN, 0, 2, UIntPtr.Zero);
                return;
            }
            if (id == Halo.Launcher.QuickActions.Prefix + Halo.Launcher.QuickActions.IdRecycle)
            {

                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { Win32.SHEmptyRecycleBin(IntPtr.Zero, null, 0); } catch { }
                });
                return;
            }
            if (id == Halo.Launcher.QuickActions.Prefix + Halo.Launcher.QuickActions.IdTheme)
            {
                ToggleWindowsTheme();
                return;
            }
            if (id == Halo.Launcher.QuickActions.Prefix + Halo.Launcher.QuickActions.IdSettings)
            {
                OpenSettingsPanel();
                return;
            }
            if (id.StartsWith(Halo.Launcher.ClipboardHistory.ActPrefix, StringComparison.Ordinal))
            {
                string clip = id[Halo.Launcher.ClipboardHistory.ActPrefix.Length..];
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { Halo.Launcher.ClipboardHistory.Restore(clip); } catch { }
                });
                return;
            }
            if (id.StartsWith(Halo.Launcher.ReminderStore.ActPrefix, StringComparison.Ordinal))
                Halo.Launcher.ReminderStore.Remove(id[Halo.Launcher.ReminderStore.ActPrefix.Length..]);
        }
        catch { }
    }

    private bool LauncherInPlace(Halo.Launcher.LauncherRow row)
    {
        string id = row.Id ?? "";
        if (id.StartsWith(Halo.Launcher.LauncherPages.LangPrefix, StringComparison.Ordinal))
        {
            string code = id[Halo.Launcher.LauncherPages.LangPrefix.Length..];
            var which = _box?.State?.Picking ?? Halo.Launcher.LauncherState.LangPick.None;
            if (which == Halo.Launcher.LauncherState.LangPick.None) return false;
            try
            {
                _settings.Set(which == Halo.Launcher.LauncherState.LangPick.From
                    ? Halo.Launcher.Translator.SourceKey : Halo.Launcher.Translator.TargetKey, code);
            }
            catch { }
            _box?.State?.ClosePicker();
            return true;
        }
        if (id == Halo.Launcher.LauncherPages.ActSwapLangs) { SwapLanguages(); return true; }

        if (id.StartsWith(Halo.Launcher.LauncherPages.AddPrefix, StringComparison.Ordinal))
        {
            string text = (_box?.State?.Query ?? "").Trim();

            if (Halo.Launcher.ReminderStore.ParseCommand(text, DateTimeOffset.Now, out _) is { } cmd)
                text = cmd.Text;
            if (text.Length == 0) return true;
            if (long.TryParse(id[Halo.Launcher.LauncherPages.AddPrefix.Length..],
                              System.Globalization.NumberStyles.Integer,
                              System.Globalization.CultureInfo.InvariantCulture, out long unix))
                Halo.Launcher.ReminderStore.Add(DateTimeOffset.FromUnixTimeSeconds(unix), text);
            _box?.State?.Reset();
            return true;
        }

        if (id.StartsWith(Halo.Launcher.ReminderStore.ActPrefix, StringComparison.Ordinal))
        {
            Halo.Launcher.ReminderStore.Remove(id[Halo.Launcher.ReminderStore.ActPrefix.Length..]);
            return true;
        }
        return false;
    }

    private void SwapLanguages()
    {
        try
        {

            var pair = Halo.Launcher.Translator.Swap(Halo.Launcher.LauncherPages.EffectiveSource(),
                                                     Halo.Launcher.LauncherPages.EffectiveTarget(),
                                                     Halo.Launcher.LauncherPages.DetectedSource());
            if (pair is not { } p) return;
            _settings.Set(Halo.Launcher.Translator.SourceKey, p.From);
            _settings.Set(Halo.Launcher.Translator.TargetKey, p.To);
            Halo.Launcher.LauncherPages.SwapTexts();
        }
        catch { }
    }

    private void LauncherSubmitted(string page, string text)
    {
        if (page == Halo.Launcher.LauncherState.PageTranslate) { TranslateSubmitted(text); return; }
        if (page != Halo.Launcher.LauncherState.PageReminders) return;
        try
        {
            var cmd = Halo.Launcher.ReminderStore.ParseCommand(text, DateTimeOffset.Now, out string? complaint);
            if (cmd is not { } ok)
            {
                _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
                {
                    App = "Halo", Title = "reminder not set",
                    Body = complaint ?? "try: in 20m walk the dog, or at 17:30 call mum",
                    Kind = "reminder-help", Duration = 6,
                });
                return;
            }
            Halo.Launcher.ReminderStore.Add(ok.When, ok.Text);
            _box?.State?.Reset();
            _box?.Invalidate();
        }
        catch { }
    }

    private void TranslateSubmitted(string text)
    {
        try
        {
            bool rtl = Halo.Widgets.Fx.IsRtl(text);
            string from = Halo.Launcher.LauncherPages.SourceLang();
            string to = Halo.Launcher.LauncherPages.TargetLang();
            Halo.Launcher.LauncherPages.SetTranslation(text, null, busy: true);
            _box?.State?.Reset();
            _box?.Invalidate();

            System.Threading.ThreadPool.QueueUserWorkItem(async _ =>
            {
                string? got = await Halo.Launcher.Translator.TranslateAsync(text, rtl, from, to);
                try
                {
                    Halo.Launcher.LauncherPages.SetTranslation(text, got, busy: false);
                    _box?.State?.Reset();
                    _box?.Invalidate();
                }
                catch { }
            });
        }
        catch { }
    }

    private void FireDueReminders()
    {
        try
        {
            var all = Halo.Launcher.ReminderStore.Load();
            if (all.Count == 0) return;
            var due = Halo.Launcher.ReminderStore.Due(all, DateTimeOffset.Now);
            if (due.Count == 0) return;
            foreach (var r in due)
                _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
                {
                    App = "Halo", Title = "reminder", Body = r.Text,
                    Kind = "reminder-" + r.Id, Duration = 10,
                });
            Halo.Launcher.ReminderStore.Save(
                Halo.Launcher.ReminderStore.Pending(all, DateTimeOffset.Now));
        }
        catch { }
    }

    private void LauncherChose(Halo.Launcher.LauncherRow row)
    {
        CloseLauncher();
        try
        {
            if (row.Kind == Halo.Launcher.LauncherRowKind.Settings)
            {

                if (!Program.OpenSettingsFromLauncher())
                    _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
                    {
                        App = "Halo", Title = "settings did not open",
                        Body = "Halo.Settings.exe is not next to Halo.App.exe",
                        Kind = "settings-missing", Duration = 6,
                    });
                return;
            }
            if (row.Kind == Halo.Launcher.LauncherRowKind.Action) { RunLauncherAction(row.Id); return; }
            if (row.Kind != Halo.Launcher.LauncherRowKind.App || string.IsNullOrEmpty(row.Aumid)) return;

            _launchStats?.Record(row.Aumid, DateTimeOffset.Now);
            var stats = _launchStats;
            if (stats is not null)
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { stats.Save(Halo.Launcher.LaunchStats.DefaultPath, DateTimeOffset.Now); } catch { }
                });

            LaunchInterrupt(row.Aumid);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "explorer.exe", "shell:AppsFolder\\" + row.Aumid) { UseShellExecute = true });
        }
        catch { }
    }

    private void LaunchInterrupt(string aumid)
    {
        try
        {
            if (!FaceInterrupt.Allowed(FaceWanted, _progress > 0.02f, _notif != null, _ask != null,
                                       _greet != GreetingKind.None, Privacy.Active, _moving,
                                       _handT >= 0f || _drop >= 0f))
                return;

            try
            {
                string pick = System.IO.Path.Combine(HaloDir, "eat-style.txt");
                if (System.IO.File.Exists(pick)
                    && int.TryParse(System.IO.File.ReadAllText(pick).Trim(), out int want))
                {
                    var v = Halo.Widgets.Face.EatStyle.Variants;
                    _eatPick = ((want - 1) % v.Length + v.Length) % v.Length;
                    Halo.Widgets.Face.Eat = v[_eatPick].Style;
                    _eatName = v[_eatPick].Name;
                }
            }
            catch { }
            _handAumid = aumid;
            _handIcon = Halo.Launcher.LauncherIcons.Get(aumid);
            _handProp = Halo.Widgets.FaceProp.AppIcon;

            try
            {
                string want = System.IO.Path.Combine(HaloDir, "face-prop.txt");
                if (System.IO.File.Exists(want)
                    && Enum.TryParse<Halo.Widgets.FaceProp>(
                           System.IO.File.ReadAllText(want).Trim(), true, out var forced)
                    && forced != Halo.Widgets.FaceProp.None)
                {
                    _handProp = forced;
                    _eatName = forced.ToString();
                }
            }
            catch { }
            _handSolo = true;
            _handDone = false;
            _handT = 0f;

            _faceT = 1f;
            _faceAge = 0f;
        }
        catch { }
    }

    private void LauncherFrame()
    {
        if (_box is null || _dim is null) return;
        try
        {
            _box.Frame(_dt);

            float target = _box.Opening ? DimTarget : 0f;
            _dimT += Math.Clamp(_dt / DimSeconds, 0f, 1f) * (target - _dimT);
            if (Math.Abs(target - _dimT) < 0.004f) _dimT = target;

            if (_dimT <= 0f) { if (_dim.Visible) _dim.Hide(); }
            else _dim.SetAlpha((byte)Math.Clamp(_dimT * 255f, 0f, 255f));
        }
        catch { }
    }

    private bool FaceOnly => _faceT > 0f && (_empty || FacePinned || _handT >= 0f);

    private static readonly bool VisDebug = Environment.GetEnvironmentVariable("HALO_VISDEBUG") == "1";

    private void LogVis(string what, IntPtr fg, int activeLen)
    {
        if (!VisDebug) return;
        try
        {
            System.IO.File.AppendAllText(VisDebugPath,
                $"{DateTime.Now:HH:mm:ss.fff} {what,-13} empty={_empty,-5} shrink={_shrink:0.000} "
                + $"progress={_progress:0.000} primary={PrimaryWidgetName(),-18} active={activeLen} "
                + $"pinned={Pinned(_pinned),-5} fg={LayeredNotch.ClassNameOf(fg)}\r\n");
        }
        catch { }
    }

    private void CheckWake()
    {
        var now = DateTime.UtcNow;
        var gap = now - _lastTickUtc;
        _lastTickUtc = now;

        if (gap < WakeGap || _greet != GreetingKind.None || _notif != null || _ask != null) return;

        if (GreetingGate.Take(GreetedPath, DateOnly.FromDateTime(DateTime.Now), arriving: false, GreetingWanted)
            == GreetingKind.None) return;
        _greet = GreetingKind.Login;
        _greetT = 0f;

        _greetArmed = false; _greetHeld = 0f; _greetWaited = 0f;
    }

    private static bool ScreenWatchable()
    {
        try
        {
            if (Win32.FindWindow("Shell_TrayWnd", null) == IntPtr.Zero) return false;
            var d = Win32.OpenInputDesktop(0, false, 0x0001);
            if (d == IntPtr.Zero) return false;
            Win32.CloseDesktop(d);
            return true;
        }
        catch { return false; }
    }

    private float _dt = 0.008f;
    private long _lastFrameAt;
    private void Frame()
    {
        DrainPosted();

        long frameNow = Environment.TickCount64;
        _dt = _lastFrameAt == 0 ? 0.008f : Math.Clamp((frameNow - _lastFrameAt) / 1000f, 0.001f, 0.05f);
        _lastFrameAt = frameNow;
        PollDisplay();
        LauncherFrame();
        RefreshFeatureMask();
        AdaptFrameRate();
        EaseRings();
        CheckAlerts();

        Halo.ClaudeCode.HookConnect.Tick((app, title, body, ok) =>
            _notifSrc.EnqueueLocal(HookBanner((app, title, body), ok)));
        var notifStart = _notif;
        var fg = Win32.GetForegroundWindow();
        DetectAgentCancel(fg);
        DetectLanguageChange(fg);

        _fgFullscreen = _notch.IsFullscreen(fg);
        bool fullscreen = !Pinned(_pinned) && _fgFullscreen;

        _overFullscreen = fullscreen;

        for (int i = 0; i < _widgets.Length; i++)
            if (_widgets[i] is Widgets.NetWidget nw) { nw.Pinned = i == _userPicked; LogNet(nw, i); }
        var active = fullscreen ? [] : ActiveIndices();

        if (!fullscreen)
        {
            for (int i = 0; i < _widgets.Length; i++)
            {
                try { if (FeatureOn(i)) _widgets[i].Tick(); }
                catch { }
            }
        }

        bool notifLive = _notif != null || _notifSrc.HasPending;

        var visibility = NotchVisibility.Decide(_userHidden || (fullscreen && !notifLive),
                                                _hiddenForFullscreen);
        _hiddenForFullscreen = visibility.HiddenForFullscreen;

        bool justShown = visibility.Action == NotchVisibilityAction.ShowAndRender;
        if (visibility.Action == NotchVisibilityAction.Hide)
        {
            LogVis("hide", fg, active.Length);
            _notch.SetVisible(false);
        }
        else if (justShown)
        {
            LogVis("show:frozen", fg, active.Length);
            if (active.Length > 0 && Array.IndexOf(active, _primary) < 0)
            {
                _primary = PreferredPrimary(active);
                _agentNotices.SetPrimary(_primary);
            }

            (_empty, _shrink) = NotchVisibility.Settled(active.Length);
            _notch.SetVisible(true);
            _lastFg = IntPtr.Zero;
            Apply(_progress);
            LogVis("show:applied", fg, active.Length);
        }

        if (visibility.ReturnEarly)
            return;

        bool holding = HoverHold.Holding(WidgetInput.Over, _progress, _notif != null, _drop >= 0f);
        active = HoverHold.Keep(active, _primary, holding);

        bool wasEmpty = _empty;
        _empty = active.Length == 0;

        if (_empty) _handDone = false;
        if (_wasActive.Length != _widgets.Length) _wasActive = new bool[_widgets.Length];
        int dressed = -1;
        foreach (int i in active)
            if (!_wasActive[i] && _widgets[i].ArrivingProp != Halo.Widgets.FaceProp.None) { dressed = i; break; }
        if (_faceT > 0.5f && _handT < 0f)
        {

            _handSolo = false;
            _handIcon = null;
            if (dressed >= 0)
            {
                _handProp = _widgets[dressed].ArrivingProp;
                _handT = 0f;
            }

            else if (wasEmpty && !_empty && active.Length > 0)
            {
                int arriving = PreferredPrimary(active);
                _handProp = arriving >= 0 && arriving < _widgets.Length
                    ? _widgets[arriving].ArrivingProp : Halo.Widgets.FaceProp.None;
                _handT = 0f;
            }
        }
        Array.Clear(_wasActive);
        foreach (int i in active) _wasActive[i] = true;
        if (justShown) LogVis("show:settled", fg, active.Length);

        if (!_empty && _drop < 0f && Array.IndexOf(active, _primary) < 0)
        {
            _primary = PreferredPrimary(active);
            _agentNotices.SetPrimary(_primary);
        }

        var now = DateTimeOffset.UtcNow;
        for (int i = 0; i < _widgets.Length; i++)
        {
            bool desktopBacked = _widgets[i] is CodexWidget codex && codex.IsDesktop;
            _agentNotices.Observe(i, _widgets[i].AgentNotice, now, desktopBacked, allowSelection: _drop < 0f);
        }

        if (holding) _agentNotices.Hold(now.AddSeconds(HoverHold.GraceSeconds));

        _agentNotices.Tick(now, i => Live(i) || (holding && i == _primary), allowSelection: _drop < 0f);
        if (_drop < 0f)
            _primary = _agentNotices.Primary;
        if (!_empty && Array.IndexOf(active, _primary) < 0)
        {
            _primary = PreferredPrimary(active);
            _agentNotices.SetPrimary(_primary);
        }

        if (_userPicked >= 0 && Array.IndexOf(active, _userPicked) < 0) _userPicked = -1;

        if (_drop < 0f && !_empty && _userPicked < 0 && _widgets[_primary].AgentNotice.State != "working")
        {
            int best = -1; long bestRank = 0;
            foreach (var i in active)
                if (_widgets[i] is ClaudeCodeWidget && _widgets[i].AgentNotice.State == "working"
                    && (best < 0 || _widgets[i].ActivityRank > bestRank))
                {
                    best = i;
                    bestRank = _widgets[i].ActivityRank;
                }
            if (best >= 0)
            {
                _primary = best;
                _agentNotices.SetPrimary(best);
            }
        }

        if (_drop < 0f && !_empty && active.Length > 1 && _primary != _userPicked
            && _widgets[_primary] is ClaudeCodeWidget)
        {
            Win32.GetWindowThreadProcessId(fg, out uint fpid);
            if (fpid != 0 && FgHostsWidget((int)fpid, _primary))
                foreach (var i in active)
                    if (i != _primary && !FgHostsWidget((int)fpid, i))
                    {
                        _primary = i;
                        _agentNotices.SetPrimary(i);
                        break;
                    }
        }
        bool notice = _drop < 0f && _agentNotices.IsOpen(now);

        if (_drop < 0f && !_empty && _userPicked < 0 && !notice)
        {
            int rank = -1;
            for (int i = 0; i < _widgets.Length && rank < 0; i++)
                if (_widgets[i] is NetWidget && Live(i)) rank = i;
            for (int i = 0; i < _widgets.Length && rank < 0; i++)
                if (_widgets[i] is DownloadWidget && Live(i)) rank = i;
            if (rank >= 0) { _primary = rank; _agentNotices.SetPrimary(rank); }
        }

        if (_drop < 0f && _btWidget.IsActive && _settings.Enabled(Halo.Settings.FeatureId.Bluetooth))
            for (int i = 0; i < _widgets.Length; i++)
                if (_widgets[i] is BtWidget) { _primary = i; _agentNotices.SetPrimary(i); break; }

        if (_prevDragActive && !FileTray.DragActive) _trayShowUntil = Environment.TickCount64 + 2500;
        _prevDragActive = FileTray.DragActive;
        if (_drop < 0f && (FileTray.DragActive || Environment.TickCount64 < _trayShowUntil)
            && _settings.Enabled(Halo.Settings.FeatureId.FileTray))
            for (int i = 0; i < _widgets.Length; i++)
                if (_widgets[i] is FileTray)
                { _primary = i; _agentNotices.SetPrimary(i); break; }

        for (int i = 0; i < _widgets.Length; i++)
        {
            bool isAct = Live(i);
            if (isAct && !_prevActive[i] && !fullscreen && _drop < 0f)
            {
                if (i == _primary) _arrive = 0f;
                else if (_progress < 0.1f)
                {
                    _pending = _primary;
                    _dropOut = true;
                    _dropIcon = _widgets[i].Icon;
                    _dropImage = _widgets[i].IconImage;
                    _dropCX = _dropCY = LayeredNotch.CircleD / 2f;
                    _drop = 0f;
                }
            }
            _prevActive[i] = isAct;
        }

        Win32.GetCursorPos(out var p);

        bool pointerMoved = p.X != _ptrX || p.Y != _ptrY;
        _ptrX = p.X; _ptrY = p.Y;

        float prevGreetT = _greetT;
        var prevGreet = _greet;
        CheckWake();
        if (_greet != GreetingKind.None)
        {
            if (_asks.Pending != null) { _greet = GreetingKind.None; _greetT = 0f; }
            else if (!_greetArmed)

                (_greetHeld, _greetWaited, _greetArmed) =
                    GreetingArm.Step(ScreenWatchable(), _greetHeld, _greetWaited, _dt);
            else
            {
                float secs = GreetingPlan.SecondsOf(_greet);
                _greetT += _dt / secs;
                if (_greetT >= 1f) { _greetT = 0f; _greet = GreetingKind.None; }
            }
        }

        if (_askTyped != null || _asks.Pending != null || _greet != GreetingKind.None
            || !_settings.Enabled(Halo.Settings.FeatureId.Notifications))
        { while (_notifSrc.Dequeue() is not null) { } }
        else if (_notif == null && !_notifClosing && _progress <= 0.02f && _drop < 0f
            && _notifSrc.Dequeue() is { } item)
        {
            _notif = item;
            _notifDetailOn = false;
            _notifDetail = 0f;
            _notifFold = 1f;
            _notifDetailH = NotifBanner.DetailHeight(item);
            _notifDeadline = DateTime.UtcNow.AddSeconds(item.Duration);
        }

        else if (_notif is { } live && !_notifClosing && _notifSrc.DequeueFoldable(live) is { } more)
        {
            live.Body = Halo.Notifications.LiveText.Append(live.Body, more.Body);
            live.Time = more.Time;

            _notifFold = 0f;
            live.Duration = Halo.Notifications.LiveText.Extend(live.Duration);
            _notifDetailH = NotifBanner.DetailHeight(live);

            _notifDeadline = DateTime.UtcNow.AddSeconds(live.Duration);

            _notifInk++;
        }

        if (_notif != null && _asks.Pending != null && !_notifDetailOn) _notifClosing = true;
        float prevAskT = _askT;
        int prevAskHover = _askHover;
        var pendingAsk = _notif == null && _settings.Current.Bool("claude.ask", true) ? _asks.Pending : null;

        if (pendingAsk != null && pendingAsk.Nonce == _askDismissed) pendingAsk = null;
        if (pendingAsk?.Nonce != _ask?.Nonce)
        {
            EndTyping();
            _ask = pendingAsk;
            _askHover = -1;

            if (_ask != null && _askDraftNonce == _ask.Nonce && _askDraft.Length > 0) BeginTyping();
        }

        if (_ask != null)
        {
            _askChips = AskBanner.Chips(_ask, AskBanner.W);
            _askH = AskBanner.Height(_ask, AskBanner.W);

            LayeredNotch.WantCaptureHeight(_askH);
        }

        else if (_notif == null && _askGhost == null) LayeredNotch.WantCaptureHeight(0);
        _askT = Math.Clamp(_askT + (_ask != null ? _dt / 0.24f : -_dt / 0.30f), 0f, 1f);
        if (_askT <= 0f) _askGhost = null;
        if (_ask != null)
        {
            _askHover = -1;
            _askCloseHover = false;
            if (InRect(p, NotifLeft(), _ct, Sc(_curW), Sc(_curH)))
            {
                _askCloseHover = InChip(p, AskBanner.CloseRect(AskBanner.W));
                if (!_askCloseHover)
                    for (int i = 0; i < _askChips.Count; i++)
                        if (InChip(p, _askChips[i].Rect)) { _askHover = i; break; }
            }
        }

        float prevPanelT = _panelT;
        int prevPanelHover = _panelHover;
        bool prevPanelCloseHover = _panelCloseHover;
        var livePanel = _notif == null && _ask == null ? _panels.Current : null;
        if (livePanel is { } shown)
        {
            _panelGhost = shown;

            _panelH = (int)Math.Ceiling(Halo.Panels.PanelLayout.Height(shown.Spec));
            LayeredNotch.WantCaptureHeight(_panelH);
        }
        _panelT = Math.Clamp(_panelT + (livePanel != null ? _dt / 0.24f : -_dt / 0.30f), 0f, 1f);
        if (_panelT <= 0f) _panelGhost = null;
        if (livePanel is { } hit)
        {
            _panelHover = -1;
            _panelCloseHover = false;
            if (InRect(p, NotifLeft(), _ct, Sc(_curW), Sc(_curH)))
            {
                _panelCloseHover = InChip(p, Halo.Panels.PanelLayout.CloseRect(Halo.Panels.PanelLayout.Width));
                if (!_panelCloseHover)
                    _panelHover = Halo.Panels.PanelHit.RowAt(hit.Spec, Halo.Panels.PanelLayout.Width, PanelLocal(p));
            }
        }

        float prevNotifT = _notifT, prevNotifDetail = _notifDetail, prevNotifFold = _notifFold;
        bool overNotif = false;
        if (_notif != null)
        {
            overNotif = InRect(p, NotifLeft(), _ct, Sc(_curW), Sc(_curH));
            if (overNotif && !_notifDetailOn && _notif.Kind != "language")
                _notifDeadline = Max(_notifDeadline, DateTime.UtcNow.AddSeconds(2.5));
            if (!_notifDetailOn && DateTime.UtcNow > _notifDeadline) _notifClosing = true;

            _notifT = Math.Clamp(_notifT + (_notifClosing ? -_dt / 0.34f : _dt / 0.42f), 0f, 1f);
            _notifDetail = Math.Clamp(_notifDetail + (_notifDetailOn ? 1 : -1) * _dt / 0.22f, 0f, 1f);
            _notifFold = Math.Clamp(_notifFold + _dt / FoldSecs, 0f, 1f);
            if (_notifClosing && _notifT <= 0f)
            {
                _notif = null;
                _notifClosing = false;
                _notifDetailOn = false;
                _notifDetail = 0f;
            }
        }

        bool down = (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0;
        bool inHandle = _progress > 0.9f
            && p.X >= _el + Sc(ExpandedW - 44) && p.X < _el + Sc(ExpandedW) + 8
            && p.Y >= _et + Sc(ExpandedH - 44) && p.Y < _et + Sc(ExpandedH) + 8;
        bool rescaled = false;
        if (_resizing)
        {
            if (down)
            {

                float ns = Math.Clamp(_scale0
                    + ((p.X - _resizeFrom.X) + (p.Y - _resizeFrom.Y)) / ((ExpandedW + ExpandedH) * _notch.Dpi),
                    0.7f, 1.6f);
                rescaled = ns != _notch.Scale;
                _notch.Scale = ns;
            }
            else { _resizing = false; _notch.SaveScale(); }
        }
        else if (down && !_lastMouseDown && inHandle && !_moving)
        {
            _resizing = true;
            _resizeFrom = p;
            _scale0 = _notch.Scale;
        }
        float prevHandle = _handle;
        _handle = Math.Clamp(_handle + (inHandle || _resizing ? 1 : -1) * _dt / 0.12f, 0f, 1f);
        _notch.HandleAlpha = _handle;

        bool hovered = _resizing || _moving || (_progress > 0.02f
            ? InRect(p, _el, _et, Sc(ExpandedW), Sc(ExpandedH))
            : InRect(p, _cl, _ct, Sc(CollapsedW), Sc(CollapsedH)));
        float prevOffsetX = _offsetX, prevHoldT = _holdT;
        UpdateMove(p, down, hovered);

        bool open = (hovered || notice || FileTray.DragActive || _apiHold || PinOpen)
            && !_empty && _notif == null && !_moving;

        int dir = open ? 1 : -1;

        float step = _dt / ((open ? OpenSeconds : CloseSeconds) * MotionScale);

        float next = open && FileTray.DragActive ? 1f : Math.Clamp(_progress + dir * step, 0f, 1f);

        int alt = AltIndices().Length;
        bool inMenu = _progress < 0.05f && _drop < 0f && InMenu(p);
        float mnext = alt >= 2 && inMenu ? Math.Min(_menu + step, 1f) : Math.Max(_menu - step, 0f);

        var rows = Groups();
        int hoverRow = -1;
        if (inMenu && p.Y >= _ct)
        {
            int r0 = (p.Y - _ct) / Sc(LayeredNotch.CircleD);
            if (r0 >= 0 && r0 < rows.Count) hoverRow = r0;
        }
        if (hoverRow != _row && hoverRow >= 0) { _row = hoverRow; _rowOpen = 0f; }
        float rnext = _row >= 0 && _row < rows.Count && rows[_row].Length >= 2 && inMenu && hoverRow == _row
            ? Math.Min(_rowOpen + step, 1f)
            : Math.Max(_rowOpen - step, 0f);
        if (mnext <= 0f && rnext <= 0f) _row = -1;

        float dnext = _drop;
        if (_drop >= 0f)
        {
            dnext = _drop + _dt / 0.34f;
            if (dnext >= 1f)
            {
                if (!_dropOut) { _primary = _pending; _agentNotices.SetPrimary(_primary); _arrive = 0f; _userPicked = _pending; }
                _dropOut = false;
                dnext = -1f;
            }
        }

        float anext = _arrive;
        if (_arrive >= 0f) { anext = _arrive + _dt / 0.22f; if (anext >= 1f) anext = -1f; }

        float prevMenu = _menu, prevDrop = _drop, prevArrive = _arrive, prevRowOpen = _rowOpen;
        _menu = mnext;
        _rowOpen = rnext;
        _drop = dnext;
        _arrive = anext;
        PollClick(p);
        HandleTrayInteraction(p, down);

        bool startExpand = _progress <= 0.02f && next > 0.02f;
        bool deskChanged = false;
        if (_askTyped == null && (fg != _lastFg || startExpand))
        {

            bool follow = _settings.Current.Bool(Halo.Settings.SettingsKeys.FollowFocus, true);
            if (follow && fg != _lastFg && _drop < 0f && !_agentNotices.IsOpen(now))
                FollowForeground(fg);
            if (follow && fg != _lastFg) FollowForegroundMedia(ProcessNameOf(fg));
            _lastFg = fg;
            bool desk = _notch.ProbeBehind(out _behind);
            deskChanged = desk != _lastDesktop;
            _lastDesktop = desk;
            if (deskChanged && !desk) _lastCaptureAt = 0;
        }

        if (_empty && _askTyped == null && _lastFrameAt - _deskPolledAt >= DeskPollMs)
        {
            _deskPolledAt = _lastFrameAt;
            bool idleDesk = _notch.ProbeBehind(out _behind);
            if (idleDesk != _lastDesktop)
            {
                deskChanged = true;
                _lastDesktop = idleDesk;
                if (!idleDesk) _lastCaptureAt = 0;
            }
        }

        bool sheet = _progress > 0.5f || _notif != null || _ask != null || _greet != GreetingKind.None;
        _sheetDbg = sheet;
        int captureEveryMs = sheet ? CaptureOpenMs : CaptureCollapsedMs;
        if (_heavy) captureEveryMs *= 3;

        if (!sheet) captureEveryMs *= Math.Clamp(1 + _notch.StaleStreak / 6, 1, 4);
        if (!_lastDesktop && _behind != IntPtr.Zero && frameNow - _lastCaptureAt >= captureEveryMs)
        {

            if (sheet)
                LayeredNotch.GlassNote($"req every={captureEveryMs} late={frameNow - _lastCaptureAt} "
                    + $"prog={_progress:0.00} heavy={_heavy} stale={_notch.StaleStreak} cad={_cadence}");
            _lastCaptureAt = frameNow;
            _notch.CaptureFrom(_behind);
        }
        int cv = _notch.CaptureVersion;
        bool refreshed = cv != _lastCaptureVer;
        _lastCaptureVer = cv;

        bool tick = DateTime.Now.Second != _lastSec;
        _lastSec = DateTime.Now.Second;

        bool forceAnim = false;

        if (_cue.Alive(Environment.TickCount64)) forceAnim = true;
        bool animating = _widgets[_primary].Animating;

        bool sprint = animating && _widgets[_primary].Sprinting;
        if (animating && (_progress >= 0.5f || sprint)) forceAnim = true;

        else if (animating && _lastFrameAt - _animDrewAt >= 16) { _animDrewAt = _lastFrameAt; forceAnim = true; }

        if (_faceT > 0f && _lastFrameAt - _faceDrewAt >= 16) { _faceDrewAt = _lastFrameAt; forceAnim = true; }

        bool overNow = _notif != null ? overNotif : hovered && next > 0.98f;
        var mouse = _notif != null
            ? new PointF((p.X - NotifLeft()) / S, (p.Y - _ct) / S)
            : new PointF((p.X - _el) / S, (p.Y - _et) / S);
        bool mouseMoved = WidgetInput.Over != overNow || (overNow && WidgetInput.Mouse != mouse);
        WidgetInput.Over = overNow;
        WidgetInput.Mouse = mouse;

        WidgetInput.Down = (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0;

        bool wheelWidget = _primary >= 0 && _primary < _widgets.Length && _widgets[_primary].WantsWheel;
        Halo.Interop.WheelGrab.WantWheel = overNow && wheelWidget && next > 0.98f;
        int notches = Halo.Interop.WheelGrab.TakeNotches();
        bool wheeled = false;
        if (notches != 0 && wheelWidget)
        {
            try { _widgets[_primary].Wheel(notches); } catch { }
            wheeled = true;
        }

        float prevStrip = _stripT;
        _stripT = Math.Clamp(_stripT + (AltIndices().Length >= 1 ? 1 : -1) * _dt / 0.22f, 0f, 1f);

        float prevHandT = _handT;

        float beat = Halo.Widgets.FaceDirector.HandSeconds(_handProp);
        bool melting = _handT >= beat;
        if (_handT >= 0f)
        {
            _handT += _dt;

            if (_handSolo && _handProp == Halo.Widgets.FaceProp.AppIcon && _handIcon is null)
            {
                _handIcon = Halo.Launcher.LauncherIcons.Get(_handAumid);
                if (_handIcon is null && _handT >= Halo.Widgets.FaceDirector.NoticeEnd)

                    _handProp = Halo.Widgets.FaceProp.Search;
            }

            if (_handT >= beat + Halo.Widgets.FaceDirector.MeltSeconds || (_empty && !_handSolo))
            {
                _handT = -1f;
                _faceT = 0f;
                _handDone = true;
                _handSolo = false;
                _handIcon = null;
                _handAumid = null;
            }
        }

        float prevShrink = _shrink;
        if (_handT >= 0f && !melting) _shrink = 1f;
        else _shrink = Math.Clamp(_shrink + (_empty ? 1 : -1) * _dt / 0.28f, 0f, 1f);

        if (_notif != null && _faceT > 0.4f) _notifFloat = true;
        else if (_notif == null && _notifT <= 0.001f) _notifFloat = false;

        float prevFaceT = _faceT;
        bool faceWakes = FaceWakes;
        FaceDebug(faceWakes);
        if (_handT < 0f)
            _faceT = Math.Clamp(_faceT + (faceWakes ? 1 : -1) * _dt / Halo.Widgets.FaceDirector.FadeSeconds, 0f, 1f);
        else if (melting)

            _faceT = Math.Max(0f, _faceT - _dt / Halo.Widgets.FaceDirector.MeltSeconds);

        if (_faceT > 0f) _faceAge += _dt;
        else _faceAge = 0f;

        int wv = WidgetVersion();

        float prevGaze = _gazeX + _gazeY + _near;
        GazeFrame();
        float prevGrip = _catGrip, prevDuck = _catDuck;
        if (CatDrop > 0f)
            CatFrame(new System.Drawing.RectangleF(0.5f, 0.5f, _curW - 1f, _curH - 1f));
        else _catGrip = _catDuck = 0f;

        bool changed = next != _progress || wv != _widgetVersion || deskChanged || wasEmpty != _empty
            || refreshed || tick || _menu != prevMenu || _drop != prevDrop || _arrive != prevArrive
            || _rowOpen != prevRowOpen || forceAnim || mouseMoved || rescaled || _handle != prevHandle
            || _shrink != prevShrink || _faceT != prevFaceT || _handT != prevHandT
            || _catGrip != prevGrip || _catDuck != prevDuck

            || _gazeX + _gazeY + _near != prevGaze

            || _catGrip > 0.01f
            || _stripT != prevStrip || _notifT != prevNotifT || _notifDetail != prevNotifDetail
            || wheeled
            || _offsetX != prevOffsetX || _holdT != prevHoldT || !ReferenceEquals(_notif, notifStart)
            || _notifInk != _drawnNotifInk || _notifFold != prevNotifFold
            || _askT != prevAskT || _askHover != prevAskHover || _askTyped != _drawnTyped
            || _greetT != prevGreetT || _greet != prevGreet

            || _carryDY != _drawnCarryDY || _dragRow != _drawnDragRow || _carryDX != _drawnCarryDX

            || _dragHeld >= DragHold;

        bool morphing = next != _progress || _notifT != prevNotifT || _askT != prevAskT || _shrink != prevShrink
            || sprint;

        int ss = morphing || _ask != null || _askGhost != null || _panelGhost != null ? 1 : 2;
        LayeredNotch.Supersample = ss;
        if (ss != _drawnSs) changed = true;

        bool watched = hovered || notice
                       || _notif != null || _ask != null || _greet != GreetingKind.None;

        bool glassLive = GlassWantsFineTimer(sheet, watched, _lastDesktop, _notch.StaleStreak);
        _raiseDbg = $"sheet={sheet} watched={watched} desk={_lastDesktop} stale={_notch.StaleStreak} live={glassLive} morph={morphing}";
        RaiseTimer(morphing || glassLive
                   || (next > 0.5f && animating && (watched || _apiHold || FileTray.DragActive)),
                   pointerMoved);
        if (morphing != _morphing) { _morphing = morphing; ApplyCadence(); }

        bool steady = !morphing && watched;
        if (_morphRate.Step(morphing, _dt) | _steadyRate.Step(steady, _dt))
            RateReport.Write(_morphRate.Measured, _displayHz, _steadyRate.Measured);

        if (Halo.Reports.ShapeReport.Due)
            Halo.Reports.ShapeReport.Write(PrimaryWidgetName(), LiveWidgetNames(),
                                          _progress > 0.9f, _heavy, FpsCeiling);
        _progress = next;
        _widgetVersion = wv;

        _drawnTyped = _askTyped;
        _drawnCarryDY = _carryDY;
        _drawnCarryDX = _carryDX;
        _drawnDragRow = _dragRow;
        if (changed) Apply(_progress);
        _drawnSs = ss;
        _drawnNotifInk = _notifInk;
    }

    private PointF PanelLocal(Win32.POINT p)
        => new((p.X - NotifLeft()) / S, (p.Y - _ct) / S);

    private bool InChip(Win32.POINT p, RectangleF r)
        => p.X >= NotifLeft() + r.X * S && p.X < NotifLeft() + r.Right * S
        && p.Y >= _ct + r.Y * S && p.Y < _ct + r.Bottom * S;

    private bool InMenu(Win32.POINT p)
    {
        var rows = Groups();
        if (rows.Count == 0) return false;
        int D = Sc(LayeredNotch.CircleD);
        int x = _cl + Sc(CollapsedW + LayeredNotch.CircleGap + LayeredNotch.PrivacyPad);
        float openV = EaseOutBack(Math.Clamp(_menu, 0f, 1f));
        float hNow = D + (rows.Count - 1) * D * Math.Max(0f, openV);
        if (p.X >= x && p.X < x + D && p.Y >= _ct && p.Y < _ct + Math.Max(D, hNow))
            return true;
        if (_row >= 0 && _row < rows.Count && _rowOpen > 0f)
        {
            float ext = rows[_row].Length * D * EaseOutBack(Math.Clamp(_rowOpen, 0f, 1f));
            if (p.X >= x + D && p.X < x + D + ext
                && p.Y >= _ct + _row * D && p.Y < _ct + (_row + 1) * D)
                return true;
        }
        return false;
    }

    private readonly Dictionary<int, Color> _ringShown = new();
    private void EaseRings()
    {
        for (int i = 0; i < _widgets.Length; i++)
        {
            if (_widgets[i].Ring is not { } target) { _ringShown.Remove(i); continue; }
            if (!_ringShown.TryGetValue(i, out var shown)) { _ringShown[i] = target; continue; }
            float k = 1f - MathF.Exp(-_dt / 0.22f);
            _ringShown[i] = Color.FromArgb(
                (int)MathF.Round(shown.A + (target.A - shown.A) * k),
                (int)MathF.Round(shown.R + (target.R - shown.R) * k),
                (int)MathF.Round(shown.G + (target.G - shown.G) * k),
                (int)MathF.Round(shown.B + (target.B - shown.B) * k));
        }
    }

    private Color? RingOf(int i)
        => _widgets[i].Ring is { } target ? (_ringShown.TryGetValue(i, out var c) ? c : target) : null;

    private Color? GroupRing(int[] gr)
    {
        Color? first = null;
        foreach (var i in gr)
        {
            if (RingOf(i) is not { } rc) continue;
            first ??= rc;
            if (rc.R != rc.G || rc.G != rc.B) return rc;
        }
        return first;
    }

    private List<int[]> Groups()
    {
        var byKind = new Dictionary<string, List<int>>();
        var order = new List<string>();
        foreach (var i in AltIndices())
        {
            string kind = KindOf(_widgets[i]);
            if (!byKind.TryGetValue(kind, out var list)) { list = new List<int>(); byKind[kind] = list; order.Add(kind); }
            list.Add(i);
        }

        _stripKinds = _stripOrder.Apply(order);

        return _stripKinds.ConvertAll(k => OrderSessions(byKind[k]));
    }

    private int[] OrderSessions(List<int> members)
    {
        var byActivity = members.OrderByDescending(i => _widgets[i].ActivityRank).ToList();
        var keys = byActivity.ConvertAll(i => _widgets[i].SessionKey);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (keys.Exists(string.IsNullOrEmpty) || keys.Exists(k => !seen.Add(k)))
            return [.. byActivity];

        var ranked = _sessionOrder.Apply(keys);
        var result = new List<int>(byActivity.Count);
        foreach (var key in ranked) result.Add(byActivity[keys.IndexOf(key)]);
        return [.. result];
    }

    private static string KindOf(IWidget w) => w switch
    {
        MediaWidget => "media",
        VlcWidget => "vlc",

        NetWidget => "net",
        DownloadWidget => "download",
        FileTray => "filetray",
        ClaudeCodeWidget => "claude",
        CodexWidget => "codex",
        GenericAgentWidget ga => "g:" + ga.GroupKey,
        _ => "other",
    };

    private int PreferredPrimary(int[] active)
    {
        if (active.Length == 0) return _primary;
        var kinds = new List<string>(active.Length);
        foreach (var i in active)
        {
            var k = KindOf(_widgets[i]);
            if (!kinds.Contains(k)) kinds.Add(k);
        }
        string first = _stripOrder.Apply(kinds)[0];
        int best = -1; long bestRank = 0;
        foreach (var i in active)
            if (KindOf(_widgets[i]) == first)
            {
                long r = _widgets[i].ActivityRank;
                if (best < 0 || r > bestRank) { best = i; bestRank = r; }
            }
        return best >= 0 ? best : active[0];
    }

    private readonly Halo.Settings.SettingsStore _settings;

    private bool Live(int i)
    {
        try { return _widgets[i].IsActive && FeatureOn(i); }
        catch { return false; }
    }

    private bool FeatureOn(int i)
    {
        if (_featureMask is { } mask && i < mask.Length) return mask[i];
        var feature = FeatureOf(_widgets[i]);
        return feature is null || _settings.Enabled(feature.Value);
    }

    private void RefreshFeatureMask()
    {
        var mask = _featureMask;
        if (mask is null || mask.Length != _widgets.Length) mask = new bool[_widgets.Length];
        for (int i = 0; i < _widgets.Length; i++)
        {
            try
            {
                var feature = FeatureOf(_widgets[i]);
                mask[i] = feature is null || _settings.Enabled(feature.Value);
            }
            catch { mask[i] = false; }
        }
        _featureMask = mask;
    }

    private static Halo.Settings.FeatureId? FeatureOf(IWidget widget) => widget switch
    {
        MediaWidget or VlcWidget => Halo.Settings.FeatureId.Media,
        DownloadWidget => Halo.Settings.FeatureId.Downloads,
        FileTray => Halo.Settings.FeatureId.FileTray,
        Widgets.BtWidget => Halo.Settings.FeatureId.Bluetooth,
        ClaudeCodeWidget => Halo.Settings.FeatureId.ClaudeCode,
        CodexWidget => Halo.Settings.FeatureId.Codex,
        GenericAgentWidget => Halo.Settings.FeatureId.GenericAgents,
        _ => null,
    };

    private int[] ActiveIndices()
    {
        var active = new List<int>(_widgets.Length);
        for (int i = 0; i < _widgets.Length; i++)
            if (Live(i))
                active.Add(i);
        return [.. active];
    }

    private int[] AltIndices()
    {
        var act = ActiveIndices();
        int n = 0;
        foreach (var i in act) if (i != _primary) n++;
        var r = new int[n];
        int j = 0;
        foreach (var i in act) if (i != _primary) r[j++] = i;
        return r;
    }

    private int WidgetVersion()
    {
        int v = Privacy.Version;
        v += _settings.Version;
        foreach (var wgt in _widgets) v += wgt.Version;
        return v;
    }

    private const float DragHold = 0.26f;

    private bool UpdateStripGesture(Win32.POINT p, bool down)
    {
        bool live = _progress < 0.1f && ActiveIndices().Length >= 2 && _drop < 0f && _notif == null
                    && _ask == null && _greet == GreetingKind.None;
        int D = Sc(LayeredNotch.CircleD);

        if (down && !_lastMouseDown)
        {
            if (!live || !InMenu(p)) return false;
            _dragRow = Math.Clamp((p.Y - _ct) / D, 0, Math.Max(0, Groups().Count - 1));
            _dragFromY = p.Y;
            _dragFromX = p.X;
            _dragHeld = 0f;

            int mx = _cl + Sc(CollapsedW + LayeredNotch.CircleGap + LayeredNotch.PrivacyPad);
            int rel = (p.X - mx) / D;
            var rowsNow = Groups();
            _dragSess = rel >= 1 && _row == _dragRow && _rowOpen > 0f
                        && _dragRow < rowsNow.Count && rowsNow[_dragRow].Length >= 2
                ? Math.Clamp(rel - 1, 0, rowsNow[_dragRow].Length - 1)
                : -1;
            return true;
        }
        if (_dragRow < 0) return false;
        if (!live) { _dragRow = -1; return false; }

        if (down)
        {
            _dragHeld += _dt;
            if (_dragHeld < DragHold) { _carryDY = 0f; return true; }

            if (_dragSess >= 0)
            {
                _carryWantX = (p.X - _dragFromX) / S;
                _carryDX = Lerp(_carryDX, _carryWantX, Math.Clamp(_dt / 0.045f, 0f, 1f));
                _carryDY = 0f;

                var rowsHeld = Groups();
                int hsteps = (int)((p.X - _dragFromX) / D);
                if (hsteps != 0 && _dragRow < rowsHeld.Count && _dragSess < rowsHeld[_dragRow].Length)
                {
                    string skey = _widgets[rowsHeld[_dragRow][_dragSess]].SessionKey;
                    var present = new List<string>();
                    foreach (var i in rowsHeld[_dragRow]) present.Add(_widgets[i].SessionKey);
                    if (skey.Length > 0 && _sessionOrder.Move(present, skey, hsteps))
                    {
                        _sessionOrder.Save(SessionOrderPath);
                        _dragSess = Math.Clamp(_dragSess + hsteps, 0, rowsHeld[_dragRow].Length - 1);
                        _dragFromX += hsteps * D;
                        _carryWantX = (p.X - _dragFromX) / S;
                        _carryDX -= hsteps * LayeredNotch.CircleD;
                    }
                }
                return true;
            }

            _carryWant = (p.Y - _dragFromY) / S;
            _carryDY = Lerp(_carryDY, _carryWant, Math.Clamp(_dt / 0.045f, 0f, 1f));

            int steps = (int)((p.Y - _dragFromY) / D);
            if (steps != 0 && _dragRow < _stripKinds.Count)
            {
                string kind = _stripKinds[_dragRow];
                if (_stripOrder.Move(_stripKinds, kind, steps))
                {
                    _stripOrder.Save(StripOrderPath);
                    _dragRow = Math.Clamp(_dragRow + steps, 0, _stripKinds.Count - 1);
                    _dragFromY += steps * D;

                    _carryWant = (p.Y - _dragFromY) / S;
                    _carryDY -= steps * LayeredNotch.CircleD;
                }
            }
            return true;
        }

        bool wasTap = _dragHeld < DragHold
                      && Math.Abs(p.Y - _dragFromY) < D / 2 && Math.Abs(p.X - _dragFromX) < D / 2;
        int row = _dragRow;
        _dragRow = -1;
        _dragSess = -1;
        _dragHeld = 0f;
        _carryDY = 0f;
        _carryDX = 0f;
        if (wasTap && InMenu(p)) JumpToRow(p, row, D);
        return true;
    }

    private void JumpToRow(Win32.POINT p, int row, int D)
    {
        var rows = Groups();
        if (rows.Count == 0) return;
        row = Math.Clamp(row, 0, rows.Count - 1);
        int mx = _cl + Sc(CollapsedW + LayeredNotch.CircleGap + LayeredNotch.PrivacyPad);
        var grp = rows[row];
        int rel = (p.X - mx) / D;
        int pick = rel <= 0 || grp.Length == 1 ? 0 : Math.Clamp(rel - 1, 0, grp.Length - 1);
        _pending = grp[pick];
        _dropIcon = _widgets[_pending].Icon;
        _dropImage = _widgets[_pending].IconImage;
        int DL = LayeredNotch.CircleD;
        _dropCX = rel <= 0 ? DL / 2f : (rel + 0.5f) * DL;
        _dropCY = (row + 0.5f) * DL;
        _drop = 0f;
        _menu = 0f;
        _rowOpen = 0f;
        _row = -1;
    }

    private DoubleClick _appReveal;

    private void PollClick(Win32.POINT p)
    {

        bool overPill = _progress > 0.02f
            ? InRect(p, _el, _et, Sc(ExpandedW), Sc(ExpandedH))
            : InRect(p, _cl, _ct, Sc(CollapsedW), Sc(CollapsedH));
        bool inert = overPill && !_moving && !OverPressable(new Point(p.X, p.Y));
        if (_appReveal.Step((Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0 && inert,
                            Environment.TickCount64, p.X, p.Y, Win32.GetDoubleClickTime()))
            RevealPrimaryApp();

        bool down = (Win32.GetAsyncKeyState(Win32.VK_LBUTTON) & 0x8000) != 0;
        if (_moving) { _lastMouseDown = down; return; }
        if (UpdatePinGesture(p, down)) { _lastMouseDown = down; return; }
        if (UpdateStripGesture(p, down)) { _lastMouseDown = down; return; }

        if (_askSwipeY is { } swipeFrom && _ask is { } swiping)
        {
            if (!down) _askSwipeY = null;
            else if (swipeFrom - p.Y >= Sc(AskSwipeDist)) DismissAsk(swiping);
        }

        if (_notif == null && _ask == null && _panelT > 0.5f && _panels.Current is { } livePanelHit)
        {
            var local = PanelLocal(p);
            if (down && !_lastMouseDown && !_resizing)
            {
                if (InChip(p, Halo.Panels.PanelLayout.CloseRect(Halo.Panels.PanelLayout.Width)))
                {
                    _panels.Close(livePanelHit.Id);
                    _panelHover = -1;
                    _panelCloseHover = false;
                    _lastMouseDown = down;
                    return;
                }
                if (Halo.Panels.PanelHit.Press(livePanelHit.Spec, Halo.Panels.PanelLayout.Width, local) is { } press)
                {
                    _panels.Apply(press.Row, press.Value);
                    _panelHeld = press.Row;
                }
            }

            else if (down && _lastMouseDown && _panelHeld >= 0)
            {
                if (Halo.Panels.PanelHit.Press(livePanelHit.Spec, Halo.Panels.PanelLayout.Width, local,
                        dragging: true, heldRow: _panelHeld) is { } drag)
                    _panels.Apply(drag.Row, drag.Value);
            }
            if (!down) _panelHeld = -1;

            if (down || _lastMouseDown) { _lastMouseDown = down; return; }
        }

        if (down && !_lastMouseDown && !_resizing && _notif == null && _ask is { } ask && _askT > 0.5f)
        {

            if (InChip(p, AskBanner.CloseRect(AskBanner.W)))
            {
                DismissAsk(ask);
                _lastMouseDown = down;
                return;
            }

            if (p.Y >= _ct + Sc(_curH) - Sc(AskSwipeStrip)) _askSwipeY = p.Y;
            bool hitRow = false;
            for (int i = 0; i < _askChips.Count; i++)
                if (InChip(p, _askChips[i].Rect))
                {
                    hitRow = true;

                    if (AskBanner.IsFreeText(_askChips[i].Option)) BeginTyping();

                    else
                    {
                        var how = AskBanner.IsChat(_askChips[i].Option) ? AskDelivery.Chat
                                : AskBanner.IsSubmit(_askChips[i].Option) ? AskDelivery.Submit
                                : AskDelivery.Option;

                        bool stays = ask.MultiSelect && how == AskDelivery.Option;
                        if (_asks.Answer(ask, _askChips[i].Option.Label, how))
                        {
                            if (!stays)
                            {
                                EndTyping();
                                ClearDraft();
                                _askGhost = ask;
                                _ask = null;
                            }
                            _askHover = -1;
                        }
                    }
                    break;
                }

            if (!hitRow && _askTyped != null && !InRect(p, NotifLeft(), _ct, Sc(_curW), Sc(_curH)))
                EndTyping();
            _lastMouseDown = down;
            return;
        }
        if (down && !_lastMouseDown && !_resizing && _notif != null)
        {

            var copyR = NotifBanner.CopyRect(_notif, _curW);
            if (!InRect(p, NotifLeft(), _ct, Sc(_curW), Sc(_curH)))
                _notifClosing = true;

            else if (!copyR.IsEmpty
                && p.X >= NotifLeft() + copyR.X * S && p.X < NotifLeft() + copyR.Right * S
                && p.Y >= _ct + copyR.Y * S && p.Y < _ct + copyR.Bottom * S)
            {
                Halo.Interop.Clipboard.SetText(_notif.Code);
                _notif.Copied = true;
                _notifDeadline = Max(_notifDeadline, DateTime.UtcNow.AddSeconds(2));
            }

            else if (!_notifDetailOn && NotifBanner.BodyOverflows(_notif) && p.Y >= _ct + Sc(_curH - 22))
            {
                _notifDetailOn = true;
                _notifDeadline = DateTime.MaxValue;
            }
            else
            {
                _notif.Activate();
                _notifClosing = true;
            }
        }
        else if (down && !_lastMouseDown && !_resizing)
        {
            if (_progress > 0.9f)
            {

                foreach (var (r, onClick) in _widgets[_primary].Buttons(ExpandedW, ExpandedH))
                {
                    float bx = _el + r.X * S, by = _et + r.Y * S;
                    if (p.X >= bx && p.X < bx + r.Width * S && p.Y >= by && p.Y < by + r.Height * S)
                    {
                        onClick(new PointF((p.X - _el) / S, (p.Y - _et) / S));
                        break;
                    }
                }
            }
            else if (_progress < 0.1f && TryCollapsedButton(p)) { }

        }

        _lastMouseDown = down;
    }

    private void HandleTrayInteraction(Win32.POINT p, bool down)
    {
        if (!(_progress > 0.9f && _drop < 0f && !_moving && _notif == null && _widgets[_primary] is FileTray tray))
        {
            if (_trayMode == 1) FileTray.CancelReorder();
            _trayPressPath = null; _trayMode = -1; _lastTrayDown = down; return;
        }

        var local = new PointF((p.X - _el) / S, (p.Y - _et) / S);
        bool inside = InRect(p, _el, _et, Sc(ExpandedW), Sc(ExpandedH));
        bool ctrl = (Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) != 0;

        if (down && !_lastTrayDown)
        {
            _trayPressPath = tray.RowPathAt(ExpandedW, ExpandedH, local);
            _trayPressAt = p;
            _trayMode = 0;
            if (_trayPressPath != null && ctrl) { FileTray.ToggleSelect(_trayPressPath); _trayPressPath = null; _trayMode = -1; }
        }
        else if (down && _trayMode == 0 && _trayPressPath != null)
        {
            int dx = p.X - _trayPressAt.X, dy = p.Y - _trayPressAt.Y;
            if (!inside) StartTrayDragOut();
            else if (dx * dx + dy * dy > 36)
            {
                _trayMode = 1;
                FileTray.BeginReorder(_trayPressPath);
                FileTray.UpdateReorder(tray.RowIndexAt(ExpandedW, ExpandedH, local));
            }
        }
        else if (down && _trayMode == 1)
        {
            if (!inside) { FileTray.CancelReorder(); StartTrayDragOut(); }
            else FileTray.UpdateReorder(tray.RowIndexAt(ExpandedW, ExpandedH, local));
        }

        if (!down && _lastTrayDown)
        {
            if (_trayMode == 1) FileTray.CommitReorder();
            else if (_trayMode == 0 && _trayPressPath != null) { FileTray.ClearSelection(); FileTray.Open(_trayPressPath); }
            _trayPressPath = null; _trayMode = -1;
        }
        _lastTrayDown = down;
    }

    private void StartTrayDragOut()
    {
        var paths = _trayPressPath != null ? FileTray.SelectionOrRow(_trayPressPath) : Array.Empty<string>();
        _trayMode = 2;
        _trayPressPath = null;

        if (paths.Length > 0 && Halo.Interop.FileDrag.Out(paths) && !CursorOverNotch()) FileTray.RemovePaths(paths);
        _trayPressPath = null; _trayMode = -1;
    }

    private bool CursorOverNotch()
    {
        return Win32.GetCursorPos(out var p) && Win32.GetWindowRect(_notch.Hwnd, out var r)
            && p.X >= r.left && p.X < r.right && p.Y >= r.top && p.Y < r.bottom;
    }

    private static bool InRect(Win32.POINT p, int left, int top, int w, int h)
        => p.X >= left && p.X < left + w && p.Y >= top && p.Y < top + h;

    private bool OverPressable(Point p)
    {
        try
        {
            if (_empty || _primary < 0 || _primary >= _widgets.Length) return false;
            if (_progress > 0.9f)
            {
                if (Contains(PinRect(ExpandedW, ExpandedH), _el, _et, p)) return true;
                foreach (var (r, _) in _widgets[_primary].Buttons(ExpandedW, ExpandedH))
                    if (Contains(r, _el, _et, p)) return true;
                return false;
            }
            if (_progress < 0.1f)
                foreach (var (r, _) in _widgets[_primary].CollapsedButtons(CollapsedW, CollapsedH))
                    if (Contains(r, _cl, _ct, p)) return true;
            return false;
        }
        catch { return false; }
    }

    private bool Contains(RectangleF r, int left, int top, Point p)
    {
        float bx = left + r.X * S, by = top + r.Y * S;
        return p.X >= bx && p.X < bx + r.Width * S && p.Y >= by && p.Y < by + r.Height * S;
    }

    private bool TryCollapsedButton(Win32.POINT p)
    {
        if (_primary < 0 || _primary >= _widgets.Length || _empty) return false;
        try
        {
            foreach (var (r, onClick) in _widgets[_primary].CollapsedButtons(CollapsedW, CollapsedH))
            {
                float bx = _cl + r.X * S, by = _ct + r.Y * S;
                if (p.X >= bx && p.X < bx + r.Width * S && p.Y >= by && p.Y < by + r.Height * S)
                {
                    onClick(new PointF((p.X - _cl) / S, (p.Y - _ct) / S));
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    internal static Bitmap? MenuRowImage(IWidget[] widgets, int[] group)
    {
        if (group.Length == 0) return null;
        if (group.Length < 2) return widgets[group[0]].IconImage;
        return widgets[group[0]] switch
        {
            ClaudeCodeWidget => ClaudeCodeWidget.PlainIcon,
            CodexWidget => CodexWidget.PlainIcon,
            _ => widgets[group[0]].IconImage,
        };
    }

    internal static float MenuRowImageOffset(IWidget[] widgets, int[] group)
        => group.Length == 0 ? 0f : widgets[group[0]].IconOffsetX;

    private void Apply(float t)
    {
        float e = EaseOutBack(t);
        int w = (int)Lerp(CollapsedW, ExpandedW, e);
        int h = (int)Lerp(CollapsedH, ExpandedH, e);
        int r = (int)Lerp(CollapsedR, ExpandedR, e);
        if (_shrink > 0f)
        {
            float s = SmoothStep(_shrink);
            w = (int)Lerp(w, 96, s);
            h = (int)Lerp(h, 12, s);
            r = (int)Lerp(r, 6, s);
        }

        float faceIn = Halo.Widgets.FaceDirector.Alpha(_faceT);
        bool faceOnly = FaceOnly;

        bool floating = faceOnly || (_notifFloat && _notifT > 0f);
        if (faceOnly)
        {

            w = (int)Lerp(w, Halo.Widgets.Face.PlateW, faceIn);
            h = (int)Lerp(h, Halo.Widgets.Face.PlateH, faceIn);
            r = (int)Lerp(r, 0, faceIn);
        }
        bool glass = !_lastDesktop;

        int cT = TintFor(glass ? TintAppCollapsed : TintDeskCollapsed, GlassScale);
        int eT = TintFor(glass ? TintAppExpanded : TintDeskExpanded, GlassScale);
        int tint = (int)Lerp(cT, eT, t);

        if (_empty && !Privacy.Active)
            tint = (int)Lerp(tint, EmptyCatchAlpha, SmoothStep(_shrink));

        if (faceOnly) tint = (int)Lerp(tint, 0, faceIn);
        float fade = ContentFade(t);
        float mini = MiniFade(t);

        if (_notif == null && _ask == null && _panelGhost != null && _panelT > 0f)
        {
            float ep = EaseOutBack(_panelT);
            w = (int)Lerp(w, Halo.Panels.PanelLayout.Width, ep);
            h = (int)Lerp(h, _panelH, ep);
            r = (int)Lerp(r, 26, ep);
            tint = (int)Lerp(cT, glass ? TintAskApp : TintAskDesk, _panelT);
            fade = ContentFade(_panelT);
            mini *= MiniFade(_panelT);
        }
        if (_notif == null && (_ask ?? _askGhost) != null && _askT > 0f)
        {
            float ea = EaseOutBack(_askT);
            w = (int)Lerp(w, AskBanner.W, ea);
            h = (int)Lerp(h, _askH, ea);
            r = (int)Lerp(r, 26, ea);
            tint = (int)Lerp(cT, glass ? TintAskApp : TintAskDesk, _askT);
            fade = ContentFade(_askT);

            mini *= MiniFade(_askT);
        }
        if (_notif != null && _notifT > 0f)
        {
            float en = EaseOutBack(_notifT);
            float nh = Lerp(NotifBanner.SummaryH, _notifDetailH, SmoothStep(_notifDetail));
            w = (int)Lerp(w, NotifBanner.W, en);
            h = (int)Lerp(h, nh, en);
            r = (int)Lerp(r, 26, en);

            tint = _notifFloat ? 0 : (int)Lerp(cT, eT, _notifT);
            fade = ContentFade(_notifT);
            mini *= MiniFade(_notifT);
        }
        float arrive = _arrive < 0f ? 1f : 1f - (1f - _arrive) * (1f - _arrive);
        mini *= arrive;

        var groups = _empty ? new List<int[]>() : Groups();

        if (_rowShift.Length != groups.Count) _rowShift = new float[groups.Count];
        bool carrying = _dragHeld >= DragHold && _dragRow >= 0 && _dragRow < groups.Count;
        float at = carrying ? _dragRow + _carryDY / LayeredNotch.CircleD : 0f;
        for (int i = 0; i < _rowShift.Length; i++)
        {
            float target = 0f;
            if (carrying && i != _dragRow)
            {
                if (_dragRow < i && at >= i) target = -LayeredNotch.CircleD;
                else if (_dragRow > i && at <= i) target = LayeredNotch.CircleD;
            }
            _rowShift[i] = Lerp(_rowShift[i], target, Math.Clamp(_dt / 0.11f, 0f, 1f));
        }

        int openRow = _row >= 0 && _row < groups.Count ? _row : -1;
        int fanCount = openRow >= 0 ? groups[openRow].Length : 0;
        if (_sessShift.Length != fanCount) _sessShift = new float[fanCount];
        bool carryingSess = _dragHeld >= DragHold && _dragSess >= 0 && _dragRow == openRow
                            && _dragSess < fanCount;
        float atX = carryingSess ? _dragSess + _carryDX / LayeredNotch.CircleD : 0f;
        for (int j = 0; j < _sessShift.Length; j++)
        {
            float target = 0f;
            if (carryingSess && j != _dragSess)
            {
                if (_dragSess < j && atX >= j) target = -LayeredNotch.CircleD;
                else if (_dragSess > j && atX <= j) target = LayeredNotch.CircleD;
            }
            _sessShift[j] = Lerp(_sessShift[j], target, Math.Clamp(_dt / 0.11f, 0f, 1f));
        }

        var frame = new MenuFrame
        {

            CarryRow = _dragHeld >= DragHold && _dragSess < 0 ? _dragRow : -1,
            CarryDY = _carryDY,
            RowShift = _rowShift,
            CarrySess = carryingSess ? _dragSess : -1,
            CarryDX = _carryDX,
            SessShift = _sessShift,

            Show = _greet == GreetingKind.None && !floating
                   && (groups.Count >= 1 || _stripT > 0.01f),
            Appear = SmoothStep(_stripT),

            Swallow = Math.Min(1f - fade, 1f - Math.Clamp(t / StripSwallowOut, 0f, 1f)),

            RowIcons = groups.ConvertAll(gr => _widgets[gr[0]].Icon).ToArray(),
            RowImages = groups.ConvertAll(gr => MenuRowImage(_widgets, gr)).ToArray(),
            RowImageOffsets = groups.ConvertAll(gr => MenuRowImageOffset(_widgets, gr)).ToArray(),
            RowCounts = groups.ConvertAll(gr => gr.Length >= 2 ? gr.Length : 0).ToArray(),
            SessIcons = groups.ConvertAll(gr => gr.Length >= 2
                ? Array.ConvertAll(gr, i => _widgets[i].Icon) : Array.Empty<string>()).ToArray(),
            SessImages = groups.ConvertAll(gr => gr.Length >= 2
                ? Array.ConvertAll(gr, i => _widgets[i].IconImage) : Array.Empty<Bitmap?>()).ToArray(),
            RowRings = groups.ConvertAll(GroupRing).ToArray(),
            RowProgress = groups.ConvertAll(gr => _widgets[gr[0]].RingProgress).ToArray(),

            SessRings = groups.ConvertAll(gr => gr.Length >= 2
                ? gr.Select((i, j) => (Color?)(RingOf(i) is { } rc ? Fx.Shade(rc, j) : null)).ToArray()
                : Array.Empty<Color?>()).ToArray(),
            Open = EaseOutBack(Math.Clamp(_menu, 0f, 1f)),
            OpenRow = _row,
            RowOpen = EaseOutBack(Math.Clamp(_rowOpen, 0f, 1f)),
            Dropping = _drop >= 0f,
            DropIcon = _dropIcon,
            DropImage = _dropImage,
            Drop = _drop >= 0f ? _drop : 0f,
        };
        frame.Outward = _dropOut;
        if (frame.Dropping)
        {
            float circleX = w + LayeredNotch.CircleGap + LayeredNotch.PrivacyPad + _dropCX;
            float circleY = LayeredNotch.CircleY + _dropCY;
            float pillX = w - h / 2f, pillY = h / 2f;
            (frame.FromX, frame.FromY, frame.ToX, frame.ToY) = _dropOut
                ? (pillX, pillY, circleX, circleY)
                : (circleX, circleY, pillX, pillY);
        }

        if (_greet != GreetingKind.None)
        {
            var gf = GreetingPlan.Of(_greet, _greetT);
            w = (int)gf.PillW;
            h = (int)gf.PillH;
            r = (int)gf.Radius;
            fade = 1f;
            mini = 0f;

            _drop = -1f;
            _arrive = -1f;
            _stripT = 0f;
        }

        Action<Graphics, int, int, float> content = _greet != GreetingKind.None
            ? (g, cw, ch, f) => DrawGreeting(g, cw, ch)
            : _notif == null && (_ask ?? _askGhost) is { } q && _askT > 0f
            ? (g, cw, ch, f) => AskBanner.Draw(g, cw, ch, f, q, _askHover, tint, _askTyped, _askCloseHover,
                                               _asks.Ticked(q.Nonce), _asks.Sent(q.Nonce))
            : _notif is { } toast && _notifT > 0f
            ? (g, cw, ch, f) =>
            {

                if (_notifFloat)
                {
                    float drop = CatDrop;
                    var sheet = Halo.Widgets.Face.SheetRect(cw, ch);
                    using var outline0 = Halo.Widgets.Face.SheetPath(sheet, 26f);
                    float on = Math.Min(1f, _notifT * 2.6f);

                    if (drop > 0f)
                        Halo.Widgets.Face.Cling(g, sheet, CatLook(sheet), _catGrip,
                                                Math.Max(_catDuck, CatRecoil()), on, _catAnchor);

                    _notch.FrostInto(g, outline0, cw, ch, on * FrostMix, BannerClarity);
                    Halo.Widgets.Face.Glass(g, sheet, 26f, on);

                    var saved = g.Save();
                    g.SetClip(outline0, System.Drawing.Drawing2D.CombineMode.Intersect);

                    NotifBanner.Draw(g, cw, ch, f, toast, SmoothStep(_notifDetail), _notifDetailOn,
                                     SmoothStep(_notifFold), onGlass: true);
                    g.Restore(saved);

                    if (drop > 0f)
                    {
                        float pull = Math.Max(_catDuck, CatRecoil());
                        Halo.Widgets.Face.ClingShadow(g, sheet, _catGrip, pull, on, _catAnchor);
                        Halo.Widgets.Face.ClingPaws(g, sheet, _catGrip, pull, on, _catAnchor);
                    }
                }
                else
                    NotifBanner.Draw(g, cw, ch, f, toast, SmoothStep(_notifDetail), _notifDetailOn,
                                     SmoothStep(_notifFold));
            }
            : _notif == null && _ask == null && _panelGhost is { } sheet && _panelT > 0f
            ? (g, cw, _, f) => Halo.Panels.PanelPaint.Draw(g, cw, sheet.Spec, f, _panelHover, _panelCloseHover)

            : faceOnly
            ? (g, cw, ch, _) => DrawFace(g, cw, ch)
            : _empty ? static (_, _, _, _) => { } : _widgets[_primary].DrawContent;

        bool pin = _notif == null && _ask == null && _askGhost == null && _panelGhost == null
            && _greet == GreetingKind.None && !TrayFront && !faceOnly;

        _notch.SkipShape = floating;

        _notch.OffsetY = Halo.Widgets.Face.FloatTop *
              Math.Max(faceOnly ? faceIn : 0f, _notifFloat ? Math.Min(1f, _notifT * 3f) : 0f);

        float drop = CatDrop;
        float side = drop > 0f ? Halo.Widgets.Face.CatSide : 0f;
        if (drop > 0f) { h += (int)drop; w += (int)side; }
        _curW = w - (int)side;
        _curH = h - (int)drop;
        _notch.OffsetX = _offsetX;
        float holdCue = _moving ? 0f : _holdT;

        bool banner = _notif != null || ((_ask ?? _askGhost) != null && _askT > 0f);

        float glassFade = faceOnly ? 1f - faceIn
            : _empty && !Privacy.Active && !banner ? 1f - SmoothStep(_shrink) : 1f;

        float mgW = (int)side, mgH = (int)drop;
        _notch.Render(w, h, r, tint, fade, mini, glass, frame,
            (g, cw0, ch0, f) =>
            {
                var margin = g.Save();
                if (mgW > 0f || mgH > 0f) g.TranslateTransform(mgW / 2f, 0f);
                int cw = (int)(cw0 - mgW), ch = (int)(ch0 - mgH);
                content(g, cw, ch, f);
                if (holdCue > 0.01f) DrawHoldCue(g, cw, ch);

                long ms = Environment.TickCount64;
                float cue = _cue.Alpha(ms) * f;

                float pulse = 0.5f - 0.5f * MathF.Cos(ms % 2900 / 2900f * MathF.Tau);
                DrawToggleCue(g, cw, ch, r, cue, _cueCapture, _cueOn, pulse);
                DrawCueEdge(g, cw, ch, r, cue, _cueCapture, _cueOn, pulse);

                if (pin) DrawPin(g, cw, ch, f);
                g.Restore(margin);
            },

            _empty || faceOnly
                ? static (_, _, _, _) => { }
                : (g, cw0, ch0, f) =>
                {
                    var margin = g.Save();
                    if (mgW > 0f || mgH > 0f) g.TranslateTransform(mgW / 2f, 0f);
                    DrawCollapsedLayer(g, (int)(cw0 - mgW), (int)(ch0 - mgH), f);
                    g.Restore(margin);
                },
            glassFade, banner ? BannerClarity : 0f);
    }

    private string _faceDbg = "";

    private float _facePower = -1f;
    private bool _facePlugged;
    private long _facePowerAt;

    private void PollFacePower()
    {
        long now = Environment.TickCount64;
        if (now - _facePowerAt < 20_000) return;
        _facePowerAt = now;
        try
        {
            if (!Win32.GetSystemPowerStatus(out var s) || s.BatteryLifePercent > 100)
            { _facePower = -1f; return; }
            _facePower = s.BatteryLifePercent / 100f;
            _facePlugged = s.ACLineStatus == 1;
        }
        catch { _facePower = -1f; }
    }

    private float FaceBattery() { PollFacePower(); return _facePower; }
    private bool FaceCharging() => _facePlugged;

    private float CatDrop => _notifFloat && _notif != null ? Halo.Widgets.Face.CatDrop : 0f;

    private const float CatReadFrom = 0.70f, CatReadTo = 4.30f, CatGasp = 5.05f;

        internal static Halo.Widgets.Face.Look CatActAt(Halo.Widgets.Face.Look look, float k, int mood = 0)
        => (CatMood)mood switch
        {
            CatMood.Doze => CatDozing(look, k),
            CatMood.Bored => CatBored(look, k),
            CatMood.Thrilled => CatThrilled(look, k),
            _ => CatReading(look, k),
        };

    private static Halo.Widgets.Face.Look CatDozing(Halo.Widgets.Face.Look look, float k)
    {
        float sleepy = Smooth01(Math.Clamp((k - 1.10f) / 1.60f, 0f, 1f));

        float open = look.Open * (1f - 0.78f * sleepy);

        float peek = MathF.Max(0f, MathF.Sin((k - 5.4f) / 0.9f * MathF.PI));
        if (k > 5.4f && k < 6.3f) open = look.Open * (0.22f + 0.62f * peek);
        return look with
        {
            Open = open,
            GazeY = look.GazeY * (1f - sleepy) + 0.30f * sleepy,
            Glow = look.Glow * (1f - 0.22f * sleepy),
        };
    }

    private static Halo.Widgets.Face.Look CatBored(Halo.Widgets.Face.Look look, float k)
    {
        if (k < 2.4f) return CatReading(look, k);
        float over = Smooth01(Math.Clamp((k - 2.4f) / 0.7f, 0f, 1f));

        float blink = k > 2.5f && k < 3.1f ? MathF.Sin((k - 2.5f) / 0.6f * MathF.PI) : 0f;
        return look with
        {
            Open = look.Open * (1f - 0.42f * over) * (1f - 0.80f * blink),
            GazeX = look.GazeX * (1f - over) + 0.75f * over,
            GazeY = look.GazeY * (1f - over) - 0.20f * over,
        };
    }

    private static Halo.Widgets.Face.Look CatThrilled(Halo.Widgets.Face.Look look, float k)
    {
        float up = Smooth01(Math.Clamp(k / 0.35f, 0f, 1f));

        float dart = MathF.Sin(k / 0.62f * MathF.Tau);
        float pulse = 0.5f + 0.5f * MathF.Sin(k / 0.41f * MathF.Tau);
        return look with
        {
            Round = up * 0.85f,
            Open = look.Open * (1f + 0.38f * up),
            Glow = look.Glow * (1f + 0.30f * up * pulse),
            GazeX = look.GazeX * (1f - up) + up * 0.55f * dart,
            GazeY = look.GazeY * (1f - up) + up * (0.28f + 0.18f * dart),
        };
    }

    private static Halo.Widgets.Face.Look CatReading(Halo.Widgets.Face.Look look, float k)
    {
        if (k < CatReadFrom) return look;

        if (k < CatReadTo)
        {

            float span = (CatReadTo - CatReadFrom) / 3f;
            float line = (k - CatReadFrom) / span;
            float across = line - MathF.Floor(line);
            return look with
            {
                GazeX = -0.85f + 1.70f * Smooth01(Math.Min(1f, across / 0.88f)),
                GazeY = 0.20f + 0.22f * MathF.Floor(line),
                Open = look.Open * 0.86f,
            };
        }

        float since = k - CatReadTo;
        if (k < CatGasp + 1.5f)
        {

            float hit = Smooth01(Math.Clamp(since / 0.22f, 0f, 1f));
            float hold = 1f - Smooth01(Math.Clamp((k - CatGasp) / 0.95f, 0f, 1f));
            float gasp = hit * hold;
            return look with
            {
                Round = gasp,
                Open = look.Open * (1f + 0.42f * gasp),
                Glow = look.Glow * (1f + 0.55f * gasp),
                GazeX = look.GazeX * (1f - gasp),
                GazeY = look.GazeY * (1f - gasp) + 0.10f * gasp,
            };
        }

        float idle = k - (CatGasp + 1.5f);
        float peek = MathF.Max(0f, MathF.Sin(idle / 4.6f * MathF.Tau)) * MathF.Max(0f, MathF.Sin(idle / 1.9f));
        return look with { GazeX = -0.45f * peek, GazeY = 0.28f * peek };
    }

    private float CatRecoil() => CatRecoilAt(_catAge, (int)_catMood);

    internal static float CatRecoilAt(float k, int mood = 0)
    {
        switch ((CatMood)mood)
        {
            case CatMood.Doze:

                float sleepy = Smooth01(Math.Clamp((k - 1.10f) / 1.60f, 0f, 1f));
                return sleepy * (0.10f + 0.055f * MathF.Sin(k / 1.9f * MathF.Tau));
            case CatMood.Bored:

                return Smooth01(Math.Clamp((k - 3.4f) / 1.5f, 0f, 1f));
            case CatMood.Thrilled:

                return 0.09f * MathF.Max(0f, MathF.Sin(k / 0.31f * MathF.Tau));
            default:
                if (k < CatReadTo) return 0f;
                float hit = Smooth01(Math.Clamp((k - CatReadTo) / 0.18f, 0f, 1f));
                float back = 1f - Smooth01(Math.Clamp((k - CatGasp) / 0.90f, 0f, 1f));
                return hit * back * 0.34f;
        }
    }

    private static float Smooth01(float t)
    {
        float k = Math.Clamp(t, 0f, 1f);
        return k * k * (3f - 2f * k);
    }

    private Halo.Widgets.Face.Look CatLook(System.Drawing.RectangleF sheet)
    {

        var look = CatActAt(Halo.Widgets.FaceDirector.At(_faceAge), _catAge, (int)_catMood);
        if (!WidgetInput.Over) return look;
        var head = Halo.Widgets.Face.CatHead(sheet, _catGrip, _catDuck, _catAnchor);
        float cx = head.X + head.Width / 2f, cy = head.Y + head.Height * 0.5f;

        return look with
        {
            GazeX = Math.Clamp((WidgetInput.Mouse.X - cx) / 90f, -1f, 1f),
            GazeY = Math.Clamp((WidgetInput.Mouse.Y - cy) / 70f, -1f, 1f),

            Open = look.Open * (1f + 0.30f * (1f - _catDuck) * CatNear(sheet)),
        };
    }

        private float CatNear(System.Drawing.RectangleF sheet)
    {
        if (!WidgetInput.Over) return 0f;
        var head = Halo.Widgets.Face.CatHead(sheet, _catGrip, 0f, _catAnchor);
        float dx = WidgetInput.Mouse.X - (head.X + head.Width / 2f);
        float dy = WidgetInput.Mouse.Y - (head.Y + head.Height / 2f);
        return 1f - Math.Clamp(MathF.Sqrt(dx * dx + dy * dy) / 78f, 0f, 1f);
    }

    private void CatFrame(System.Drawing.RectangleF sheet)
    {
        float want = _notif != null && _notifFloat && !_notifClosing ? 1f : 0f;
        _catGrip += (want - _catGrip) * Math.Min(1f, _dt / 0.34f);

        if (!ReferenceEquals(_catFor, _notif)) { _catFor = _notif; _catAge = 0f; CatCast(_notif); }
        if (!_catShow) want = 0f;
        if (want > 0f) _catAge += _dt;
        float scared = CatNear(sheet) > 0.62f ? 1f : 0f;
        _catDuck += (scared - _catDuck) * Math.Min(1f, _dt / (scared > 0.5f ? 0.16f : 0.62f));
    }

    private float _groove;
    private float FaceLevel()
    {
        switch (_handProp)
        {
            case Halo.Widgets.FaceProp.Headphones:

                float peak = 0f;
                try { peak = (_launcherAudio ??= new Halo.Widgets.AudioMeter()).Peak(); } catch { }
                float want = Math.Clamp(MathF.Sqrt(Math.Max(0f, peak)) * 1.25f, 0f, 1f);
                _groove += (want - _groove) * 0.35f;
                return _groove;
            case Halo.Widgets.FaceProp.Download:

                _groove = 0f;
                foreach (var w in _widgets)
                    if (w is DownloadWidget dl)
                        try { return dl.RingProgress; } catch { return -1f; }
                return -1f;
            default:
                _groove = 0f;
                return 0f;
        }
    }

    private void FaceDebug(bool wakes)
    {
        if (Environment.GetEnvironmentVariable("HALO_FACEDEBUG") != "1") return;
        string line = $"wakes={wakes} empty={_empty} desk={_lastDesktop} want={FaceWanted} "
            + $"privacy={Privacy.Active} moving={_moving} notif={_notif != null} "
            + $"ask={_ask != null || _askGhost != null} panel={_panelGhost != null} greet={_greet} "
            + $"t={_faceT:0.00} shrink={_shrink:0.00} hand={_handT:0.00}/{_handProp} done={_handDone} "

            + $"solo={_handSolo} icon={(_handIcon is null ? "none" : $"{_handIcon.Width}x{_handIcon.Height}")}"
            + (_eatName.Length > 0 ? $" eat=[{_eatName}]" : "");
        if (line == _faceDbg) return;
        _faceDbg = line;
        try
        {
            Halo.Reports.DebugFile.Append(
                System.IO.Path.Combine(HaloDir, "face-debug.txt"),
                $"{DateTime.Now:HH:mm:ss.fff} {line}\r\n", 64 * 1024);
        }
        catch { }
    }

    private void DrawFace(Graphics g, int w, int h)
    {
        float alpha = Halo.Widgets.FaceDirector.Alpha(_faceT);
        var beat = new Halo.Widgets.FaceDirector.Beat(FaceLook(), 0f, 1f, 0f);
        if (_handT >= 0f)
        {
            beat = Halo.Widgets.FaceDirector.Hand(_handT, _handProp, _faceAge, FaceLevel());
            alpha *= beat.Alpha;
        }
        using (var outline = Halo.Widgets.Face.SheetPath(Halo.Widgets.Face.SheetRect(w, h), h / 2f))
            _notch.FrostInto(g, outline, w, h, alpha * FrostMix, BannerClarity);

        var box = Halo.Widgets.Face.BeatBox(w, h, beat.Bob, beat.Sway, beat.Scale, beat.Squash);

        Halo.Widgets.Face.FilmTint = null;
        foreach (var fw in _widgets)
            if (fw is MediaWidget fm && fm.IsActive) { Halo.Widgets.Face.FilmTint = fm.ArtAccent; break; }
        Halo.Widgets.Face.RingTone = Halo.Widgets.HaloMood.At(
            (float)DateTime.Now.TimeOfDay.TotalHours,
            FaceConditions());

        Halo.Widgets.Face.DrawGlass(g, w, h, alpha);

        Halo.Widgets.Face.Waves(g, box, beat.Phase, beat.Wave, alpha);
        Halo.Widgets.Face.Draw(g, box, beat.Look, alpha, beat.Liquid, beat.Chase, beat.Film);
        float prop = beat.Prop;

        if (prop > 0.001f || _handProp == Halo.Widgets.FaceProp.AppIcon)

            Halo.Widgets.Face.DrawProp(g, box, _handProp, prop, alpha, _handIcon,
                Math.Clamp(_handT / Halo.Widgets.FaceDirector.HandSeconds(_handProp), 0f, 1f));

        Halo.Widgets.Face.Letterbox(g, w, h, beat.Letterbox, alpha);
    }

    private Halo.Widgets.HaloMood.Conditions FaceConditions()
    {
        var doing = Halo.Widgets.HaloMood.Doing.Nothing;
        System.Drawing.Color? accent = null;
        foreach (var w in _widgets)
        {
            if (w is MediaWidget m && m.IsActive && m.Playing)
            {

                doing = m.ShowingVideo
                    ? Halo.Widgets.HaloMood.Doing.Video
                    : Halo.Widgets.HaloMood.Doing.Music;
                accent = m.ArtAccent;
                if (doing == Halo.Widgets.HaloMood.Doing.Video) break;
            }
            else if (doing == Halo.Widgets.HaloMood.Doing.Nothing && w is DownloadWidget { IsActive: true })
                doing = Halo.Widgets.HaloMood.Doing.Downloading;
        }
        return new Halo.Widgets.HaloMood.Conditions(
            FaceBattery(), FaceCharging(),
            Halo.Widgets.Privacy.Mic, Halo.Widgets.Privacy.Cam,

            ClaudeCode.NetMon.NetDown, doing, accent);
    }

    private void DrawCollapsedLayer(Graphics g, int w, int h, float fade)
    {
        Fx.AmbientScale = CollapsedAmbient(h);

        try { _widgets[_primary].DrawCollapsed(g, w, h, fade); }
        finally { Fx.AmbientScale = 1f; }
    }

    internal const int AmbientOutH = CollapsedH * 5 / 2;
    internal static float CollapsedAmbient(int h)
        => Math.Clamp((AmbientOutH - h) / (float)(AmbientOutH - CollapsedH), 0f, 1f);

    private void DrawGreeting(Graphics g, int w, int h)
    {
        var f = GreetingPlan.Of(_greet, _greetT);
        var box = Greeting.InkBox(w, h);

        float pen = f.PillW > GreetingPlan.CollapsedW + 1f ? 9f : 11f;
        Greeting.DrawHello(g, box, f.Written, f.HelloAlpha, Color.White, pen);
        if (f.LineAlpha > 0.004f)
            Greeting.DrawLine(g, Greeting.Lines[f.LineIndex], box, f.LineWritten, f.LineAlpha,
                              Color.White, pen);
    }

    private void DismissAsk(PendingAsk ask)
    {
        EndTyping();
        _askDismissed = ask.Nonce;
        _askGhost = ask;
        _ask = null;
        _askHover = -1;
        _askCloseHover = false;
        _askSwipeY = null;
    }

    private void BeginTyping()
    {
        if (_askTyped != null) return;

        _askTyped = _askDraftNonce == _ask?.Nonce ? _askDraft
                  : _ask != null ? _asks.Sent(_ask.Nonce) ?? "" : "";
        _keys.Start();
    }

    private void EndTyping()
    {
        if (_askTyped == null && !_keys.Active) return;
        if (_askTyped != null) { _askDraft = _askTyped; _askDraftNonce = _ask?.Nonce; }
        _askTyped = null;
        _keys.Stop();
    }

    private void ClearDraft()
    {
        _askDraft = "";
        _askDraftNonce = null;
    }

    private void TypedChar(char c)
    {
        if (_askTyped == null) return;
        if (c < ' ' || c == 0x7F) return;
        if (_askTyped.Length >= 400) return;
        _askTyped += c;
    }

    private void TypedKey(int vk)
    {
        if (_askTyped == null) return;
        if (vk == Win32.VK_BACK)
        {
            if (_askTyped.Length > 0) _askTyped = _askTyped[..^1];
        }
        else if (vk == Win32.VK_ESCAPE)
        {

            bool retracted = _ask is { } esc && _asks.Sent(esc.Nonce) is { Length: > 0 }
                          && _asks.Answer(esc, "", AskDelivery.FreeText);
            EndTyping();
            if (retracted) ClearDraft();
        }
        else if (vk == Win32.VK_RETURN)
        {
            string answer = _askTyped.Trim();

            if (answer.Length > 0 && _ask is { } ask)
            {

                _asks.Answer(ask, answer, AskDelivery.FreeText);

                if (!ask.MultiSelect) { _askGhost = ask; _ask = null; }
                _askHover = -1;
                EndTyping();
                ClearDraft();
                return;
            }
            EndTyping();
        }
        else if (vk == Win32.VK_V)
        {

            try
            {
                if (Clipboard.Text() is { Length: > 0 } t)
                    _askTyped = (_askTyped + t.Replace('\r', ' ').Replace('\n', ' ')).Trim();
            }
            catch { }
        }
    }

    private void RevealPrimaryApp()
    {
        try
        {
            if (_empty || _primary < 0 || _primary >= _widgets.Length) return;
            var widget = _widgets[_primary];

            var hwnd = AppFront.VerifiedHwnd(widget.RevealHwnd, widget.RevealPid);
            if (hwnd == IntPtr.Zero) hwnd = AppFront.TopLevelForPid(widget.RevealPid, widget.RevealHint);

            if (hwnd == IntPtr.Zero) hwnd = AppFront.TopLevelFor(widget.OwnerPids, widget.RevealHint);
            if (hwnd == IntPtr.Zero) hwnd = AncestorWindow(widget);
            if (hwnd == IntPtr.Zero && widget is MediaWidget media)
                hwnd = AppFront.TopLevelForProcess(media.App);
            if (hwnd == IntPtr.Zero && widget.RevealProcess is { Length: > 0 } app)
                hwnd = AppFront.TopLevelForProcess(app);
            if (hwnd != IntPtr.Zero) AppFront.Front(hwnd);
        }
        catch { }
    }

    private IntPtr AncestorWindow(IWidget widget)
    {
        var map = ParentMap();
        foreach (var owner in widget.OwnerPids)
        {
            int pid = owner, guard = 0;
            while (pid > 4 && guard++ < 32)
            {
                if (!map.TryGetValue(pid, out pid)) break;
                var hwnd = AppFront.TopLevelForPid(pid, widget.RevealHint);
                if (hwnd != IntPtr.Zero) return hwnd;
            }
        }
        return IntPtr.Zero;
    }

    private void FollowForeground(IntPtr fg)
    {
        try
        {
            Win32.GetWindowThreadProcessId(fg, out uint pid);
            if (pid == 0) return;
            for (int i = 0; i < _widgets.Length; i++)
            {
                if (i == _primary || !Live(i)) continue;
                foreach (var owner in _widgets[i].OwnerPids)
                    if (owner == (int)pid)
                    {
                        _primary = i;
                        _agentNotices.SetPrimary(i);
                        return;
                    }
            }
        }
        catch { }
    }

    private void FollowForegroundMedia(string fgProc)
    {
        if (string.IsNullOrEmpty(fgProc)) return;
        if (_widgets[_primary] is not MediaWidget pm || !pm.IsActive || !AppMatches(pm.App, fgProc)) return;
        for (int i = 0; i < _widgets.Length; i++)
            if (i != _primary && _widgets[i] is MediaWidget m && m.IsActive)
            { _primary = i; _agentNotices.SetPrimary(i); return; }
    }

    private static bool AppMatches(string app, string proc)
    {
        proc = proc.ToLowerInvariant();
        return app.Length > 1 && proc.Length > 1 && (app == proc || app.Contains(proc) || proc.Contains(app));
    }

    private Dictionary<int, int> _parentMap = new();
    private long _parentMapAt;
    private Dictionary<int, int> ParentMap()
    {
        long now = Environment.TickCount64;
        if (_parentMap.Count > 0 && now - _parentMapAt < 2000) return _parentMap;
        var snap = Win32.CreateToolhelp32Snapshot(Win32.TH32CS_SNAPPROCESS, 0);
        if (snap == new IntPtr(-1)) return _parentMap;
        try
        {
            var map = new Dictionary<int, int>(512);
            var pe = new Win32.PROCESSENTRY32W
            { dwSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32.PROCESSENTRY32W>() };
            if (Win32.Process32FirstW(snap, ref pe))
                do { map[(int)pe.th32ProcessID] = (int)pe.th32ParentProcessID; }
                while (Win32.Process32NextW(snap, ref pe));
            if (map.Count > 0) { _parentMap = map; _parentMapAt = now; }
        }
        finally { Win32.CloseHandle(snap); }
        return _parentMap;
    }

    private bool FgHostsWidget(int fgPid, int widget)
    {
        if (fgPid <= 4) return false;
        var map = ParentMap();
        foreach (var owner in _widgets[widget].OwnerPids)
        {
            int p = owner, guard = 0;
            while (p > 4 && guard++ < 32)
            {
                if (p == fgPid) return true;
                if (!map.TryGetValue(p, out p)) break;
            }
        }
        return false;
    }

    private static RectangleF PinRect(int w, int h) => new(9, 4, 24, 24);

    private bool OverPin(Win32.POINT p)
    {
        var r = PinRect(ExpandedW, ExpandedH);
        return p.X >= _el + r.X * S && p.X < _el + (r.X + r.Width) * S
            && p.Y >= _et + r.Y * S && p.Y < _et + (r.Y + r.Height) * S;
    }

    private DateTime _pinPressAt = DateTime.MaxValue;
    private bool _pinHoldFired;
    private const double PinHoldSeconds = 0.55;

    private bool UpdatePinGesture(Win32.POINT p, bool down)
    {

        bool over = _progress > 0.9f && _notif == null && !TrayFront && OverPin(p);
        if (down && !_lastMouseDown)
        {
            if (!over) return false;
            _pinPressAt = DateTime.UtcNow;
            _pinHoldFired = false;
            return true;
        }
        if (_pinPressAt == DateTime.MaxValue) return false;

        if (down)
        {
            if (!_pinHoldFired && (DateTime.UtcNow - _pinPressAt).TotalSeconds >= PinHoldSeconds)
            {
                _pinHoldFired = true;
                _recordable = !_recordable;
                SaveRecordable();
                _notch.SetCapturable(_recordable);
                FireToggleCue(capture: true, on: _recordable);
            }
            return true;
        }

        if (!_pinHoldFired && over) { _pinned = !_pinned; SavePin(); FireToggleCue(capture: false, on: _pinned); }
        _pinPressAt = DateTime.MaxValue;
        return true;
    }

    private ToggleCue _cue;
    private bool _cueCapture, _cueOn;

    private void FireToggleCue(bool capture, bool on)
    {
        _cue = new ToggleCue(Environment.TickCount64);
        _cueCapture = capture;
        _cueOn = on;
    }

    private const string GlyphPin = "\uE718", GlyphPinOff = "\uE77A";
    private const string GlyphEye = "\uE890", GlyphEyeOff = "\uED1A";

    private static (string Glyph, string Title, string Body, Color Accent) CueText(bool capture, bool on) => capture
        ? on
            ? (GlyphEye, "Visible in captures",
               "Screenshots, screen recordings and shared screens will now include the pill, so whatever it "
               + "happens to be showing goes out with them. Turn this off by holding the pushpin again.",
               Color.FromArgb(255, 240, 196, 120))
            : (GlyphEyeOff, "Hidden from captures",
               "The pill is left out of screenshots, recordings and screen shares. It stays on your own "
               + "screen exactly as it is - it simply will not appear in the picture, so notifications and "
               + "session panels do not end up in anything you send.",
               Color.FromArgb(255, 158, 168, 184))
        : on

            ? (GlyphPin, "Pinned over fullscreen",
               "Everything keeps its place at the top: what is playing, its controls, and any live session "
               + "stay one glance away while a game or a video owns the whole display.",

               Color.FromArgb(255, 92, 226, 198))
            : (GlyphPinOff, "Unpinned from fullscreen",
               "Fullscreen stays uncovered - the widgets step aside so nothing sits over a game or a film. "
               + "Notifications still reach you as banners, and the pill comes back the moment you leave.",
               Color.FromArgb(255, 158, 168, 184));

    internal static void DrawToggleCue(Graphics g, int w, int h, float radius, float alpha, bool capture,
                                       bool on, float pulse)
    {
        if (alpha <= 0.01f) return;
        var (glyph, title, body, accent) = CueText(capture, on);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        int A(int v) => (int)Math.Clamp(v * alpha, 0, 255);

        var st = g.Save();
        using (var shape = Fx.PillPath(w, h, radius))
        {

            g.SetClip(shape);

            using (var frost = new SolidBrush(Color.FromArgb(A(250), 9, 10, 13)))
                g.FillRectangle(frost, 0, 0, w, h);

            using (var sheen = new System.Drawing.Drawing2D.LinearGradientBrush(
                       new RectangleF(0, 0, w, h), Color.White, Color.White, 90f))
            {
                sheen.InterpolationColors = new System.Drawing.Drawing2D.ColorBlend(3)
                {
                    Colors = new[] { Color.FromArgb(A(26), 255, 255, 255), Color.FromArgb(A(8), 255, 255, 255),
                                     Color.FromArgb(0, 255, 255, 255) },
                    Positions = new[] { 0f, 0.55f, 1f },
                };
                g.FillRectangle(sheen, 0, 0, w, h);
            }

            float lift = (1f - alpha) * 10f;
            float pad = Math.Max(18f, w * 0.055f);
            float top = Math.Max(16f, h * 0.20f) + lift;

            const int headA = 255;

            float gs = Math.Max(17f, h * 0.105f);
            using var gfont = new Font("Segoe Fluent Icons", gs, FontStyle.Regular, GraphicsUnit.Pixel);
            using var tf = new Font("Segoe UI", Math.Max(15f, h * 0.098f), FontStyle.Bold, GraphicsUnit.Pixel);
            using var bf = new Font("Segoe UI", Math.Max(11f, h * 0.063f), FontStyle.Regular, GraphicsUnit.Pixel);
            using var gfmt = new StringFormat(StringFormat.GenericTypographic);
            using var titleFmt = new StringFormat(StringFormat.GenericTypographic)
            { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

            using var bodyFmt = new StringFormat(StringFormat.GenericTypographic)
            { Trimming = StringTrimming.Word };

            var gsz = g.MeasureString(glyph, gfont, int.MaxValue, gfmt);
            var tsz = g.MeasureString(title, tf, int.MaxValue, titleFmt);
            float rowH = Math.Max(gsz.Height, tsz.Height);
            float gapX = Math.Max(10f, gs * 0.55f);

            var textHint = g.TextRenderingHint;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using (var gbrush = new SolidBrush(Color.FromArgb(A(headA), accent.R, accent.G, accent.B)))
                Fx.Text(g, glyph, gfont, gbrush, new PointF(pad, top + (rowH - gsz.Height) / 2f), gfmt);
            g.TextRenderingHint = textHint;
            using (var tb = new SolidBrush(Color.FromArgb(A(headA), accent.R, accent.G, accent.B)))
                Fx.Text(g, title, tf, tb, new RectangleF(pad + gsz.Width + gapX, top + (rowH - tsz.Height) / 2f,
                                                          w - pad * 2 - gsz.Width - gapX, rowH), titleFmt);

            using (var bb = new SolidBrush(Color.FromArgb(A(190), 226, 231, 240)))
                Fx.Text(g, body, bf, bb, new RectangleF(pad, top + rowH + rowH * 0.62f,
                                                         w - pad * 2, h - (top + rowH * 1.62f) - pad * 0.6f), bodyFmt);
        }
        g.Restore(st);
    }

    private static Color Vivid(Color c)
    {
        int m = Math.Max(c.R, Math.Max(c.G, c.B));
        int Deepen(int v) => (int)Math.Clamp(m - (m - v) * 1.45f, 0, 255);
        return Color.FromArgb(c.A, Deepen(c.R), Deepen(c.G), Deepen(c.B));
    }

    internal static void DrawCueEdge(Graphics g, int w, int h, float radius, float alpha, bool capture,
                                     bool on, float pulse)
    {
        if (alpha <= 0.01f) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        const float width = 3.4f;

        float lit = 0.30f + 0.70f * Math.Clamp(pulse, 0f, 1f);
        int a = (int)Math.Clamp(255 * alpha, 0, 255);

        Color Mix(Color c) => Color.FromArgb(a, (int)(c.R * lit), (int)(c.G * lit), (int)(c.B * lit));
        var ribbon = new[]
        {
            Mix(Vivid(Color.FromArgb(255, 255, 105, 120))),
            Mix(Vivid(Color.FromArgb(255, 255, 170, 90))),
            Mix(Vivid(Color.FromArgb(255, 250, 225, 110))),
            Mix(Vivid(Color.FromArgb(255, 120, 235, 150))),
            Mix(Vivid(Color.FromArgb(255, 90, 220, 240))),
            Mix(Vivid(Color.FromArgb(255, 120, 160, 255))),
            Mix(Vivid(Color.FromArgb(255, 190, 130, 255))),
            Mix(Vivid(Color.FromArgb(255, 255, 105, 120))),
        };

        using var path = Fx.PillPath(w, h, radius, width / 2f);

        using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            new RectangleF(-1f, -1f, w + 2f, h + 2f), Color.White, Color.White, 20f);
        var positions = new float[ribbon.Length];
        for (int i = 0; i < ribbon.Length; i++) positions[i] = i / (float)(ribbon.Length - 1);
        brush.InterpolationColors = new System.Drawing.Drawing2D.ColorBlend(ribbon.Length)
        { Colors = ribbon, Positions = positions };

        using var pen = new Pen(brush, width);
        g.DrawPath(pen, path);
    }

    private float PinHoldProgress()
        => _pinPressAt == DateTime.MaxValue || _pinHoldFired ? 0f
         : Math.Clamp((float)((DateTime.UtcNow - _pinPressAt).TotalSeconds / PinHoldSeconds), 0f, 1f);

    private void DrawPin(Graphics g, int w, int h, float a)
    {
        if (a <= 0.01f) return;
        var r = PinRect(w, h);
        bool hov = WidgetInput.Over && r.Contains(WidgetInput.Mouse);
        _pinHov = Toward(_pinHov, hov ? 1f : 0f, _dt / 0.10f);
        float hv = _pinHov * _pinHov * (3f - 2f * _pinHov);

        DrawPushpin(g, r, _pinned, hv, a, _recordable, PinHoldProgress());
    }

    private static void Sphere(Graphics g, RectangleF head, float hr, GraphicsPath? needle, float a,
        Color baseColor, Color? needleColor = null)
    {
        int A(float f) => (int)Math.Clamp(f * a, 0, 255);
        Color Tint(Color c, float k) => Color.FromArgb(A(255),
            (int)Math.Clamp(c.R * k, 0, 255),
            (int)Math.Clamp(c.G * k, 0, 255),
            (int)Math.Clamp(c.B * k, 0, 255));
        Color Shade(float k) => Tint(baseColor, k);

        if (needle != null)
        {
            var nc = needleColor ?? baseColor;
            using var nb = new LinearGradientBrush(
                new PointF(-3f * hr, 0), new PointF(3f * hr, 0), Tint(nc, 1.16f), Tint(nc, 0.52f));
            g.FillPath(nb, needle);
        }

        using (var shadow = new GraphicsPath())
        {
            shadow.AddEllipse(head.X + hr * 0.16f, head.Y + hr * 0.42f, head.Width * 0.92f, head.Height * 0.92f);
            using var sb = new PathGradientBrush(shadow)
            {
                CenterColor = Color.FromArgb(A(96), 0, 0, 0),
                SurroundColors = [Color.FromArgb(0, 0, 0, 0)],
            };
            g.FillPath(sb, shadow);
        }

        using (var hp = new GraphicsPath())
        {
            hp.AddEllipse(head);
            using var pgb = new PathGradientBrush(hp)
            {

                CenterPoint = new PointF(head.X + hr * 0.60f, head.Y + hr * 0.58f),
                CenterColor = Shade(1.34f),
                SurroundColors = [Shade(0.55f)],
            };
            g.FillPath(pgb, hp);
        }

        using (var spec = new GraphicsPath())
        {
            spec.AddEllipse(head.X + hr * 0.34f, head.Y + hr * 0.30f, hr * 0.62f, hr * 0.62f);
            using var sb = new PathGradientBrush(spec)
            {
                CenterColor = Color.FromArgb(A(215), 255, 255, 255),
                SurroundColors = [Color.FromArgb(0, 255, 255, 255)],
            };
            g.FillPath(sb, spec);
        }

        using (var rim = new Pen(Color.FromArgb(A(70), 255, 255, 255), Math.Max(0.7f, hr * 0.11f)))
            g.DrawArc(rim, head.X + 0.6f, head.Y + 0.6f, head.Width - 1.2f, head.Height - 1.2f, 20f, 130f);
    }

    internal static void DrawPushpin(Graphics g, RectangleF r, bool pinned, float hover, float a,
        bool recordable = false, float holdT = 0f)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var st = g.Save();
        float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, u = r.Width / 24f * 0.7f;
        g.TranslateTransform(cx, cy);
        g.RotateTransform(28f);

        float hr = 6.4f * u;
        var head = new RectangleF(-hr, -3f * u - hr, hr * 2, hr * 2);

        float grow = 1f + 0.18f * holdT;
        if (grow > 1.001f)
        {
            float gh = hr * grow;
            head = new RectangleF(-gh, -3f * u - gh, gh * 2, gh * 2);
            hr = gh;
        }

        if (recordable)
        {

            var amber = Color.FromArgb(255, 255, 200, 92);
            Sphere(g, head, hr, null, a, pinned ? amber : amber);
        }
        else if (pinned)
        {

            using (var fill = new SolidBrush(Color.FromArgb((int)((15 + 19 * hover) * a), 255, 255, 255)))
                g.FillEllipse(fill, head);
            using (var pen = new Pen(Color.FromArgb((int)((34 + 30 * hover) * a), 255, 255, 255), 1.7f * u))
                g.DrawEllipse(pen, head);
        }
        else
        {
            int dim = (int)((122 + 78 * hover) * a);
            using var pen = new Pen(Color.FromArgb(dim, 255, 255, 255), 1.7f * u)
            { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawEllipse(pen, head.X, head.Y, hr * 2, hr * 2);
        }
        g.Restore(st);
    }

    private static float Toward(float v, float t, float step)
        => v < t ? Math.Min(t, v + step) : Math.Max(t, v - step);

    private void DetectAgentCancel(IntPtr fg)
    {
        if ((Win32.GetAsyncKeyState(Win32.VK_ESCAPE) & 0x8000) == 0) return;
        if (!ForegroundIsAgentHost(fg)) return;
        if (_claudeStore.Current?.State == "compacting")
            ClaudeCodeWidget.MarkCompactCancelled(_claudeStore.Current?.StartedAt);
        if (_codexStore.Current?.State == "compacting")
            CodexWidget.MarkCompactCancelled(_codexStore.Current?.StartedAt);

        if (_claudeStore.Current?.State == "working")
            ClaudeCodeWidget.MarkTurnCancelled(_claudeStore.Current?.StartedAt);
        if (_codexStore.Current?.State == "working")
            CodexWidget.MarkTurnCancelled(_codexStore.Current?.StartedAt);
    }

    private static string ProcessNameOf(IntPtr hwnd)
    {
        try
        {
            Win32.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return "";
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch { return ""; }
    }

    private static bool ForegroundIsAgentHost(IntPtr fg)
    {
        try
        {
            Win32.GetWindowThreadProcessId(fg, out uint pid);
            if (pid == 0) return false;
            using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
            var name = proc.ProcessName.ToLowerInvariant();
            return name is "windowsterminal" or "wt" or "conhost" or "openconsole" or "powershell"
                or "pwsh" or "cmd" or "bash" or "wsl" or "alacritty" or "wezterm-gui" or "code"
                or "chatgpt" or "codex" || name.Contains("claude");
        }
        catch
        {
            return false;
        }
    }

    private void CancelClaude(int slot)
    {
        var st = _claudeStore.SessionLive(slot);
        var pid = st?.Pid ?? 0;
        if (pid <= 0) return;
        CcCancel.Request(pid);

        ClaudeCodeWidget.MarkTurnCancelled(st?.StartedAt);
    }

    private void CancelCodex(CodexSurface surface)
    {
        var snapshot = _codexStore.Candidate(surface);
        if (snapshot is { Source: CodexSurface.Cli, State: "working", ConsolePid: > 0 })
            CcCancel.Request(snapshot.ConsolePid);
        else if (snapshot is { Source: CodexSurface.Desktop, State: "working" })
            _codexDesktopRuntime.TryCancel();
        else return;

        CodexWidget.MarkTurnCancelled(snapshot.StartedAt);
    }

    private void OnClipboardImage(Bitmap shot, bool isScreenshot)
    {
        if (!Alert("clipboard")) return;
        string path = "";
        try
        {
            path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"halo-shot-{DateTime.Now:yyyyMMdd-HHmmss-fff}.png");
            shot.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        catch { path = ""; }
        _notifSrc.EnqueueLocal(new Halo.Notifications.NotifItem
        {
            App = isScreenshot ? Halo.Notifications.NotifItem.ScreenshotApp : Halo.Notifications.NotifItem.ClipboardApp,
            Title = isScreenshot ? Halo.Notifications.NotifItem.ScreenshotTitle : Halo.Notifications.NotifItem.ImageCopiedTitle,
            Preview = shot,
            LaunchPath = path,

            Icon = isScreenshot ? Badges.Shot() : Badges.Clip(),
        });
    }

    private void DetectLanguageChange(IntPtr fg)
    {
        try
        {
            uint tid = Win32.GetWindowThreadProcessId(fg, out _);
            if (tid == 0) return;
            uint lang = (uint)(Win32.GetKeyboardLayout(tid).ToInt64() & 0xFFFF);
            if (lang == 0) return;
            long now = Environment.TickCount64;
            if (fg != _langFg)
            {
                _langFg = fg; _lastLangId = lang; _langFgSince = now;
                return;
            }

            if (_lastLangId != 0 && lang != _lastLangId && now - _langFgSince > 600 && Alert("language"))
                ShowLanguageNotif(lang);
            _lastLangId = lang;
        }
        catch { }
    }

    private void ShowLanguageNotif(uint langId)
    {
        string name = "Keyboard", code = "?";
        try
        {
            var ci = new System.Globalization.CultureInfo((int)langId);
            var lang = ci.Parent.EnglishName.Length > 0 ? ci.Parent.EnglishName : ci.EnglishName;
            if (lang.Length > 0) name = lang;
            code = ci.TwoLetterISOLanguageName.ToUpperInvariant();
        }
        catch { }
        var item = new Halo.Notifications.NotifItem
        {
            App = "Keyboard", Title = name, Icon = Badges.Language(code),
            Kind = "language", Duration = 1,
        };

        if (_notif is { Kind: "language" } && !_notifClosing)
        {
            _notif.Icon?.Dispose();
            _notif = item;
            _notifDeadline = DateTime.UtcNow.AddSeconds(1);
            return;
        }
        _notifSrc.DropPending("language");
        _notifSrc.EnqueueLocal(item);
    }

    private static Halo.Notifications.NotifItem Sample((string App, string Title, string Body) n, Bitmap icon)
        => new() { App = n.App, Title = n.Title, Body = n.Body, Icon = icon };

    internal static Halo.Notifications.NotifItem HookBanner((string App, string Title, string Body) n, bool ok)
        => new()
        {
            App = n.App, Title = n.Title, Body = n.Body,
            Icon = ok ? Badges.Hooked() : Badges.HookFailed(),
            Kind = "hooks", Duration = 8,
        };

    internal static Halo.Notifications.NotifItem[] SampleLocalNotices(Bitmap shot) => new[]
    {
        new Halo.Notifications.NotifItem
        {
            App = Halo.Notifications.NotifItem.ScreenshotApp,
            Title = Halo.Notifications.NotifItem.ScreenshotTitle,
            Preview = shot, Icon = Badges.Shot(),
        },

        HookBanner(Halo.ClaudeCode.HookConnect.Notice("Claude Code",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", "settings.json")), ok: true),
        HookBanner(Halo.ClaudeCode.HookConnect.Notice("Codex",
            System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex", "hooks.json")), ok: true),
        HookBanner(Halo.ClaudeCode.HookConnect.Failed("Claude Code", "access denied"), ok: false),
        new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.app.network"),
            Title = Halo.Localization.Strings.Get("notice.net.slow.title"), Icon = Badges.NetSlow(),
        },
        new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.app.network"),
            Title = Halo.Localization.Strings.Get("notice.net.down.title"),
            Body = Halo.Localization.Strings.Get("notice.net.down.body"),
            Icon = Badges.NetDown(),
        },
        new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.app.claude"),
            Title = Halo.Localization.Strings.Get("notice.api.down.title"),
            Body = Halo.Localization.Strings.Get("notice.api.down.body"),
            Icon = Badges.ApiDown(),
        },
        new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.app.system"),
            Title = Halo.Localization.Strings.Format("notice.load.title", Halo.Localization.Strings.Get("notice.load.cpu"), 92),
            Body = Halo.Localization.Strings.Format("notice.load.body", "chrome.exe"),
            Icon = Badges.Cpu(),
        },
        new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.app.system"),
            Title = Halo.Localization.Strings.Format("notice.load.title", Halo.Localization.Strings.Get("notice.load.memory"), 88),
            Body = Halo.Localization.Strings.Format("notice.load.body", "Chrome"),
            Icon = Badges.Memory(),
        },
        new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.app.battery"),
            Title = Halo.Localization.Strings.Format("notice.battery.title", Halo.Localization.Strings.Get("notice.battery.critical"), 7),
            Body = Halo.Localization.Strings.Get("notice.battery.body"),
            Icon = Badges.BatteryDead(),
        },
        new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.app.claude"),
            Title = Halo.Localization.Strings.Format("notice.context.title", 85),
            Body = Halo.Localization.Strings.Get("notice.context.body"), Icon = Badges.Context(),
        },
        new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.app.claude"),
            Title = Halo.Localization.Strings.Format("limit.title", Halo.Localization.Strings.Get("notice.app.claude"), 85),
            Body = Halo.Localization.Strings.Format("limit.body", 85, Halo.Localization.Strings.Get("limit.window.weekly")),
            Icon = Badges.LimitLong(),
        },

        new Halo.Notifications.NotifItem
        {
            App = "Tehran",
            Title = Almanac.Headline(DateTime.Today.AddHours(1), new Almanac.Weather(27, 0, Day: false), metric: true),
            Body = Almanac.Detail(DateTime.Today.AddHours(1), CalendarKind.SolarHijri),
            Icon = Badges.Local(0xE708, 232, 32f),
        },

        new Halo.Notifications.NotifItem
        {
            App = Halo.Localization.Strings.Get("notice.weather.app"),
            Title = Halo.Localization.Strings.Format("notice.heat.title", 34),
            Body = Halo.Localization.Strings.Format("notice.heat.body", HeatWatch.RiseC + 1),
            Icon = Badges.Hot(),
        },
    };

    private int NotifLeft() => _notch.WorkLeft + (_notch.WorkWidth - Sc(_curW)) / 2 + (int)_offsetX;

    private static readonly string HaloDir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo");
    private static readonly string OffsetPath = System.IO.Path.Combine(HaloDir, "offset");
    private static readonly string GreetedPath = System.IO.Path.Combine(HaloDir, "greeted");
    private static readonly string VisDebugPath = System.IO.Path.Combine(HaloDir, "vis-debug.txt");
    private static readonly string StripOrderPath = System.IO.Path.Combine(HaloDir, "strip-order.txt");

    private static readonly bool NetDebug = Probe(System.IO.Path.Combine(HaloDir, "net-debug"));
    private static readonly string NetDebugPath = System.IO.Path.Combine(HaloDir, "net-debug.txt");
    private long _netLogAt;

    private static bool Probe(string path)
    {
        try { return System.IO.File.Exists(path); } catch { return false; }
    }

    private void LogNet(Widgets.NetWidget nw, int index)
    {
        if (!NetDebug) return;
        long now = Environment.TickCount64;
        if (now - _netLogAt < 1000) return;
        _netLogAt = now;
        try
        {

            string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0:HH:mm:ss}  down {1,7:F1}  up {2,7:F1} KB/s   busy={3,-5} pinned={4,-5} active={5,-5} " +
                "primary={6} picked={7} idx={8}",
                DateTime.Now, _net.DownRate / 1024.0, _net.UpRate / 1024.0,
                _net.Busy, nw.Pinned, nw.IsActive, _primary, _userPicked, index);
            System.IO.File.AppendAllText(NetDebugPath, line + Environment.NewLine);
        }
        catch { }
    }
    private static readonly string SessionOrderPath = System.IO.Path.Combine(HaloDir, "session-order.txt");
    private static readonly string PinPath = System.IO.Path.Combine(HaloDir, "pinned");

    private void LoadOffset()
    {
        try { if (float.TryParse(System.IO.File.ReadAllText(OffsetPath), System.Globalization.CultureInfo.InvariantCulture, out var v)) _offsetX = v; }
        catch { }

        try
        {

            _pinned = _settings.Current.Bool(Halo.Settings.SettingsKeys.OverFullscreen,
                                             Halo.Settings.SettingsKeys.OverFullscreenDefault);
        }
        catch { }
    }

    private DateTime _offsetStamp;

    private void ReloadOffset()
    {
        try
        {
            var stamp = System.IO.File.GetLastWriteTimeUtc(OffsetPath);
            if (stamp == _offsetStamp) return;
            _offsetStamp = stamp;
            if (float.TryParse(System.IO.File.ReadAllText(OffsetPath),
                    System.Globalization.CultureInfo.InvariantCulture, out var v) && v != _offsetX)
                _offsetX = v;
        }
        catch { }
    }

    private void SaveOffset()
    {
        try
        {
            System.IO.File.WriteAllText(OffsetPath, _offsetX.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _offsetStamp = System.IO.File.GetLastWriteTimeUtc(OffsetPath);
        }
        catch { }
    }

    private void SavePin()
    {
        try { System.IO.File.WriteAllText(PinPath, _pinned ? "1" : "0"); } catch { }
        try { _settings.Set(Halo.Settings.SettingsKeys.OverFullscreen, _pinned ? "on" : "off"); } catch { }
    }

    private static readonly string RecordablePath = System.IO.Path.Combine(HaloDir, "capturable");
    private bool _recordable;

    private void LoadRecordable()
    {
        try
        {
            bool legacy = System.IO.File.Exists(RecordablePath)
                && System.IO.File.ReadAllText(RecordablePath).Trim() == "1";
            _recordable = _settings.Current.Bool(Halo.Settings.SettingsKeys.InCaptures, legacy);
        }
        catch { }
    }

    private void SaveRecordable()
    {
        try { System.IO.File.WriteAllText(RecordablePath, _recordable ? "1" : "0"); } catch { }
        try { _settings.Set(Halo.Settings.SettingsKeys.InCaptures, _recordable ? "on" : "off"); } catch { }
    }

    private bool PressOnControl(Win32.POINT p)
    {
        if (_progress <= 0.9f || _primary < 0 || _primary >= _widgets.Length) return false;

        if (OverPin(p)) return true;
        try
        {
            foreach (var (r, _) in _widgets[_primary].Buttons(ExpandedW, ExpandedH))
            {
                float bx = _el + r.X * S, by = _et + r.Y * S;
                if (p.X >= bx - 6 * S && p.X < bx + (r.Width + 6) * S
                    && p.Y >= by - 8 * S && p.Y < by + (r.Height + 8) * S) return true;
            }
        }
        catch { }
        return false;
    }

    private void UpdateMove(Win32.POINT p, bool down, bool hovered)
    {
        int centre = _notch.WorkLeft + _notch.WorkWidth / 2;
        const float snap = 55f;
        if (_moving)
        {
            if (down)
            {
                float raw = Math.Clamp(p.X - _moveGrabDX - centre,
                    -(_notch.WorkWidth / 2f - Sc(CollapsedW) / 2f - 8), _notch.WorkWidth / 2f - Sc(CollapsedW) / 2f - 8);
                _offsetX = MathF.Abs(raw) < snap ? 0f : raw;
            }
            else { if (MathF.Abs(_offsetX) < snap) _offsetX = 0f; _moving = false; _holdT = 0f; SaveOffset(); }
            return;
        }

        bool holding = down && hovered && !_resizing && _notif == null
                    && !FileTray.DragActive && _trayPressPath == null && _trayMode < 1
                    && !PressOnControl(p);
        bool still = Math.Abs(p.X - _holdAnchor.X) <= 8 && Math.Abs(p.Y - _holdAnchor.Y) <= 8;
        if (holding && _holdStart != DateTime.MaxValue && still)
        {
            _holdT = Math.Clamp((float)((DateTime.UtcNow - _holdStart).TotalSeconds / HoldSeconds), 0f, 1f);
            if (_holdT >= 1f) { _moving = true; _moveGrabDX = p.X - (int)(centre + _offsetX); _holdStart = DateTime.MaxValue; }
        }
        else if (holding) { _holdStart = DateTime.UtcNow; _holdAnchor = p; _holdT = 0f; }
        else { _holdStart = DateTime.MaxValue; _holdT = 0f; }
    }

    private void DrawHoldCue(Graphics g, int w, int h)
    {
        float t = SmoothStep(_holdT);
        float bw = (w - 64) * t;
        if (bw < 3f) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF((w - bw) / 2f, h - 6f, bw, 2.5f);
        using var p = Fx.Rounded(rect, 1.25f);
        using var br = new System.Drawing.Drawing2D.LinearGradientBrush(
            new RectangleF(rect.X - 0.5f, rect.Y, rect.Width + 1f, rect.Height),
            Color.White, Color.White, 0f);
        int peak = 25 + (int)(110 * t);
        br.InterpolationColors = new System.Drawing.Drawing2D.ColorBlend(3)
        {
            Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(peak, 255, 255, 255), Color.FromArgb(0, 255, 255, 255) },
            Positions = new[] { 0f, 0.5f, 1f },
        };
        g.FillPath(br, p);
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static float SmoothStep(float t) => t * t * (3f - 2f * t);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    internal static float EaseOutBack(float t)
    {
        const float c1 = 1.2f;
        const float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * MathF.Pow(p, 3f) + c1 * MathF.Pow(p, 2f);
    }
}
