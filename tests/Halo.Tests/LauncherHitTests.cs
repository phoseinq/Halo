using Halo.Launcher;

namespace Halo.Tests;

// The geometry the mouse depends on. Draw reads the same frame, so if these ever disagree a row highlights one
// place and activates another.
public sealed class LauncherHitTests
{
    // Pad 12, FieldH 44 -> header is 0..55; rows start at 62 and step 34.
    private const int FirstRowTop = 62;
    private const int RowH = 34;

    private static LauncherLayout.Frame List(int rows, bool back = false, float band = 0f)
        => LauncherLayout.Compute(rows, back, band);

    [Theory]
    [InlineData(0, true)]
    [InlineData(55, true)]
    [InlineData(56, false)]
    [InlineData(FirstRowTop, false)]
    [InlineData(-1, false)]
    public void InHeader_IsTheFieldStripAndNothingBelowIt(float y, bool expected)
        => Assert.Equal(expected, LauncherView.InHeader(y));

    [Fact]
    public void Hit_FindsEachRowAtItsOwnBand()
    {
        var f = List(6);
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(i, LauncherLayout.Hit(f, 280, FirstRowTop + i * RowH));
            Assert.Equal(i, LauncherLayout.Hit(f, 280, FirstRowTop + i * RowH + RowH - 1));
        }
    }

    [Fact]
    public void Hit_RefusesTheHeaderAndAnythingPastTheLastRow()
    {
        var f = List(6);
        Assert.Equal(-1, LauncherLayout.Hit(f, 280, 30));
        Assert.Equal(-1, LauncherLayout.Hit(f, 280, FirstRowTop - 1));
        Assert.Equal(-1, LauncherLayout.Hit(f, 280, FirstRowTop + 6 * RowH));
    }

    [Fact]
    public void Hit_RefusesThePaddingEitherSide()
    {
        var f = List(6);
        Assert.Equal(-1, LauncherLayout.Hit(f, 2, FirstRowTop));
        Assert.Equal(-1, LauncherLayout.Hit(f, LauncherView.W - 2, FirstRowTop));
    }

    [Fact]
    public void Back_LivesInTheField_AndCostsTheListNoRow()
    {
        var f = List(4, back: true);
        var c = LauncherLayout.BackCell;
        Assert.Equal(0, LauncherLayout.Hit(f, c.X + c.Width / 2f, c.Y + c.Height / 2f));
        Assert.True(LauncherView.InHeader(c.Y));
        Assert.Equal(1, LauncherLayout.Hit(f, 280, FirstRowTop));
    }

    [Fact]
    public void TheBandPushesTheListDown_AndIsNotARow()
    {
        var f = List(3, back: true, band: LauncherView.LangBarH);
        Assert.Equal(-1, LauncherLayout.Hit(f, 280, FirstRowTop + 2));
        Assert.Equal(1, LauncherLayout.Hit(f, 280, FirstRowTop + LauncherView.LangBarH + 2));
    }

    [Fact]
    public void HitBand_SplitsTheBandIntoEqualCells()
    {
        var f = List(7, back: true, band: LauncherView.BandH);
        float y = f.Band.Y + 10f, cell = f.Band.Width / 5f;
        for (int i = 0; i < 5; i++)
            Assert.Equal(i, LauncherLayout.HitBand(f, f.Band.X + cell * i + cell / 2f, y, 5));
        Assert.Equal(-1, LauncherLayout.HitBand(f, 280, f.Band.Y - 1f, 5));
        Assert.Equal(-1, LauncherLayout.HitBand(f, 280, y, 0));
    }

    [Fact]
    public void NoRowSitsInTheBand()
    {
        var f = List(7, back: true, band: LauncherView.BandH);
        foreach (var c in f.Cells) Assert.False(c.IntersectsWith(f.Band));
    }

    [Fact]
    public void HeaderAndRowsNeverOverlap()
    {
        var f = List(6);
        for (float y = 0; y < f.Height; y += 0.5f)
            Assert.False(LauncherView.InHeader(y) && LauncherLayout.Hit(f, 280, y) >= 0, $"y={y} was both");
    }
}
