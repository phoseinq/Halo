using Halo.Launcher;

namespace Halo.Tests;

public sealed class LauncherViewTests
{
    [Fact]
    public void Height_GrowsOneRowAtATime()
    {
        Assert.Equal(LauncherView.Height(3) + LauncherView.RowH, LauncherView.Height(4));
    }

    [Fact]
    public void Height_WithNoRows_IsJustTheField()
    {
        // typing something that matches nothing must leave a field, not a sliver
        Assert.True(LauncherView.Height(0) >= LauncherView.FieldH);
        Assert.True(LauncherView.Height(0) < LauncherView.Height(1));
    }

    [Fact]
    public void SixRows_AreTheTallestItGets()
    {
        // AppMatch caps at six and the menu is six, so nothing may ask for a seventh
        Assert.Equal(LauncherView.Height(6), LauncherView.Height(AppMatch.MaxResults));
        Assert.Equal(6, LauncherState.Menu.Count);
    }

    [Fact]
    public void EveryMenuRow_HasAGlyph()
    {
        // a missing entry here draws as tofu, and the row still looks plausible in a screenshot taken
        // from the wrong distance
        foreach (var row in LauncherState.Menu)
            Assert.False(string.IsNullOrEmpty(LauncherView.MenuGlyph(row.Label)));
    }
}
