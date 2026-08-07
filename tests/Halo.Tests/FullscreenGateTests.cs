using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// Reported: with the pin off, switching between two browser windows and a terminal flashes a black pill
// with nothing in it. Caught with HALO_VISDEBUG=1, which logs the two floats at each hide/show edge, and
// it was two separate defects - one that made the pill hide far more often than it should, and one that
// decided what it looked like when it came back.
public class FullscreenGateTests
{
    // The rect rule is right about what it asks. What it was missing is that the shell puts its own
    // full-screen windows in front during a switch, and those are not the game the rule is for.
    [Theory]
    [InlineData("XamlExplorerHostIslandWindow")]   // seen in the log on nearly every alt-tab
    [InlineData("MultitaskingViewFrame")]
    [InlineData("ForegroundStaging")]              // also in the log, mid-handover
    public void The_shells_own_switching_windows_are_not_fullscreen_apps(string cls)
        => Assert.True(LayeredNotch.IsShellTransientClass(cls));

    // The classes from the same log that ARE real windows have to keep hiding the pill when they really
    // do cover the screen - this is a hide the feature exists for, not one to widen the exemption over.
    [Theory]
    [InlineData("Qt5QWindowIcon")]                 // VLC fullscreen, the case that started all this
    [InlineData("CASCADIA_HOSTING_WINDOW_CLASS")]  // Windows Terminal
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("CabinetWClass")]
    public void A_real_window_is_still_judged_on_its_rect(string cls)
        => Assert.False(LayeredNotch.IsShellTransientClass(cls));

    // A window that stops one pixel short of any edge is not covering the screen. Pinning the boundary
    // because the rule is >=/<= on all four sides and an off-by-one here is a pill that never hides.
    [Theory]
    [InlineData(0, 0, 2560, 1440, true)]
    [InlineData(-8, -8, 2568, 1448, true)]      // a maximised window with the resize border outside
    [InlineData(0, 0, 2560, 1400, false)]       // taskbar visible at the bottom
    [InlineData(0, 1, 2560, 1440, false)]
    [InlineData(1, 0, 2560, 1440, false)]
    [InlineData(0, 0, 2559, 1440, false)]
    public void Covering_the_screen_means_all_four_edges(int l, int t, int r, int b, bool covers)
        => Assert.Equal(covers,
            LayeredNotch.CoversScreen(new Halo.Interop.Win32.RECT
            { left = l, top = t, right = r, bottom = b }, 2560, 1440));

    // The flash itself. The pill is off screen for the whole time it is hidden, so whatever changed while
    // it was gone is not something it can animate - it has to be true already on the frame it returns.
    [Fact]
    public void Coming_back_with_nothing_live_is_already_tucked_away()
    {
        var (empty, shrink) = NotchVisibility.Settled(0);
        Assert.True(empty);
        Assert.Equal(1f, shrink);   // 0f here is the reported bug: a full-size pill holding nothing
    }

    [Fact]
    public void Coming_back_with_a_widget_live_is_already_full_size()
    {
        var (empty, shrink) = NotchVisibility.Settled(2);
        Assert.False(empty);
        Assert.Equal(0f, shrink);   // 1f here is the inverse, also in the log: back as an invisible strip
    }

    // Both ends are resting states, never a value in between - the point of snapping is that there is no
    // half-melted frame left over from an animation nobody was there to see.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void It_never_comes_back_mid_melt(int active)
    {
        var (_, shrink) = NotchVisibility.Settled(active);
        Assert.True(shrink is 0f or 1f, $"came back mid-melt at shrink={shrink}");
    }
}
