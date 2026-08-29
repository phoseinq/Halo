using Halo.Launcher;

namespace Halo.Tests;

public sealed class LauncherActivateTests
{
    private static LauncherState Fresh()
        => new(() => [], () => true, new LaunchStats(), () => DateTimeOffset.UnixEpoch);

    // The menu has no dead rows left, so the behaviour is exercised where dead rows actually live now:
    // inside a page. System Info is entirely Info rows, none of which do anything when clicked.
    private static LauncherState OnAPageWithDeadRows()
    {
        var s = new LauncherState(() => [], () => true, new LaunchStats(), () => DateTimeOffset.UnixEpoch)
        {
            PageRows = (_, _) =>
            [
                new("Memory", null, false, LauncherRowKind.Info, null, "16 GB"),
                new("Uptime", null, false, LauncherRowKind.Info, null, "3h"),
            ],
        };
        s.GoTo(LauncherState.PageSystem);
        return s;
    }

    [Fact]
    public void Activate_OnADisabledRow_DoesNothingAndFiresNothing()
    {
        var s = OnAPageWithDeadRows();
        int before = s.Selected;
        Assert.Equal("Memory", s.Rows[1].Label);
        Assert.Null(s.Activate(1));
        Assert.Equal(before, s.Selected);      // and it did not drag the selection along either
    }

    [Fact]
    public void Activate_OnADisabledRow_DoesNotFallThroughToWhateverWasSelected()
    {
        // the actual bug: a click lands on a dead row and whatever was selected fired instead. On a page
        // of Info rows the only live row is Back, so a fall-through would have walked the user backwards.
        var s = OnAPageWithDeadRows();
        Assert.Equal(LauncherRowKind.Back, s.Rows[s.Selected].Kind);
        Assert.Null(s.Activate(2));
        Assert.Equal(0, s.Selected);
    }

    [Fact]
    public void Activate_OnAnEnabledRow_ReturnsItAndSelectsIt()
    {
        var s = Fresh();
        var row = s.Activate(5);
        Assert.NotNull(row);
        Assert.Equal(LauncherRowKind.Settings, row!.Kind);
        Assert.Equal(5, s.Selected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(999)]
    public void Activate_OutOfRange_IsNothing(int i) => Assert.Null(Fresh().Activate(i));

    [Fact]
    public void Activate_OnALiveMenuRow_StillWorks()
    {
        var s = Fresh();
        var row = s.Activate(0);
        Assert.NotNull(row);
        Assert.Equal("Quick Actions", row!.Label);
    }
}
