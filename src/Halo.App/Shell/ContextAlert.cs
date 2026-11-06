using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Halo.Shell;

internal sealed class ContextAlert
{

    internal const float RearmDrop = 0.30f;
    internal static readonly TimeSpan Remember = TimeSpan.FromDays(7);
    private const int Cap = 300;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "ctx-warned.txt");

    private readonly Dictionary<string, DateTime> _warned = new(StringComparer.Ordinal);

    internal ContextAlert() => Load();

        internal static (bool Fire, bool Warned) Decide(bool warned, float frac, float warnAt)
    {
        if (frac >= warnAt) return (!warned, true);
        if (warned && frac < warnAt - RearmDrop) return (false, false);
        return (false, warned);
    }

        internal bool Check(string session, float frac, float warnAt, DateTime nowUtc)
    {
        if (session.Length == 0) return false;
        bool was = _warned.ContainsKey(session);
        var (fire, warned) = Decide(was, frac, warnAt);
        if (warned != was || fire)
        {
            if (warned) _warned[session] = nowUtc; else _warned.Remove(session);
            Save(nowUtc);
        }
        return fire;
    }

    internal static Dictionary<string, DateTime> Parse(IEnumerable<string> lines, DateTime nowUtc)
    {
        var d = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var parts = line.Split('\t');
            if (parts.Length != 2 || parts[0].Length == 0) continue;
            if (!long.TryParse(parts[1], out long secs)) continue;
            var at = DateTimeOffset.FromUnixTimeSeconds(secs).UtcDateTime;
            if (nowUtc - at <= Remember) d[parts[0]] = at;
        }
        return d;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            foreach (var kv in Parse(File.ReadAllLines(FilePath), DateTime.UtcNow)) _warned[kv.Key] = kv.Value;
        }
        catch { }
    }

    private void Save(DateTime nowUtc)
    {
        try
        {
            var keep = _warned.Where(kv => nowUtc - kv.Value <= Remember)
                              .OrderByDescending(kv => kv.Value).Take(Cap)
                              .Select(kv => kv.Key + "\t" + new DateTimeOffset(kv.Value, TimeSpan.Zero).ToUnixTimeSeconds());
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, keep);
        }
        catch { }
    }
}
