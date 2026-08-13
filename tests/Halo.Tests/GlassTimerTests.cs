using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// The glass can never be fresher than the frame that asks for it, and the frame cannot be finer than the
// platform timer. Measured with HALO_GLASS_DEBUG, panel open over a playing video: the request interval is
// 16ms and the frame carrying it ran 62ms late, because 16ms does not fit in one 15.6ms tick. So a backdrop
// that is actually moving now holds the fine timer, exactly as an animating widget does.
public class GlassTimerTests
{
    [Fact]
    public void An_open_panel_over_moving_content_asks_for_the_fine_timer()
        => Assert.True(NotchController.GlassWantsFineTimer(true, true, false, 0));

    // The collapsed pill is 220x40 of glass nobody is reading, and it captures at 50ms by design. Holding a
    // machine-wide timer for that is the runaway this policy is deliberately narrow to avoid.
    [Fact]
    public void A_collapsed_pill_does_not()
        => Assert.False(NotchController.GlassWantsFineTimer(false, true, false, 0));

    [Fact]
    public void A_panel_nobody_is_watching_does_not()
        => Assert.False(NotchController.GlassWantsFineTimer(true, false, false, 0));

    // Over the desktop there is no capture at all - Frame skips it - so there is nothing to keep up with.
    [Fact]
    public void Over_the_desktop_there_is_no_backdrop_to_track()
        => Assert.False(NotchController.GlassWantsFineTimer(true, true, true, 0));

    // The capture thread's own count of identical plates. A still window behind an open panel is the common
    // case - a paused video, an editor - and it must not hold the timer just for being open.
    // A frozen backdrop releases the hold, but only after it has really stopped. A sheet grabs about forty
    // times a second, so the threshold is roughly three quarters of a second of identical plates.
    [Fact]
    public void A_backdrop_that_has_stopped_asks_for_nothing()
    {
        Assert.False(NotchController.GlassWantsFineTimer(true, true, false, NotchController.GlassLiveStreak));
        Assert.False(NotchController.GlassWantsFineTimer(true, true, false, 400));
    }

    // Both of these were measured on the live pill and both declined the hold when they should have taken
    // it. A film runs at 24 frames a second and a grab every 31ms lands twice inside the same one
    // constantly, so at a threshold of zero the hold flapped at exactly the rate it was meant to cure; at
    // three it still declined, with the trace reading stale=34 and climbing while the box was open.
    [Fact]
    public void A_few_repeated_plates_are_not_a_backdrop_that_has_stopped()
    {
        Assert.True(NotchController.GlassWantsFineTimer(true, true, false, 1));
        Assert.True(NotchController.GlassWantsFineTimer(true, true, false, 3));
        Assert.True(NotchController.GlassWantsFineTimer(true, true, false, NotchController.GlassLiveStreak - 1));
    }
}
