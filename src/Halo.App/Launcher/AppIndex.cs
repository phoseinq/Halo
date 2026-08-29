using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Halo.Launcher;

internal sealed class AppIndex : IDisposable
{
    internal const int DefaultDebounceMs = 1500;

    private readonly string _cachePath;
    private readonly Func<IReadOnlyList<AppEntry>> _scan;
    private readonly int _debounceMs;
    private readonly object _gate = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private Timer? _debounce;
    private IReadOnlyList<AppEntry> _apps = [];
    private bool _ready;
    private int _scanning;

    internal AppIndex(string cachePath, Func<IReadOnlyList<AppEntry>> scan, int debounceMs = DefaultDebounceMs)
    {
        _cachePath = cachePath;
        _scan = scan;
        _debounceMs = debounceMs;
    }

    internal event Action? Changed;

    internal IReadOnlyList<AppEntry> Apps { get { lock (_gate) return _apps; } }
    internal bool Ready { get { lock (_gate) return _ready; } }

    internal void Start()
    {
        var cached = AppCache.Read(_cachePath);
        if (cached.Count > 0) lock (_gate) { _apps = cached; _ready = true; }
        Watch();
        RefreshSoon();
    }

    internal void RefreshSoon()
    {
        try
        {
            _debounce ??= new Timer(_ => Refresh(), null, Timeout.Infinite, Timeout.Infinite);
            _debounce.Change(_debounceMs, Timeout.Infinite);
        }
        catch { }
    }

    private void Refresh()
    {

        if (Interlocked.Exchange(ref _scanning, 1) == 1) return;
        try
        {
            IReadOnlyList<AppEntry> found;
            try { found = _scan(); }
            catch { return; }

            if (found.Count == 0) return;
            lock (_gate) { _apps = found; _ready = true; }
            AppCache.Save(_cachePath, found);
        }
        finally
        {
            Interlocked.Exchange(ref _scanning, 0);

            try { Changed?.Invoke(); } catch { }
        }
    }

    private void Watch()
    {
        foreach (var root in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
        })
        {
            try
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                var w = new FileSystemWatcher(root) { IncludeSubdirectories = true, EnableRaisingEvents = true };
                w.Created += (_, _) => RefreshSoon();
                w.Deleted += (_, _) => RefreshSoon();
                w.Renamed += (_, _) => RefreshSoon();
                _watchers.Add(w);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        foreach (var w in _watchers) { try { w.Dispose(); } catch { } }
        _watchers.Clear();
        try { _debounce?.Dispose(); } catch { }
        _debounce = null;
    }
}
