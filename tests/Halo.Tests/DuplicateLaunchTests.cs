using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// Signing in used to open the settings panel: the logon task and Windows' app-restore both start Halo, and
// the loser of the single-instance race took the "you clicked the shortcut" path.
public class DuplicateLaunchTests
{
    [Fact]
    public void A_duplicate_that_lands_with_the_running_one_at_logon_stays_quiet()
        => Assert.False(DuplicateLaunch.ShouldOpenPanel(false, 0.4));

    [Fact]
    public void A_click_on_a_pill_that_has_been_up_a_while_still_opens_the_panel()
        => Assert.True(DuplicateLaunch.ShouldOpenPanel(false, 60 * 60));

    // The affordance is the common case, so an unreadable age must fail towards opening it.
    [Fact]
    public void An_unknown_age_opens_the_panel()
        => Assert.True(DuplicateLaunch.ShouldOpenPanel(false, null));

    // Asking for it outranks the timing - the restart the panel performs on apply passes this.
    [Fact]
    public void An_explicit_request_opens_the_panel_however_new_the_other_one_is()
        => Assert.True(DuplicateLaunch.ShouldOpenPanel(true, 0.1));

    [Fact]
    public void The_window_boundary_is_exclusive()
    {
        Assert.False(DuplicateLaunch.ShouldOpenPanel(false, DuplicateLaunch.LogonWindowSeconds));
        Assert.True(DuplicateLaunch.ShouldOpenPanel(false, DuplicateLaunch.LogonWindowSeconds + 0.1));
    }
}
