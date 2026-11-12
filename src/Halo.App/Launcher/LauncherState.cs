using System;
using System.Linq;
using System.Collections.Generic;

namespace Halo.Launcher;

internal enum LauncherRowKind { Inert, Settings, App, Notice, Page, Action, Info, Back, Tick }

internal sealed record LauncherRow(string Label, string? Aumid, bool Enabled, LauncherRowKind Kind,
                                   string? Id = null, string? Detail = null, string? Glyph = null,
                                   System.Drawing.Color? Tint = null, int Dots = 0, bool Pin = false, bool KeepPinIcon = false);

internal sealed class LauncherState
{
    internal const int MaxQuery = 200;

    internal const int MaxPageRows = 12;
    internal const string IndexingLabel = "still reading your apps...";

    internal const string PageQuick = "quick";
    internal const string PageSystem = "sysinfo";
    internal const string PageClipboard = "clipboard";
    internal const string PageReminders = "reminders";
    internal const string PageTranslate = "translate";
    internal const string PageAddAction = "addaction";
    internal const string PageReminderEdit = "reminderedit";
    internal const string PageClipPins = "clippins";
    internal const string PageVault = "vault";

    internal static IReadOnlyList<LauncherRow> Menu { get; } =
    [
        new("Quick Actions", null, true, LauncherRowKind.Page, PageQuick,
            Detail: "mute, lock, sleep and your own", Tint: Halo.Widgets.Fx.VitalMemory),
        new("System Info", null, true, LauncherRowKind.Page, PageSystem,
            Detail: "live load and this machine", Tint: Halo.Widgets.Fx.VitalCpu),
        new("Clipboard History", null, true, LauncherRowKind.Page, PageClipboard,
            Detail: "what you copied, one click back", Tint: Halo.Widgets.Fx.VitalStorage),
        new("Translate", null, true, LauncherRowKind.Page, PageTranslate,
            Detail: "type a line, get it back", Tint: Halo.Widgets.Fx.VitalGpu),
        new("Reminders", null, true, LauncherRowKind.Page, PageReminders,
            Detail: "say what, pick when", Tint: Halo.Widgets.Fx.VitalBattery),
        new("Settings", null, true, LauncherRowKind.Settings,
            Detail: "everything Halo can do", Tint: Halo.Widgets.Fx.VitalOs),
    ];

    internal Func<string, int>? PageBadge;
    internal Func<string, LauncherExtras.Set>? Extras;
    internal Func<string, IReadOnlyList<LauncherRow>>? FileHits;

    private readonly Func<IReadOnlyList<AppEntry>> _apps;
    private readonly Func<bool> _ready;
    private readonly LaunchStats _stats;
    private readonly Func<DateTimeOffset> _now;
    private string _query = "";
    private string? _page;
    private IReadOnlyList<LauncherRow> _rows;
    private int _selected;

    internal Func<string, string, IReadOnlyList<LauncherRow>>? PageRows;

    internal Func<IReadOnlyList<LauncherPages.Gauge>>? PageGauges;

    internal Func<IReadOnlyList<LauncherRow>>? LanguageRows;

    internal LauncherState(Func<IReadOnlyList<AppEntry>> apps, Func<bool> ready,
                           LaunchStats stats, Func<DateTimeOffset> now)
    {
        _apps = apps; _ready = ready; _stats = stats; _now = now;
        _rows = Menu;
        _selected = FirstEnabled(Menu);
    }

    internal string Query => _query;
    internal string? Page => _page;
    internal IReadOnlyList<LauncherPages.Gauge> Gauges { get; private set; } = [];
    internal int HotGauge { get; private set; } = -1;
    internal int HotRing { get; private set; } = -1;

    internal bool ShowGauges => _page == PageSystem && _query.Length == 0 && Gauges.Count > 0;

        internal void RefreshKeeping()
    {
        var was = _selected >= 0 && _selected < _rows.Count ? _rows[_selected] : null;
        Rebuild();
        if (was is null) return;
        for (int i = 0; i < _rows.Count; i++)
            if (_rows[i].Enabled && _rows[i].Id == was.Id && _rows[i].Aumid == was.Aumid && _rows[i].Label == was.Label)
            {
                _selected = i;
                return;
            }
    }

        internal enum LangPick { None, From, To }

    internal LangPick Picking { get; private set; }

        internal bool ShowLangBar => _page == PageTranslate && Picking == LangPick.None;

    internal void OpenPicker(LangPick which)
    {
        if (which == LangPick.None || _page != PageTranslate) return;
        Picking = which;
        _query = "";
        Rebuild();
    }

    internal bool ClosePicker()
    {
        if (Picking == LangPick.None) return false;
        Picking = LangPick.None;
        _query = "";
        Rebuild();
        return true;
    }

    internal void SetHotGauge(int index, int ring = -1) { HotGauge = index; HotRing = ring; }

        internal bool ShowClearAll => _page == PageClipboard && _query.Length == 0;

        internal bool ClearArmed { get; private set; }
    internal bool ClearHot { get; private set; }

    internal void SetClear(bool hot, bool armed) { ClearHot = hot; ClearArmed = armed; }

        internal int HotTick { get; private set; } = -1;
    internal void SetHotTick(int index) => HotTick = index;

        internal DateTimeOffset? ReminderWhen { get; private set; }

    internal void SetReminderWhen(DateTimeOffset when) => ReminderWhen = when;

        internal DateTimeOffset? EffectiveWhen => ReminderStore.Understand(_query, _now())?.When ?? ReminderWhen;

    internal string ReminderText => (ReminderStore.Understand(_query, _now())?.Text ?? _query).Trim();

        internal string? EditingReminder { get; private set; }

        internal void EditReminder(Reminder r)
    {
        EditingReminder = r.Id;
        ReminderWhen = r.When;
        _page = PageReminderEdit;
        _query = r.Text.Length <= MaxQuery ? r.Text : r.Text[..MaxQuery];
        Rebuild();
    }

    internal void EditChoice(LauncherRow row)
    {
        if (row.Id is not { } id || !id.StartsWith(LauncherPages.AddPrefix, StringComparison.Ordinal)) return;
        if (!long.TryParse(id[LauncherPages.AddPrefix.Length..], out long unix)) return;
        string line = ReminderStore.EditLine(_query, DateTimeOffset.FromUnixTimeSeconds(unix), _now());
        _query = line.Length <= MaxQuery ? line : line[..MaxQuery];
        Rebuild();
    }

    internal bool RefreshGauges()
    {
        if (_page != PageSystem) return false;
        var fresh = PageGauges?.Invoke() ?? [];
        Gauges = fresh;
        return true;
    }
    internal int Selected => _selected;
    internal IReadOnlyList<LauncherRow> Rows => _rows;

    internal bool FieldIsTitle => _page == PageSystem;

    internal void Type(char c)
    {
        if (c < ' ' || c == (char)0x7F) return;
        if (FieldIsTitle) return;

        if (c == ' ' && _query.Length == 0) return;
        if (_query.Length >= MaxQuery) return;
        _query += c;
        Rebuild();
    }

    internal void Backspace()
    {
        if (_query.Length == 0) return;
        _query = _query[..^1];
        Rebuild();
    }

        internal bool BackOrErase()
    {
        if (_query.Length > 0) { Backspace(); return true; }
        return Back();
    }

        internal void MoveEdge(bool toEnd)
    {
        int step = toEnd ? -1 : 1;
        int back = !toEnd && _page is not null ? 1 : 0;
        for (int at = toEnd ? _rows.Count - 1 : back; at >= 0 && at < _rows.Count; at += step)
            if (_rows[at].Enabled) { _selected = at; return; }
    }

    internal void Reset()
    {
        _query = "";
        Rebuild();
    }

        internal enum TabResult { Ignored, Changed, CycleTranslatePair }

    private static readonly string[] ReminderTemplates =
        ["in 20m ", "in 2 hours ", "at 17:30 ", "tomorrow 9am "];

    internal string Completion
    {
        get
        {
            if (_page is not null && PageTakesText(_page)) return "";
            string typed = _query;
            if (typed.Length == 0 || _selected < 0 || _selected >= _rows.Count) return "";
            var row = _rows[_selected];
            if (!row.Enabled || row.Kind is LauncherRowKind.Back or LauncherRowKind.Notice) return "";
            if (LauncherExtras.FromQuery(row.Id) || row.Id?.StartsWith(LauncherExtras.FilePrefix, StringComparison.Ordinal) == true) return "";
            string label = row.Label;
            if (label.Length <= typed.Length || label.Length > MaxQuery) return "";
            return label.StartsWith(typed, StringComparison.OrdinalIgnoreCase)
                ? label[typed.Length..] : "";
        }
    }

    internal TabResult Tab()
    {
        if (_page is not null && PageTakesText(_page))
        {
            if (_page == PageTranslate) return TabResult.CycleTranslatePair;

            if (_page == PageReminders && _query.Trim().Length > 0 && Array.IndexOf(ReminderTemplates, _query) < 0
                && _selected >= 0 && _selected < _rows.Count
                && (_rows[_selected].Id ?? "").StartsWith(LauncherPages.AddPrefix, StringComparison.Ordinal))
            {
                EditChoice(_rows[_selected]);
                return TabResult.Changed;
            }
            int at = Array.IndexOf(ReminderTemplates, _query);
            _query = ReminderTemplates[(at + 1) % ReminderTemplates.Length];
            Rebuild();
            return TabResult.Changed;
        }

        if (_query.Trim().Length == 0)
        {
            int before = _selected;
            Move(1);
            return _selected == before ? TabResult.Ignored : TabResult.Changed;
        }

        if (_selected < 0 || _selected >= _rows.Count) return TabResult.Ignored;
        var row = _rows[_selected];

        if (!row.Enabled || row.Kind is LauncherRowKind.Back or LauncherRowKind.Notice) return TabResult.Ignored;
        if (LauncherExtras.FromQuery(row.Id)) return TabResult.Ignored;

        if (row.Glyph == FileSearch.FolderGlyph && row.Id is { } folder && folder.StartsWith(LauncherExtras.FilePrefix, StringComparison.Ordinal))
        {
            string inside = folder[LauncherExtras.FilePrefix.Length..].TrimEnd('\\') + "\\";
            if (inside.Length > MaxQuery) return TabResult.Ignored;
            _query = inside;
            Rebuild();
            return TabResult.Changed;
        }
        if (row.Id?.StartsWith(LauncherExtras.FilePrefix, StringComparison.Ordinal) == true) return TabResult.Ignored;
        if (row.Label.Length == 0 || row.Label.Length > MaxQuery) return TabResult.Ignored;
        if (string.Equals(_query, row.Label, StringComparison.Ordinal)) return TabResult.Ignored;
        _query = row.Label;
        Rebuild();
        return TabResult.Changed;
    }

    internal void Refresh() => Rebuild();

    internal string Placeholder => Picking == LangPick.None ? PlaceholderFor(_page) : "Search languages...";

    internal static string PlaceholderFor(string? page) => page switch
    {
        PageQuick => "Filter actions...",
        PageSystem => "Filter system info...",
        PageClipboard => "Search clipboard...",
        PageReminders => "Remind me to...",
        PageTranslate => "Type a line to translate...",
        PageAddAction => "Name | a file, folder, app or link...",
        PageReminderEdit => "What to be reminded of...",
        PageClipPins => "Search pinned...",
        PageVault => "Search locked...",
        _ => "Search apps, files, math...",
    };

    internal static bool PageTakesText(string page) => page is PageReminders or PageTranslate or PageAddAction or PageReminderEdit;

    internal void GoTo(string page)
    {
        if (string.IsNullOrEmpty(page)) return;
        EditingReminder = null;
        _page = page;
        Picking = LangPick.None;
        _query = "";
        Rebuild();
    }

    internal bool Back()
    {

        if (ClosePicker()) return true;
        if (_page is null) return false;

        EditingReminder = null;
        _page = _page == PageAddAction ? PageQuick
              : _page is PageClipPins or PageVault ? PageClipboard
              : _page == PageReminderEdit ? PageReminders : null;
        _query = "";
        Rebuild();
        return true;
    }

    internal void Move(int delta)
    {
        if (_rows.Count == 0) return;
        int at = Math.Clamp(_selected + delta, 0, _rows.Count - 1);
        int step = delta >= 0 ? 1 : -1;
        while (at >= 0 && at < _rows.Count && !_rows[at].Enabled) at += step;
        if (at >= 0 && at < _rows.Count) _selected = at;
    }

    internal void SelectAt(int index)
    {
        if (index < 0 || index >= _rows.Count) return;
        if (!_rows[index].Enabled) return;
        _selected = index;
    }

    internal LauncherRow? Activate(int index)
    {
        if (index < 0 || index >= _rows.Count) return null;
        var row = _rows[index];
        if (!row.Enabled) return null;
        _selected = index;
        return row;
    }

    internal LauncherRow? Enter()
    {
        if (_selected < 0 || _selected >= _rows.Count) return null;
        var row = _rows[_selected];
        return row.Enabled ? row : null;
    }

    private void Rebuild()
    {
        if (Picking != LangPick.None)
        {
            Gauges = [];
            HotGauge = -1; HotRing = -1;
            var langs = LanguageRows?.Invoke() ?? [];
            string want = _query.Trim();
            if (want.Length > 0)
                langs = [.. langs.Where(r => r.Label.Contains(want, StringComparison.OrdinalIgnoreCase)
                                          || (r.Id ?? "").Contains(want, StringComparison.OrdinalIgnoreCase))];

            var withBack = new List<LauncherRow>(langs.Count + 1)
            {
                new("Back", null, true, LauncherRowKind.Back, null, null, "\uE72B"),
            };
            withBack.AddRange(langs);
            _rows = withBack;
            _selected = withBack.Count > 1 ? 1 : 0;
            return;
        }

        if (_page is not null)
        {
            Gauges = _page == PageSystem ? (PageGauges?.Invoke() ?? []) : [];
            HotGauge = -1; HotRing = -1;
            var page = PageRows?.Invoke(_page, _query) ?? [];
            string needle = PageTakesText(_page) ? "" : _query.Trim();
            if (needle.Length > 0)
                page = [.. page.Where(r => r.Label.Contains(needle, StringComparison.OrdinalIgnoreCase)
                                        || (r.Detail ?? "").Contains(needle, StringComparison.OrdinalIgnoreCase))];

            if (_page == PageClipboard && page.Count > MaxPageRows)
            {
                var trimmed = new List<LauncherRow>(MaxPageRows + 1)
                {
                    new($"{page.Count - MaxPageRows} older - type to search them", null, false, LauncherRowKind.Notice),
                };
                trimmed.AddRange(page.Skip(page.Count - MaxPageRows));
                page = trimmed;
            }
            else if (page.Count > MaxPageRows)
            {
                int hidden = page.Count - MaxPageRows;
                var shown = new List<LauncherRow>(page.Take(MaxPageRows))
                {
                    new($"{hidden} more - keep typing to narrow", null, false, LauncherRowKind.Notice),
                };
                page = shown;
            }

            var withBack = new List<LauncherRow>(page.Count + 1)
            {
                new("Back", null, true, LauncherRowKind.Back, null, null, "\uE72B"),
            };
            withBack.AddRange(page);
            _rows = withBack;

            int first = 1;
            while (first < withBack.Count && !withBack[first].Enabled) first++;
            _selected = first < withBack.Count ? first : 0;
            if (_page == PageReminderEdit && withBack[^1].Enabled) _selected = withBack.Count - 1;
            if (_page == PageClipboard && _query.Length == 0)
            {

                for (int at = withBack.Count - 1; at > 0; at--)
                    if (withBack[at].Enabled && withBack[at].Kind == LauncherRowKind.Action
                        && withBack[at].Id != ClipboardHistory.OpenVault) { _selected = at; break; }
            }
            return;
        }

        Gauges = [];
        HotGauge = -1; HotRing = -1;
        if (_query.Length == 0)
        {
            _rows = HomeRows();
            _selected = FirstEnabled(_rows);
            return;
        }
        if (_query.Trim().Length == 0) { _rows = Menu; _selected = FirstEnabled(Menu); return; }

        var extras = Extras?.Invoke(_query);
        if (extras is { Exclusive: true } only)
        {
            _rows = only.Above;
            _selected = 0;
            return;
        }

        var hits = AppMatch.Top(_apps(), _query, id => _stats.ScoreOf(id, _now()));
        if (hits.Count == 0 && !_ready() && extras is null)
        {

            _rows = [new LauncherRow(IndexingLabel, null, false, LauncherRowKind.Notice)];
            _selected = 0;
            return;
        }

        var rows = new List<LauncherRow>(hits.Count + 5);
        if (extras is { } above) rows.AddRange(above.Above);
        foreach (var a in hits)
            rows.Add(new LauncherRow(a.Name, a.Aumid, true, LauncherRowKind.App));
        if (FileHits?.Invoke(_query) is { Count: > 0 } files) rows.AddRange(files);
        if (extras is { } below) rows.AddRange(below.Below);
        _rows = rows;
        _selected = 0;
    }

    private IReadOnlyList<LauncherRow> HomeRows()
    {
        if (PageBadge is null) return Menu;
        var rows = new List<LauncherRow>(Menu.Count);
        foreach (var m in Menu)
            rows.Add(m.Id is { } id && PageBadge(id) is > 0 and var n ? m with { Dots = n } : m);
        return rows;
    }

    private static int FirstEnabled(IReadOnlyList<LauncherRow> rows)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].Enabled) return i;
        return 0;
    }
}
