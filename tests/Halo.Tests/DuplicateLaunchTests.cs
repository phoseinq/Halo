using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// Signing in used to open the settings panel: the logon task and Windows' app-restore both start Halo, and
// the loser of the single-instance race took the "you clicked the shortcut" path.
public class DuplicateLaunchTests
{
    [Fact]
    public void A_duplicate_that_lands_with_the_running_one_at_logon_stays_quiet()
        => Assert.False(DuplicateLaunch.ShouldOpenPanel(false, 0.4, null));

    [Fact]
    public void A_click_on_a_pill_that_has_been_up_a_while_still_opens_the_panel()
        => Assert.True(DuplicateLaunch.ShouldOpenPanel(false, 60 * 60, 60 * 60));

    // The affordance is the common case, so an unreadable age must fail towards opening it.
    [Fact]
    public void An_unknown_age_opens_the_panel()
        => Assert.True(DuplicateLaunch.ShouldOpenPanel(false, null, null));

    // Asking for it outranks the timing - the restart the panel performs on apply passes this.
    [Fact]
    public void An_explicit_request_opens_the_panel_however_new_the_other_one_is()
        => Assert.True(DuplicateLaunch.ShouldOpenPanel(true, 0.1, 0.1));

    [Fact]
    public void The_pill_window_boundary_is_exclusive()
    {
        Assert.False(DuplicateLaunch.ShouldOpenPanel(false, DuplicateLaunch.LogonWindowSeconds, null));
        Assert.True(DuplicateLaunch.ShouldOpenPanel(false, DuplicateLaunch.LogonWindowSeconds + 0.1, null));
    }

    [Fact]
    public void The_session_window_boundary_is_exclusive()
    {
        Assert.False(DuplicateLaunch.ShouldOpenPanel(false, null, DuplicateLaunch.SessionWindowSeconds));
        Assert.True(DuplicateLaunch.ShouldOpenPanel(false, null, DuplicateLaunch.SessionWindowSeconds + 0.1));
    }

    // The boot the 45-second window lost, in its own numbers. Sign-in 12:46:38 (explorer), the logon task's
    // Halo 12:46:45, and Windows' restore 12:47:42 - so the winner was 64.0s old, comfortably past a window
    // built for a pair that "lands within moments". The session was 64.1s old at the same instant, and that
    // is the number that cannot drift: both launches hang off the sign-in, not off each other.
    [Fact]
    public void The_sign_in_that_defeated_the_pill_window_is_caught_by_the_session()
        => Assert.False(DuplicateLaunch.ShouldOpenPanel(false, 64.0, 64.1));

    // Either clock alone is enough to refuse. The pair can land together on a session that is already old
    // (explorer restarted mid-session and took the pill's window with it), or apart on a session that is new,
    // which is the case above - so this is an OR over two refusals, not an AND over two permissions.
    [Fact]
    public void A_pair_that_lands_together_is_refused_even_on_an_old_session()
        => Assert.False(DuplicateLaunch.ShouldOpenPanel(false, 0.6, 60 * 60));

    // Explorer restarted three minutes ago and the pill has been up all day: nothing here says sign-in, so
    // the click has to work. This is the cost of the session clock, and it is bounded by the window.
    [Fact]
    public void A_click_after_the_session_window_opens_the_panel()
        => Assert.True(DuplicateLaunch.ShouldOpenPanel(false, 8 * 60 * 60, DuplicateLaunch.SessionWindowSeconds + 1));

    // A negative age is a clock that moved between the two reads, not a launch from the future - the same
    // thing PanelLaunch.Fresh already allows for, and it must not read as "older than the window".
    [Fact]
    public void A_clock_that_moved_backwards_is_still_a_sign_in()
        => Assert.False(DuplicateLaunch.ShouldOpenPanel(false, -2.0, -2.0));
}
