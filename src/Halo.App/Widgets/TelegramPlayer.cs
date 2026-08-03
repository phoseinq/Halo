using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using Halo.Interop;

namespace Halo.Widgets;

internal static class TelegramPlayer
{
    public static volatile bool Live;
    public static long LastLiveAt;
    public static int Version;
    public static volatile string? Debug;

    public static volatile string? Title;

    public static volatile bool VideoSource;

    public static volatile string? VideoDebug;

    private static readonly object _lock = new();
    private static TimeSpan _pos;
    private static TimeSpan? _dur;
    private static long _wanted;
    private static Thread? _thread;

    public static (TimeSpan pos, TimeSpan? dur) Read() { lock (_lock) return (_pos, _dur); }

    public static volatile bool Seekable = true;
    private static double _aimed = -1;
    private static long _aimedAt;

    private static void JudgeSeek(double f)
    {
        if (_aimed < 0) return;
        long age = Environment.TickCount64 - _aimedAt;
        if (age < 1200) return;

        if (Math.Abs(f - _aimed) <= 0.08) { Seekable = true; _aimed = -1; }
        else if (age > 6000) { Debug = $"seek ignored (aimed {_aimed:F2}, strip at {f:F2})"; Seekable = false; _aimed = -1; }
    }

    public static void Reset()
    {
        lock (_lock) { _pos = TimeSpan.Zero; _dur = null; Version++; }
    }

    public static bool SeekTo(double frac)
    {
        try
        {
            frac = Math.Clamp(frac, 0.0, 1.0);
            var auto = Uia.Create();
            if (auto == null) { Debug = "seek: no uia"; return false; }

            IntPtr hwnd; double[] sr;
            if (SampleVideo(auto) is { } vid)
            {
                hwnd = vid.hwnd; sr = Uia.PropRect(vid.slider);
            }
            else
            {
                if (FindStrip(auto) is not { } found) { Debug = "seek: no strip"; return false; }
                if (auto.CreatePropertyCondition(Uia.ClassNameProp, "class Ui::FilledSlider", out var cond) != 0)
                    return false;
                if (found.strip.FindFirst(Uia.TreeScopeDescendants, cond, out var slider) != 0 || slider == null)
                { Debug = "seek: no slider"; return false; }
                hwnd = found.hwnd; sr = Uia.PropRect(slider);
            }
            if (!PostClick(auto, hwnd, sr, frac, hover: false)) return false;
            Debug = null;
            _aimed = frac; _aimedAt = Environment.TickCount64;
            return true;
        }
        catch (Exception e) { Debug = "seek: " + e.Message; return false; }
    }

    internal static bool TitleMatches(string? strip, string? title)
    {
        string s = Norm(strip), t = Norm(title);
        if (s.Length < 3 || t.Length < 3) return false;
        return s.Contains(t, StringComparison.Ordinal) || t.Contains(s, StringComparison.Ordinal);
    }

    private static string Norm(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        bool space = false;
        foreach (char c in s.Trim().ToLowerInvariant())
        {

            int u = c;
            char n = u is 0x2013 or 0x2014 or 0x2012 or '_' ? '-' : c;
            if (char.IsWhiteSpace(n)) { space = sb.Length > 0; continue; }
            if (space) { sb.Append(' '); space = false; }
            sb.Append(n);
        }
        return sb.ToString();
    }

    private static string? _lastLogged;
    internal static void Log(string line)
    {
        try
        {
            if (line == _lastLogged) return;
            _lastLogged = line;
            string dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo");
            string path = System.IO.Path.Combine(dir, "tg-debug.txt");
            System.IO.Directory.CreateDirectory(dir);
            var f = new System.IO.FileInfo(path);
            if (f.Exists && f.Length > 200_000) f.Delete();
            System.IO.File.AppendAllText(path,
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + line + Environment.NewLine);
        }
        catch { }
    }

    public static volatile string? Speed;

    private const string SpeedClass = "class Media::Player::SpeedButton";

    internal static string? ParseSpeed(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        int i = name.LastIndexOf(':');
        if (i < 0 || i + 1 >= name.Length) return null;
        string s = name[(i + 1)..].Trim();
        return s.Length > 1 && (s.EndsWith('x') || s.EndsWith('X')) ? s.ToLowerInvariant() : null;
    }

    private static bool PostClick(IUIAutomation auto, IntPtr hwnd, double[] r, double fracX, bool hover)
    {
        if (r.Length < 4 || r[2] <= 1) return false;
        int sx = (int)(r[0] + r[2] * fracX), sy = (int)(r[1] + r[3] / 2);
        int cx, cy, limX, limY;
        if (!Win32.IsIconic(hwnd))
        {
            var pt = new Win32.POINT { X = sx, Y = sy };
            if (!Win32.ScreenToClient(hwnd, ref pt) || !Win32.GetClientRect(hwnd, out var cr))
            { Debug = "click: transform failed"; return false; }
            (cx, cy, limX, limY) = (pt.X, pt.Y, cr.right, cr.bottom);
        }
        else
        {
            if (auto.ElementFromHandle(hwnd, out var root) != 0 || root == null) return false;
            var wr = Uia.PropRect(root);
            if (wr.Length < 4 || wr[2] <= 1) { Debug = "click: window rect"; return false; }
            (cx, cy, limX, limY) = (sx - (int)wr[0], sy - (int)wr[1], (int)wr[2], (int)wr[3]);
        }
        if (cx < 0 || cy < 0 || cx >= limX || cy >= limY)
        { Debug = $"click: pt {cx},{cy} outside {limX}x{limY}"; return false; }

        IntPtr lp = (IntPtr)((cy << 16) | (cx & 0xFFFF));
        if (hover)
        {
            Win32.PostMessage(hwnd, 0x0200, IntPtr.Zero, lp);
            Thread.Sleep(200);
            Win32.PostMessage(hwnd, 0x0200, IntPtr.Zero, lp);
            Thread.Sleep(200);
        }
        Win32.PostMessage(hwnd, 0x0201, (IntPtr)1, lp);
        Thread.Sleep(hover ? 90 : 40);
        Win32.PostMessage(hwnd, 0x0202, IntPtr.Zero, lp);
        return true;
    }

    public static bool ToggleSpeed()
    {
        try
        {
            var auto = Uia.Create();
            if (auto == null) return false;
            if (FindStrip(auto) is not { } found) { Debug = "speed: no strip"; return false; }
            if (auto.CreatePropertyCondition(Uia.ClassNameProp, SpeedClass, out var cond) != 0) return false;
            if (found.strip.FindFirst(Uia.TreeScopeDescendants, cond, out var btn) != 0 || btn == null)
            { Debug = "speed: no button"; return false; }
            string? before = ParseSpeed(Uia.PropString(btn, Uia.NameProp));
            if (!PostClick(auto, found.hwnd, Uia.PropRect(btn), 0.5, hover: true)) return false;

            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(200);
                string? now = ParseSpeed(Uia.PropString(btn, Uia.NameProp));
                if (now != null && now != before) { Speed = now; return true; }
            }
            Speed = before;
            Debug = $"speed: telegram kept {before ?? "-"} after the click";
            return false;
        }
        catch (Exception e) { Debug = "speed: " + e.Message; return false; }
    }

    public static void Poke()
    {
        Interlocked.Exchange(ref _wanted, Environment.TickCount64);
        if (_thread != null) return;
        lock (_lock)
        {
            if (_thread != null) return;
            _thread = new Thread(Loop) { IsBackground = true, Name = "tg-uia" };
            _thread.Start();
        }
    }

    internal static (TimeSpan pos, TimeSpan? dur) Infer(double frac, TimeSpan text,
        double prevFrac, TimeSpan prevText, TimeSpan? prevDur)
    {
        frac = Math.Clamp(frac, 0.0, 1.0);

        if (frac <= 0.01 && text > TimeSpan.FromSeconds(10))
            return (TimeSpan.Zero, text);
        if (text > prevText && frac >= 0.02)
        {
            var est = TimeSpan.FromSeconds(Math.Round(text.TotalSeconds / frac));

            var dur = prevDur is { } known && Math.Abs((known - est).TotalSeconds) <= 3 ? known : est;
            return (text, dur);
        }
        if (text > prevText)
            return (text, prevDur);
        if (text == prevText && frac > prevFrac + 0.001 && text > TimeSpan.Zero)
            return (TimeSpan.FromSeconds(Math.Round(frac * text.TotalSeconds)), text);

        if (prevDur is { } d)
            return (text <= d ? text : TimeSpan.FromSeconds(Math.Round(frac * d.TotalSeconds)), d);
        return (text, null);
    }

    internal static TimeSpan? Settle(TimeSpan? settled, TimeSpan? candidate, TimeSpan pos)
    {
        var dur = settled;
        if (candidate is { } cand && (dur is not { } s || Math.Abs((cand - s).TotalSeconds) > 3))
            dur = cand;
        return dur is { } known && pos > known + TimeSpan.FromSeconds(1) ? null : dur;
    }

    internal static double? ParsePercent(string? s)
        => s != null && s.EndsWith('%')
           && double.TryParse(s[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var p)
            ? Math.Clamp(p / 100.0, 0.0, 1.0) : null;

    private static readonly Regex TimeRx = new(@"^\d{1,2}:\d{2}(:\d{2})?$", RegexOptions.Compiled);

    internal static TimeSpan? ParseTime(string? s)
    {
        if (s == null || !TimeRx.IsMatch(s)) return null;
        var parts = s.Split(':');
        try
        {
            return parts.Length == 3
                ? new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]))
                : new TimeSpan(0, int.Parse(parts[0]), int.Parse(parts[1]));
        }
        catch { return null; }
    }

    private static void Loop()
    {
        IUIAutomation? auto = null;
        IUIAutomationElement? strip = null;
        double prevFrac = -1; TimeSpan prevText = TimeSpan.MinValue;
        while (true)
        {
            Thread.Sleep(1000);
            if (Environment.TickCount64 - Interlocked.Read(ref _wanted) > 15_000)
            {
                Live = false; strip = null;
                Log("reader parked - nothing has poked it for 15s");
                continue;
            }
            try
            {
                auto ??= Uia.Create();
                if (auto == null) { Debug = "CUIAutomation failed"; Live = false; continue; }

                if (SampleVideo(auto) is { } vid)
                {
                    lock (_lock)
                    {
                        if (_pos != vid.pos || _dur != vid.dur) Version++;
                        _pos = vid.pos; _dur = vid.dur;
                    }
                    VideoSource = true; Title = null; Debug = null;
                    Live = true; LastLiveAt = Environment.TickCount64;
                    prevFrac = -1; prevText = TimeSpan.MinValue;
                    Log($"VIDEO claimed dur={vid.dur}");
                    continue;
                }
                if (VideoSource) Log("video released");
                VideoSource = false;
                if (VideoDebug is { } vd) Log("no video: " + vd);

                strip ??= FindStrip(auto)?.strip;
                if (strip == null) { Debug = "player strip not found"; Live = false; Title = null; continue; }

                double? frac = null; TimeSpan? text = null; string? label = null;
                if (!Sample(auto, strip, ref frac, ref text, ref label))
                {

                    Debug = "strip went stale"; strip = null; Live = false; Title = null;
                    continue;
                }
                Title = label;
                Speed = SampleSpeed(auto, strip);
                if (frac is not { } f || text is not { } t)
                { Debug = $"frac={frac?.ToString() ?? "-"} text={text?.ToString() ?? "-"}"; Live = false; continue; }
                Debug = null;
                JudgeSeek(f);

                bool live;
                lock (_lock)
                {
                    var (pos, durCand) = prevFrac < 0 ? Infer(f, t, f, t, _dur) : Infer(f, t, prevFrac, prevText, _dur);
                    bool changed = pos != _pos;
                    _pos = pos;
                    var next = Settle(_dur, durCand, pos);
                    if (next != _dur) { _dur = next; changed = true; }
                    if (changed) Version++;
                    live = _dur is not null;
                }
                prevFrac = f; prevText = t;
                Live = live;
                if (live) LastLiveAt = Environment.TickCount64;
                lock (_lock) Log($"music live={live} dur={(_dur?.ToString() ?? "-")} title={Title ?? "-"}");
            }
            catch (Exception e)
            {
                Debug = "com: " + e.Message;
                strip = null; Live = false;
            }
        }
    }

    private static bool IsMinus(char c)
    {
        int u = c;
        return u is '-' or 0x2212 or 0x2013 or 0x2014 or 0x2012;
    }

    internal static (TimeSpan pos, TimeSpan dur)? VideoClock(System.Collections.Generic.IEnumerable<string> labels)
    {
        TimeSpan? elapsed = null, left = null;
        foreach (string name in labels)
        {
            if (name.Length > 1 && IsMinus(name[0])) left ??= ParseTime(name[1..]);
            else elapsed ??= ParseTime(name);
        }
        if (elapsed is not { } pos || left is not { } rem || rem <= TimeSpan.Zero) return null;
        var dur = pos + rem;
        return dur > TimeSpan.Zero ? (pos, dur) : null;
    }

    private static readonly System.Collections.Generic.HashSet<IntPtr> _barren = new();
    private static long _barrenAt;

    private static (IntPtr hwnd, IUIAutomationElement slider, TimeSpan pos, TimeSpan dur)? SampleVideo(
        IUIAutomation auto)
    {

        long now = Environment.TickCount64;
        if (now - _barrenAt > 60_000) { _barren.Clear(); _barrenAt = now; }
        if (auto.CreatePropertyCondition(Uia.ClassNameProp, "class Ui::MediaSlider", out var sliderCond) != 0)
            return null;
        if (auto.CreatePropertyCondition(Uia.ControlTypeProp, Uia.TextType, out var textCond) != 0)
            return null;

        string? why = null;
        foreach (IntPtr hwnd in TelegramWindows())
        {

            if (_barren.Contains(hwnd)) continue;
            if (auto.ElementFromHandle(hwnd, out var root) != 0 || root == null) continue;
            if (root.FindAll(Uia.TreeScopeDescendants, sliderCond, out var arr) != 0 || arr == null) continue;
            if (arr.get_Length(out int n) != 0 || n == 0) { _barren.Add(hwnd); continue; }

            IUIAutomationElement? seek = null; double widest = 0;
            for (int i = 0; i < n; i++)
            {
                if (arr.GetElement(i, out var e) != 0 || e == null) continue;
                var r = Uia.PropRect(e);
                if (r.Length < 4 || r[2] <= widest) continue;
                widest = r[2]; seek = e;
            }
            if (seek == null) { why = $"{n} mediasliders, none with a rect"; continue; }

            if (root.FindAll(Uia.TreeScopeDescendants, textCond, out var texts) != 0 || texts == null) continue;
            if (texts.get_Length(out int tn) != 0) continue;
            var labels = new System.Collections.Generic.List<string>(tn);
            var seen = new System.Text.StringBuilder();
            for (int i = 0; i < tn; i++)
            {
                if (texts.GetElement(i, out var e) != 0 || e == null) continue;
                string name = Uia.PropString(e, Uia.NameProp);
                labels.Add(name);
                if (seen.Length < 120) seen.Append('[').Append(name).Append(']');
            }
            if (VideoClock(labels) is not { } clock)
            { why = $"{n} sliders, {tn} texts {seen} did not read as a video clock"; continue; }
            VideoDebug = null;
            return (hwnd, seek, clock.pos, clock.dur);
        }
        VideoDebug = why;
        return null;
    }

    private static (IntPtr hwnd, IUIAutomationElement strip)? FindStrip(IUIAutomation auto)
    {
        if (auto.CreatePropertyCondition(Uia.ClassNameProp, "class Media::Player::Widget", out var cond) != 0)
            return null;
        foreach (IntPtr hwnd in TelegramWindows())
        {
            if (auto.ElementFromHandle(hwnd, out var root) != 0 || root == null) continue;
            if (root.FindFirst(Uia.TreeScopeDescendants, cond, out var found) == 0 && found != null)
                return (hwnd, found);
        }
        return null;
    }

    private static string? SampleSpeed(IUIAutomation auto, IUIAutomationElement strip)
    {
        try
        {
            if (auto.CreatePropertyCondition(Uia.ClassNameProp, SpeedClass, out var cond) != 0) return null;
            if (strip.FindFirst(Uia.TreeScopeDescendants, cond, out var btn) != 0 || btn == null) return null;
            return ParseSpeed(Uia.PropString(btn, Uia.NameProp));
        }
        catch { return null; }
    }

    private static bool Sample(IUIAutomation auto, IUIAutomationElement strip,
        ref double? frac, ref TimeSpan? text, ref string? title)
    {
        if (auto.CreatePropertyCondition(Uia.ClassNameProp, "class Ui::FilledSlider", out var sliderCond) != 0)
            return false;
        if (strip.FindFirst(Uia.TreeScopeDescendants, sliderCond, out var slider) != 0 || slider == null)
            return false;
        frac = ParsePercent(Uia.PatternValue(slider));

        if (auto.CreatePropertyCondition(Uia.ControlTypeProp, Uia.TextType, out var textCond) != 0)
            return false;
        if (strip.FindAll(Uia.TreeScopeDescendants, textCond, out var texts) != 0 || texts == null)
            return false;
        if (texts.get_Length(out int n) != 0) return false;

        for (int i = 0; i < n; i++)
        {
            if (texts.GetElement(i, out var e) != 0 || e == null) continue;
            string? name = Uia.PropString(e, Uia.NameProp);
            if (ParseTime(name) is { } t) { text ??= t; }
            else if (!string.IsNullOrWhiteSpace(name)) title ??= name;
        }
        return true;
    }

    private static IntPtr FindTelegramWindow()
    {
        var all = TelegramWindows();
        return all.Count > 0 ? all[0] : IntPtr.Zero;
    }

    private static System.Collections.Generic.List<IntPtr> TelegramWindows()
    {
        var found = new System.Collections.Generic.List<IntPtr>();
        try
        {
            Win32.EnumWindows((hwnd, _) =>
            {
                try
                {
                    if (!Win32.IsWindowVisible(hwnd)) return true;
                    Win32.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid == 0) return true;
                    using var p = System.Diagnostics.Process.GetProcessById((int)pid);
                    if (p.ProcessName.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
                        found.Add(hwnd);
                    return true;
                }
                catch { return true; }
            }, IntPtr.Zero);
        }
        catch { }
        return found;
    }

    internal static void DumpTree(System.IO.TextWriter w)
    {
        var auto = Uia.Create();
        if (auto == null) { w.WriteLine("CUIAutomation failed"); return; }
        var wins = TelegramWindows();
        w.WriteLine($"visible telegram windows: {wins.Count}");
        foreach (IntPtr hwnd in wins)
        {
            var buf = new char[256];
            int len = Win32.GetClassName(hwnd, buf, buf.Length);
            w.WriteLine($"== hwnd 0x{hwnd.ToInt64():X} class='{new string(buf, 0, Math.Max(0, len))}' iconic={Win32.IsIconic(hwnd)}");
            if (auto.ElementFromHandle(hwnd, out var root) != 0 || root == null) { w.WriteLine("   no uia root"); continue; }

            foreach (int type in new[] { Uia.SliderType, Uia.TextType })
            {
                if (auto.CreatePropertyCondition(Uia.ControlTypeProp, type, out var cond) != 0) continue;
                if (root.FindAll(Uia.TreeScopeDescendants, cond, out var arr) != 0 || arr == null) continue;
                if (arr.get_Length(out int n) != 0) continue;
                w.WriteLine($"   {(type == Uia.SliderType ? "sliders" : "texts")}: {n}");

                for (int i = 0; i < Math.Min(n, 600); i++)
                {
                    if (arr.GetElement(i, out var e) != 0 || e == null) continue;
                    string name = Uia.PropString(e, Uia.NameProp);

                    if (type == Uia.TextType && !(name.Contains(':') && name.Length <= 12) && name.Length is 0 or > 60)
                        continue;
                    var r = Uia.PropRect(e);
                    string rect = r.Length >= 4 ? $"{(int)r[0]},{(int)r[1]} {(int)r[2]}x{(int)r[3]}" : "-";
                    w.WriteLine($"     [{i}] cls='{Uia.PropString(e, Uia.ClassNameProp)}' name='{name}' " +
                                $"value='{Uia.PatternValue(e) ?? "-"}' rect={rect}");
                }
            }
        }
    }
}
