using System;
using System.Linq;
using System.Collections.Generic;

namespace Halo.Launcher;

internal enum LauncherRowKind { Inert, Settings, App, Notice, Page, Action, Info, Back, Tick }

internal sealed record LauncherRow(string Label, string? Aumid, bool Enabled, LauncherRowKind Kind,
                                   string? Id = null, string? Detail = null, string? Glyph = null,
                                   System.Drawing.Color? Tint = null);

internal sealed class LauncherState
{
    internal const int MaxQuery = 64;

    internal const int MaxPageRows = 12;
    internal const string IndexingLabel = "still reading your apps...";

    internal const string PageQuick = "quick";
    internal const string PageSystem = "sysinfo";
    internal const string PageClipboard = "clipboard";
    internal const string PageReminders = "reminders";
    internal const string PageTranslate = "translate";

    internal static IReadOnlyList<LauncherRow> Menu { get; } =
    [
        new("Quick Actions", null, true, LauncherRowKind.Page, PageQuick),
        new("System Info", null, true, LauncherRowKind.Page, PageSystem),
        new("Clipboard History", null, true, LauncherRowKind.Page, PageClipboard),
        new("Translate", null, true, LauncherRowKind.Page, PageTranslate,
            Tint: Halo.Widgets.Fx.VitalGpu),
        new("Reminders", null, true, LauncherRowKind.Page, PageReminders,
            Tint: Halo.Widgets.Fx.VitalBattery),
        new("Settings", null, true, LauncherRowKind.Settings, Tint: Halo.Widgets.Fx.VitalOs),
    ];

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

    internal bool RefreshGauges()
    {
        if (_page != PageSystem) return false;
        var fresh = PageGauges?.Invoke() ?? [];
        Gauges = fresh;
        return true;
    }
    internal int Selected => _selected;
    internal IReadOnlyList<LauncherRow> Rows => _rows;

    internal void Type(char c)
    {
        if (c < ' ' || c == (char)0x7F) return;

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
        _ => "Search apps...",
    };

    internal static bool PageTakesText(string page) => page is PageReminders or PageTranslate;

    internal void GoTo(string page)
    {
        if (string.IsNullOrEmpty(page)) return;
        _page = page;
        Picking = LangPick.None;
        _query = "";
        Rebuild();
    }

    internal bool Back()
    {

        if (ClosePicker()) return true;
        if (_page is null) return false;
        _page = null;
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

            if (page.Count > MaxPageRows)
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
            return;
        }

        Gauges = [];
        HotGauge = -1; HotRing = -1;
        if (_query.Trim().Length == 0) { _rows = Menu; _selected = FirstEnabled(Menu); return; }

        var hits = AppMatch.Top(_apps(), _query, id => _stats.ScoreOf(id, _now()));
        if (hits.Count == 0 && !_ready())
        {

            _rows = [new LauncherRow(IndexingLabel, null, false, LauncherRowKind.Notice)];
            _selected = 0;
            return;
        }

        var rows = new List<LauncherRow>(hits.Count);
        foreach (var a in hits) rows.Add(new LauncherRow(a.Name, a.Aumid, true, LauncherRowKind.App));
        _rows = rows;
        _selected = 0;
    }

    private static int FirstEnabled(IReadOnlyList<LauncherRow> rows)
    {
        for (int i = 0; i < rows.Count; i++) if (rows[i].Enabled) return i;
        return 0;
    }
}
