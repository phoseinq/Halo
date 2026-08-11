using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// The 1ms platform timer resolution is a machine-wide setting, so Halo only buys it while something is
// actually animating - and gives it back after ten minutes of one continuous hold, because that is no
// longer a person watching. What is pinned here is the way out of that cap.
public class TimerLatchTests
{
    [Fact]
    public void A_hold_past_the_cap_gives_the_resolution_back()
    {
        var r = NotchController.TimerLatch(want: true, raised: true, capped: false,
            raisedAt: 0, now: NotchController.TimerRaiseCapMs + 1, inputEdge: false);

        Assert.False(r.Raise);
        Assert.True(r.Capped);
    }

    // The defect: once capped, only the request going false cleared it, so a pointer parked at top-center
    // over a playing track stayed on the coarse timer and the animation stayed choppy for as long as it sat
    // there. Somebody still moving the mouse is still watching.
    [Fact]
    public void A_pointer_that_moves_re_arms_a_capped_latch()
    {
        var r = NotchController.TimerLatch(want: true, raised: false, capped: true,
            raisedAt: 0, now: NotchController.TimerRaiseCapMs + 5000, inputEdge: true);

        Assert.True(r.Raise);
        Assert.False(r.Capped);
        Assert.Equal(NotchController.TimerRaiseCapMs + 5000, r.RaisedAt);
    }

    [Fact]
    public void Letting_go_clears_the_cap()
    {
        var r = NotchController.TimerLatch(want: false, raised: true, capped: true,
            raisedAt: 0, now: 1000, inputEdge: false);

        Assert.False(r.Raise);
        Assert.False(r.Capped);
    }

    // A raise already in effect keeps its original stamp, or the cap would never be reached: re-stamping
    // every frame is what "held for ten minutes" would measure against.
    [Fact]
    public void An_existing_raise_keeps_the_moment_it_started()
    {
        var r = NotchController.TimerLatch(want: true, raised: true, capped: false,
            raisedAt: 500, now: 900, inputEdge: false);

        Assert.True(r.Raise);
        Assert.Equal(500, r.RaisedAt);
    }

    // A pointer edge while nothing wants the raise must not turn it on: movement is a re-arm, not a request.
    [Fact]
    public void Movement_alone_does_not_raise_anything()
    {
        var r = NotchController.TimerLatch(want: false, raised: false, capped: false,
            raisedAt: 0, now: 1000, inputEdge: true);

        Assert.False(r.Raise);
    }
}
