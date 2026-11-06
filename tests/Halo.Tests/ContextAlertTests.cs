using Halo.Shell;

namespace Halo.Tests;

// The context banner: once per conversation, re-armed only by a real compact, remembered across restarts.
public class ContextAlertTests
{
    private const float At = 0.80f;

    [Fact]
    public void Fires_once_on_crossing_the_line()
    {
        Assert.Equal((true, true), ContextAlert.Decide(false, 0.81f, At));
        Assert.Equal((false, true), ContextAlert.Decide(true, 0.86f, At));
    }

    [Fact]
    public void A_wobble_under_the_line_does_not_rearm_it()
    {
        var (fire, warned) = ContextAlert.Decide(true, 0.78f, At);
        Assert.False(fire);
        Assert.True(warned);                                    // still latched
        Assert.Equal((false, true), ContextAlert.Decide(warned, 0.81f, At));   // no second banner
    }

    [Fact]
    public void A_real_compact_rearms_it()
    {
        var (fire, warned) = ContextAlert.Decide(true, 0.20f, At);
        Assert.False(fire);
        Assert.False(warned);
        Assert.Equal((true, true), ContextAlert.Decide(warned, 0.82f, At));    // full again: warn again
    }

    [Fact]
    public void Below_the_line_and_unwarned_stays_quiet()
        => Assert.Equal((false, false), ContextAlert.Decide(false, 0.5f, At));

    [Fact]
    public void Old_and_malformed_lines_are_dropped_on_load()
    {
        var now = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        long fresh = new DateTimeOffset(now.AddDays(-1)).ToUnixTimeSeconds();
        long stale = new DateTimeOffset(now.AddDays(-9)).ToUnixTimeSeconds();
        var d = ContextAlert.Parse(["a\t" + fresh, "b\t" + stale, "garbage", "\t5"], now);
        Assert.Equal(["a"], d.Keys);
    }
}
