using System;
using System.Text.RegularExpressions;

namespace Halo.Agents;

internal sealed class TurnRecap
{
    private string? _turn;
    private DateTimeOffset _turnFrom;
    private long _endedAt;
    private TimeSpan _took;
    private bool _stopped;

        internal void Observe(string? shown, string? startedAt, DateTimeOffset nowUtc, long nowMs, string? cancelledTurn)
    {
        if (shown == "working" && !string.IsNullOrEmpty(startedAt))
        {
            if (startedAt != _turn &&
                DateTimeOffset.TryParse(startedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t))
            {
                _turn = startedAt;
                _turnFrom = t;
            }
            _endedAt = 0;
            return;
        }
        if ((shown is "idle" or null or "") && _turn is not null)
        {
            _took = nowUtc - _turnFrom;
            _stopped = cancelledTurn is not null && cancelledTurn == _turn;
            _endedAt = nowMs;
            _turn = null;
        }
    }

        internal (string Verb, string Took)? Line(long nowMs)
    {
        if (_endedAt == 0 || nowMs - _endedAt >= Widgets.DoneLinger.Ms) return null;
        return (_stopped ? "stopped" : "done :)", Duration(_took));
    }

    internal static string Duration(TimeSpan d)
    {
        if (d.TotalSeconds < 1) return "";
        return d.TotalMinutes >= 1 ? $"{(int)d.TotalMinutes}m {d.Seconds}s" : $"{d.Seconds}s";
    }

    internal static string Thinking() => "thinking…";

        internal static string? Idle(float ctx, float budget, string budgetLabel, int maxChars = 0)
    {

        string? c = ctx >= 0f ? $"context {ctx * 100:0}%" : null;
        string? b = budget >= 0f ? $"{budgetLabel} {budget * 100:0}%" : null;
        string? both = c is not null && b is not null ? c + ", " + b : c ?? b;
        if (both is null || maxChars <= 0 || both.Length <= maxChars) return both;
        return c is not null ? c + " full" : b;
    }

    private static readonly Regex PermissionTo = new(@"permission to use (?<tool>[A-Za-z_][\w-]*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static string? AskLine(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var m = PermissionTo.Match(message);
        if (!m.Success) return null;
        var tool = m.Groups["tool"].Value;
        if (tool.StartsWith("mcp__", StringComparison.Ordinal))
        {
            var parts = tool.Split("__", StringSplitOptions.RemoveEmptyEntries);
            tool = parts.Length >= 2 ? parts[1] : tool;
        }
        if (tool.Length > 14) tool = tool[..14];
        return "allow " + tool + "?";
    }
}
