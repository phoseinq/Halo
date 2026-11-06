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
}
