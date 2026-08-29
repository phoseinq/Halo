using System.Drawing;
using Halo.Launcher;

namespace Halo.Tests;

public sealed class LauncherPlacementTests
{
    private static readonly Rectangle Screen = new(0, 0, 2560, 1440);

    [Fact]
    public void Default_IsTheMiddleOfTheRightHalf()
    {
        var p = LauncherPlacement.Default(Screen, notchBottom: 48, gap: 56);
        Assert.Equal(1920, p.CenterX);   // middle of 1280..2560
        Assert.Equal(104, p.Top);
    }

    [Fact]
    public void Default_FollowsTheMonitorOrigin()
    {
        var second = new Rectangle(2560, 0, 1920, 1080);
        Assert.Equal(2560 + 1440, LauncherPlacement.Default(second, 48, 56).CenterX);
    }

    [Theory]
    [InlineData(1920, 104)]
    [InlineData(-40, 0)]
    [InlineData(0, -1200)]
    public void Parse_RoundTripsFormat(int cx, int top)
    {
        var got = LauncherPlacement.Parse(LauncherPlacement.Format(cx, top));
        Assert.Equal((cx, top), got);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1920")]
    [InlineData("1920,104,7")]
    [InlineData("left,top")]
    [InlineData("1920;104")]
    public void Parse_RejectsAnythingItCannotUse(string? text)
        => Assert.Null(LauncherPlacement.Parse(text));

    [Fact]
    public void Clamp_KeepsBothEdgesOnScreen()
    {
        Assert.Equal(280, LauncherPlacement.Clamp((-500, 100), Screen, 560, 278).CenterX);
        Assert.Equal(2280, LauncherPlacement.Clamp((9999, 100), Screen, 560, 278).CenterX);
    }

    [Fact]
    public void Clamp_KeepsTheWholeBoxVertically()
    {
        Assert.Equal(0, LauncherPlacement.Clamp((1920, -300), Screen, 560, 278).Top);
        Assert.Equal(1440 - 278, LauncherPlacement.Clamp((1920, 5000), Screen, 560, 278).Top);
    }

    [Fact]
    public void Clamp_LeavesAGoodPositionAlone()
    {
        Assert.Equal((1920, 104), LauncherPlacement.Clamp((1920, 104), Screen, 560, 278));
    }

    [Fact]
    public void Clamp_CentresABoxTooBigForTheMonitor()
    {
        var tiny = new Rectangle(0, 0, 400, 200);
        var p = LauncherPlacement.Clamp((9999, 9999), tiny, 560, 278);
        Assert.Equal(200, p.CenterX);
        Assert.Equal(0, p.Top);
    }

    // an unplugged second monitor is the case this exists for: the saved spot is simply gone
    [Fact]
    public void Clamp_PullsBackAPositionFromAMonitorThatIsNoLongerThere()
    {
        var p = LauncherPlacement.Clamp((3400, 900), Screen, 560, 278);
        Assert.True(p.CenterX + 280 <= Screen.Right);
        Assert.True(p.Top + 278 <= Screen.Bottom);
    }
}
