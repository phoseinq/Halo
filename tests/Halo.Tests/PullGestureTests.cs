using Halo.Shell;
using Xunit;

namespace Halo.Tests;

using Step = NotchController.PullStep;

public class PullGestureTests
{
    private const int T = 31;   // Sc(26) at the scale this was measured on

    [Fact]
    public void A_press_that_has_not_moved_is_still_the_hold_to_move_gesture()
    {
        Assert.Equal(Step.Hold, NotchController.PullDecide(0, 0, T, false));
        Assert.Equal(Step.Hold, NotchController.PullDecide(8, 8, T, false));
    }

    // The bug, as a test. Every one of these is a frame of an ordinary hand pulling down, and every one of
    // them used to come back Reanchor - which put the anchor under the pointer and reset the distance to
    // zero, so the threshold was unreachable no matter how far you pulled.
    [Theory]
    [InlineData(0, 9)]
    [InlineData(0, 15)]
    [InlineData(2, 20)]
    [InlineData(5, 25)]
    [InlineData(0, 31)]
    public void On_the_way_down_the_anchor_must_stay_put(int dx, int dy)
        => Assert.Equal(Step.Keep, NotchController.PullDecide(dx, dy, T, false));

    [Fact]
    public void Past_the_threshold_it_opens()
    {
        Assert.Equal(Step.Open, NotchController.PullDecide(0, T + 1, T, false));
        Assert.Equal(Step.Open, NotchController.PullDecide(20, 40, T, false));
    }

    // a pull done four pixels at a time must reach Open, which is exactly what the live harness showed it
    // could not do: in 4px steps the window never left its hovered size
    [Fact]
    public void A_slow_pull_gets_there_one_small_step_at_a_time()
    {
        Step last = Step.Hold;
        for (int dy = 0; dy <= 44; dy += 4) last = NotchController.PullDecide(0, dy, T, false);
        Assert.Equal(Step.Open, last);
        // and it never asked to re-anchor on the way, which is the thing that made it impossible
        for (int dy = 0; dy <= 44; dy += 4)
            Assert.NotEqual(Step.Reanchor, NotchController.PullDecide(0, dy, T, false));
    }

    [Fact]
    public void Sideways_and_upward_are_not_pulls_and_start_again_from_there()
    {
        Assert.Equal(Step.Reanchor, NotchController.PullDecide(40, 10, T, false));   // mostly sideways
        Assert.Equal(Step.Reanchor, NotchController.PullDecide(0, -20, T, false));   // upward
        Assert.Equal(Step.Reanchor, NotchController.PullDecide(30, 30, T, false));   // exactly diagonal
    }

    [Fact]
    public void With_the_sheet_already_open_a_further_pull_opens_nothing()
        => Assert.Equal(Step.Keep, NotchController.PullDecide(0, 90, T, historyOpen: true));
}
