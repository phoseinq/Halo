using Halo.ClaudeCode;

namespace Halo.Tests;

// The live context read: newest usage wins, a compact boundary voids everything older, lines without usage
// (tool output, the bulk of a transcript) are skipped.
public class LiveContextTests
{
    private static string U(long input, long read, string model = "claude-opus-5-5")
        => "{\"type\":\"assistant\",\"message\":{\"model\":\"" + model + "\",\"usage\":{\"input_tokens\":" + input
           + ",\"cache_read_input_tokens\":" + read + ",\"cache_creation_input_tokens\":0}}}";

    [Fact]
    public void Newest_usage_line_wins()
    {
        var (v, isVoid) = LiveContext.Scan([U(10, 100), "{\"type\":\"user\",\"toolUseResult\":{}}", U(20, 500), ""]);
        Assert.False(isVoid);
        Assert.Equal(new LiveContext.Reading(520, 1_000_000), v);
    }

    [Fact]
    public void A_compact_with_nothing_after_it_voids_the_reading()
    {
        var (v, isVoid) = LiveContext.Scan([U(10, 900_000), "{\"type\":\"user\",\"isCompactSummary\":true}"]);
        Assert.Null(v);
        Assert.True(isVoid);
    }

    [Fact]
    public void Usage_after_the_compact_is_the_new_context()
    {
        var (v, _) = LiveContext.Scan([U(10, 900_000), "{\"isCompactSummary\":true}", U(5, 40_000, "claude-haiku-5-5")]);
        Assert.Equal(new LiveContext.Reading(40_005, 200_000), v);
    }

    [Fact]
    public void No_usage_and_no_boundary_is_no_reading()
        => Assert.Equal((null, false), LiveContext.Scan(["{\"type\":\"user\"}", "garbage {"]));

    // the end of a turn, read from the transcript rather than waiting on the Stop hook
    [Fact]
    public void The_newest_reply_says_whether_the_turn_ended()
    {
        const string Tool = "{\"type\":\"assistant\",\"timestamp\":\"2026-11-07T10:00:00Z\",\"message\":{\"stop_reason\":\"tool_use\"}}";
        const string End = "{\"type\":\"assistant\",\"timestamp\":\"2026-11-07T10:01:00Z\",\"message\":{\"stop_reason\":\"end_turn\"}}";
        const string Result = "{\"type\":\"user\",\"toolUseResult\":{}}";
        Assert.Null(LiveContext.Ended([End, Tool, Result]));                       // still going
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 10, 1, 0, TimeSpan.Zero), LiveContext.Ended([Tool, Result, End]));
        Assert.Null(LiveContext.Ended(["{\"type\":\"user\"}"]));
    }

    // the account limit ends a turn too, with no Stop hook after it: a synthetic error reply
    [Fact]
    public void A_limit_error_reply_ends_the_turn_and_says_when_it_resets()
    {
        const string Limit = "{\"type\":\"assistant\",\"timestamp\":\"2026-11-07T10:02:00Z\",\"isApiErrorMessage\":true,"
            + "\"error\":\"rate_limit\",\"message\":{\"stop_reason\":\"stop_sequence\",\"content\":[{\"type\":\"text\","
            + "\"text\":\"You've hit your 5-hour limit \u00B7 resets 11:30pm (Asia/Tehran)\"}]}}";
        var end = LiveContext.Finish([Limit]);
        Assert.NotNull(end);
        Assert.Equal("rate_limit", end!.Value.Error);
        Assert.Equal("resets 11:30pm", Halo.Widgets.ClaudeCodeWidget.LimitResetText(end.Value.Text));
    }
}
