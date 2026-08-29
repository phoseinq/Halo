using System;
using System.Collections.Generic;

namespace Halo.Panels;

internal sealed class PanelStore
{

    internal readonly record struct Snapshot(PanelSpec Spec, string Id, DateTimeOffset Expires,
        DateTimeOffset PublishedAt);

    private readonly object _gate = new();
    private Snapshot? _current;
    private int _version;

    internal const int DefaultSeconds = 300, MaxSeconds = 3600;

    internal int Version => System.Threading.Volatile.Read(ref _version);

    internal Snapshot? Current
    {
        get
        {
            lock (_gate)
            {
                if (_current is not { } snap) return null;
                if (DateTimeOffset.UtcNow >= snap.Expires) { _current = null; Bump(); return null; }
                return snap;
            }
        }
    }

    internal bool IsLive => Current is not null;

    internal string Publish(PanelSpec spec, double seconds)
    {
        string id = Guid.NewGuid().ToString("n");
        var expires = DateTimeOffset.UtcNow.AddSeconds(Math.Clamp(seconds, 1, MaxSeconds));
        lock (_gate)
        {

            _current = new Snapshot(spec, id, expires, DateTimeOffset.UtcNow);
            Bump();
        }
        return id;
    }

    internal bool Close(string? id = null)
    {
        lock (_gate)
        {
            if (_current is not { } snap) return false;

            if (id is { Length: > 0 } && !string.Equals(id, snap.Id, StringComparison.Ordinal)) return false;
            _current = null;
            Bump();
            return true;
        }
    }

    internal bool Apply(int row, double value)
    {
        lock (_gate)
        {
            if (_current is not { } snap) return false;
            var next = PanelHit.With(snap.Spec, row, value);
            if (ReferenceEquals(next, snap.Spec)) return false;
            _current = snap with { Spec = next };
            Bump();
            return true;
        }
    }

    internal IReadOnlyDictionary<string, double> Values()
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal);
        if (Current is not { } snap) return values;
        foreach (var row in snap.Spec.Rows)
            if (row.Id.Length > 0 && row.Kind is PanelRowKind.Slider or PanelRowKind.Toggle or PanelRowKind.Buttons)
                values[row.Id] = row.Value;
        return values;
    }

    private void Bump() => System.Threading.Interlocked.Increment(ref _version);
}
