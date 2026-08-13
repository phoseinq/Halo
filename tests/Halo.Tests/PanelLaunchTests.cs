using System;
using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// The settings panel must never put itself on screen. It came back at every sign-in, which is Windows'
// "automatically save my restartable apps" replaying what was open at sign-out, and not any decision of Halo's -
// launch-debug.txt proved that by having no `panel` line beside the launch.
//
// The stamp is the only signal a restore cannot forge. It replays the original command line verbatim, so a flag
// or a nonce comes back with it; what it cannot do is refresh a file that Halo.App writes the instant before it
// starts the panel. So these pin two things: what counts as a fresh stamp, and that nothing else counts at all.
public class PanelLaunchTests
{
    [Fact]
    public void Only_a_stamped_launch_shows_a_window()
    {
        Assert.True(PanelLaunch.ShouldShow(requested: true));
        Assert.False(PanelLaunch.ShouldShow(requested: false));
    }

    [Fact]
    public void Freshness_spans_the_launch_and_tolerates_a_clock_nudge()
    {
        Assert.True(PanelLaunch.Fresh(0.0));
        Assert.True(PanelLaunch.Fresh(PanelLaunch.RequestFreshSeconds));
        Assert.False(PanelLaunch.Fresh(PanelLaunch.RequestFreshSeconds + 0.1));
        // the time service resynced twice during the sign-in that produced the bug, so a stamp can arrive
        // reading very slightly in the future
        Assert.True(PanelLaunch.Fresh(-1.0));
        Assert.False(PanelLaunch.Fresh(-3600.0));
        // a stamp from a previous session, sitting there because the panel never got to consume it
        Assert.False(PanelLaunch.Fresh(86_400.0));
    }

    // Invariant and greppable, for the same reason LaunchLog's lines are: this ships to a machine running fa-IR,
    // where a current-culture age comes back with a Persian decimal separator. The age is no longer part of the
    // decision, only of the record - it separates a sign-in restore from something starting the panel at three
    // in the afternoon, which would be a different bug.
    [Fact]
    public void The_refusal_line_is_greppable_and_culture_invariant()
    {
        var now = new DateTime(2026, 8, 13, 0, 42, 7, 543);
        Assert.Equal("2026-08-13 00:42:07.543 panel refused=unrequested sessionAge=8.4s\r\n",
            PanelLaunch.RefusedLine(now, 8.42));
        Assert.Equal("2026-08-13 00:42:07.543 panel refused=unrequested sessionAge=?\r\n",
            PanelLaunch.RefusedLine(now, null));
    }
}
