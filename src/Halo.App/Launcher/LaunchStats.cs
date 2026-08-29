using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace Halo.Launcher;

internal sealed record LaunchRecord(int Count, DateTimeOffset Last);

internal sealed class LaunchStats
{
    internal const int MaxEntries = 300;
    internal const double HalfLifeDays = 30.0;

    private readonly Dictionary<string, LaunchRecord> _by = new(StringComparer.OrdinalIgnoreCase);

    internal static double Score(LaunchRecord r, DateTimeOffset now)
    {

        double days = Math.Max(0.0, (now - r.Last).TotalDays);
        return r.Count * Math.Pow(0.5, days / HalfLifeDays);
    }

    internal double ScoreOf(string aumid, DateTimeOffset now)
        => _by.TryGetValue(aumid, out var r) ? Score(r, now) : 0.0;

    internal void Record(string aumid, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(aumid)) return;
        int count = _by.TryGetValue(aumid, out var r) ? r.Count + 1 : 1;
        _by[aumid] = new LaunchRecord(count, now);
    }

    internal static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Halo", "launches.json");

    internal string ToJson(DateTimeOffset now)
    {
        var kept = new List<KeyValuePair<string, LaunchRecord>>(_by);
        kept.Sort((x, y) => Score(y.Value, now).CompareTo(Score(x.Value, now)));
        var arr = new JsonArray();
        for (int i = 0; i < kept.Count && i < MaxEntries; i++)
            arr.Add(new JsonObject
            {
                ["a"] = kept[i].Key,
                ["n"] = kept[i].Value.Count,
                ["t"] = kept[i].Value.Last.ToString("o", CultureInfo.InvariantCulture),
            });
        return new JsonObject { ["apps"] = arr }.ToJsonString();
    }

    internal static LaunchStats FromJson(string? json)
    {
        var s = new LaunchStats();
        try
        {
            if (string.IsNullOrWhiteSpace(json)) return s;
            if (JsonNode.Parse(json) is not JsonObject root) return s;
            if (root["apps"] is not JsonArray arr) return s;
            foreach (var node in arr)
            {
                if (node is not JsonObject o) continue;
                string aumid = o["a"] is JsonValue av && av.TryGetValue<string>(out var id) ? id : "";
                if (string.IsNullOrWhiteSpace(aumid)) continue;
                if (o["n"] is not JsonValue nv || !nv.TryGetValue<int>(out int n) || n <= 0) continue;
                if (o["t"] is not JsonValue tv || !tv.TryGetValue<string>(out var stamp)) continue;
                if (!DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var last)) continue;
                s._by[aumid] = new LaunchRecord(n, last);
            }
        }
        catch { }
        return s;
    }

    internal static LaunchStats Read(string path)
    {
        try { return File.Exists(path) ? FromJson(File.ReadAllText(path)) : new LaunchStats(); }
        catch { return new LaunchStats(); }
    }

    internal bool Save(string path, DateTimeOffset now)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, ToJson(now));
            File.Move(tmp, path, overwrite: true);
            return true;
        }
        catch { return false; }
    }
}
