using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;

namespace Halo.ClaudeCode;

internal static class LiveContext
{
    internal readonly record struct Reading(long Used, long Max);

    internal const int PollMs = 1500;

    private const int TailBytes = 1 << 20;

    public static int Version;

    private sealed class Entry
    {
        public long Length = -1, CheckedAt;
        public Reading? Value;
        public bool Void;
    }

    private static readonly Dictionary<string, Entry> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();

        internal static (Reading? Value, bool Void) Get(string? path)
    {
        if (string.IsNullOrEmpty(path)) return (null, false);
        lock (Gate)
        {
            if (!Cache.TryGetValue(path, out var e))
            {
                if (Cache.Count > 32) Cache.Clear();
                Cache[path] = e = new Entry();
            }
            long now = Environment.TickCount64;
            if (now - e.CheckedAt >= PollMs)
            {
                e.CheckedAt = now;
                try
                {
                    long len = new FileInfo(path).Length;
                    if (len != e.Length)
                    {
                        e.Length = len;
                        var (val, isVoid) = Scan(Tail(path, len));
                        if (val != e.Value || isVoid != e.Void)
                        {
                            e.Value = val; e.Void = isVoid;
                            Interlocked.Increment(ref Version);
                        }
                    }
                }
                catch { }
            }
            return (e.Value, e.Void);
        }
    }

    private static List<string> Tail(string path, long len)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long start = Math.Max(0, len - TailBytes);
        fs.Seek(start, SeekOrigin.Begin);
        var buf = new byte[len - start];
        int got = 0;
        while (got < buf.Length) { int n = fs.Read(buf, got, buf.Length - got); if (n <= 0) break; got += n; }
        var lines = new List<string>(Encoding.UTF8.GetString(buf, 0, got).Split('\n'));
        if (start > 0 && lines.Count > 0) lines.RemoveAt(0);
        return lines;
    }

        internal static (Reading? Value, bool Void) Scan(IReadOnlyList<string> lines)
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            bool summary = line.Contains("\"isCompactSummary\"", StringComparison.Ordinal);
            if (!summary && !line.Contains("\"usage\"", StringComparison.Ordinal)) continue;
            JsonNode? node;
            try { node = JsonNode.Parse(line); } catch { continue; }
            if (summary && node?["isCompactSummary"] is JsonValue v && v.TryGetValue<bool>(out var b) && b)
                return (null, true);
            var usage = node?["message"]?["usage"] ?? node?["usage"];
            if (usage == null) continue;
            long ctx = Get(usage, "input_tokens") + Get(usage, "cache_read_input_tokens")
                + Get(usage, "cache_creation_input_tokens");
            if (ctx <= 0) continue;
            var model = (node?["message"]?["model"] ?? node?["model"])?.GetValue<string>();
            return (new Reading(ctx, Window(model)), false);
        }
        return (null, false);
    }

    internal static long Window(string? model)
    {
        var m = (model ?? "").ToLowerInvariant();
        if (m.Contains("haiku")) return 200_000;
        if (m.Contains("opus") || m.Contains("fable") || m.Contains("sonnet")) return 1_000_000;
        return 200_000;
    }

    private static long Get(JsonNode usage, string key)
    {
        try { return usage[key]?.GetValue<long>() ?? 0; }
        catch { return 0; }
    }
}
