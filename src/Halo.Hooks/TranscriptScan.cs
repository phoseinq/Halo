using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Halo.Hooks;

internal static class TranscriptScan
{
    internal readonly record struct Reading(long Latest, long Turn, string? Model, bool Compacted);

    internal static Reading Read(IReadOnlyList<string> lines, DateTimeOffset started)
    {
        long latest = 0, turn = 0;
        string? model = null;

        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            JsonNode? node;
            try { node = JsonNode.Parse(lines[i]); } catch { continue; }

            if (IsCompactSummary(node)) return new Reading(latest, turn, model, latest == 0);

            var usage = node?["message"]?["usage"] ?? node?["usage"];
            if (usage == null) continue;

            long ctx = Get(usage, "input_tokens") + Get(usage, "cache_read_input_tokens")
                + Get(usage, "cache_creation_input_tokens");
            if (latest == 0 && ctx > 0)
            {
                latest = ctx;
                model = (node?["message"]?["model"] ?? node?["model"])?.GetValue<string>();
            }

            if (started == DateTimeOffset.MinValue) { if (latest > 0) break; continue; }
            var tsNode = node?["timestamp"]?.GetValue<string>();
            if (!DateTimeOffset.TryParse(tsNode, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var ts)) continue;
            if (ts < started) { if (latest > 0) break; continue; }
            turn += Get(usage, "input_tokens") + Get(usage, "cache_creation_input_tokens")
                + Get(usage, "output_tokens");
        }

        return new Reading(latest, turn, model, false);
    }

    private static bool IsCompactSummary(JsonNode? node)
    {
        try { return node?["isCompactSummary"] is JsonValue v && v.TryGetValue<bool>(out var b) && b; }
        catch { return false; }
    }

    private static long Get(JsonNode usage, string key)
    {
        try { return usage[key]?.GetValue<long>() ?? 0; }
        catch { return 0; }
    }
}
