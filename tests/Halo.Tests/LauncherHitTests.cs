using Halo.Launcher;

namespace Halo.Tests;

// The geometry the mouse depends on. Draw derives row positions from the same constants, so if these two
// ever disagree a row highlights one place and activates another.
public sealed class LauncherHitTests
{
    // Pad 12, FieldH 44 -> header is 0..55; rows start at 62 and step 34.
    private const int FirstRowTop = 62;
    private const int RowH = 34;

    [Theory]
    [InlineData(0, true)]
    [InlineData(55, true)]
    [InlineData(56, false)]
    [InlineData(FirstRowTop, false)]
    [InlineData(-1, false)]
    public void InHeader_IsTheFieldStripAndNothingBelowIt(float y, bool expected)
        => Assert.Equal(expected, LauncherView.InHeader(y));

    [Fact]
    public void HitRow_FindsEachRowAtItsOwnBand()
    {
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(i, LauncherView.HitRow(280, FirstRowTop + i * RowH, 6));
            Assert.Equal(i, LauncherView.HitRow(280, FirstRowTop + i * RowH + RowH - 1, 6));
        }
    }

    [Fact]
    public void HitRow_RefusesTheHeaderAndAnythingPastTheLastRow()
    {
        Assert.Equal(-1, LauncherView.HitRow(280, 30, 6));
        Assert.Equal(-1, LauncherView.HitRow(280, FirstRowTop - 1, 6));
        Assert.Equal(-1, LauncherView.HitRow(280, FirstRowTop + 6 * RowH, 6));
    }

    [Fact]
    public void HitRow_RefusesThePaddingEitherSide()
    {
        Assert.Equal(-1, LauncherView.HitRow(2, FirstRowTop, 6));
        Assert.Equal(-1, LauncherView.HitRow(LauncherView.W - 2, FirstRowTop, 6));
    }

    [Fact]
    public void HitRow_OnAnEmptyListIsAlwaysNothing()
        => Assert.Equal(-1, LauncherView.HitRow(280, FirstRowTop, 0));

    // ---- the ring band -----------------------------------------------------------------------------

    [Fact]
    public void TheBandSitsAfterTheBackRow_NotBeforeIt()
    {
        // Back is the way out of the page and belongs at the top of it. Row 0 must therefore be in the
        // same place whether or not the page carries a dashboard.
        Assert.Equal(LauncherView.RowY(0, band: false), LauncherView.RowY(0, band: true));

        // and everything after it is pushed down by exactly the band
        Assert.Equal(LauncherView.RowY(1, band: false) + LauncherView.BandH,
                     LauncherView.RowY(1, band: true));
    }

    [Fact]
    public void HitRow_FindsBackAboveTheBandAndTheFactsBelowIt()
    {
        Assert.Equal(0, LauncherView.HitRow(280, LauncherView.RowY(0, true) + 2, 6, band: true));
        Assert.Equal(1, LauncherView.HitRow(280, LauncherView.RowY(1, true) + 2, 6, band: true));
        Assert.Equal(5, LauncherView.HitRow(280, LauncherView.RowY(5, true) + 2, 6, band: true));
    }

    [Fact]
    public void HitRow_TreatsTheBandItselfAsNoRow()
    {
        // the band is not a list, and rounding a press there onto a neighbouring row would activate
        // something the pointer was never on
        float bandMid = LauncherView.GaugeTop(true) + LauncherView.GaugeSize / 2f;
        Assert.Equal(-1, LauncherView.HitRow(280, bandMid, 6, band: true));
    }

    [Fact]
    public void HitGauge_SplitsTheBandIntoEqualCells()
    {
        float y = LauncherView.GaugeTop(true) + 10f;
        float cell = (LauncherView.W - 24f) / 5f;
        for (int i = 0; i < 5; i++)
            Assert.Equal(i, LauncherView.HitGauge(12f + cell * i + cell / 2f, y, 5, band: true));
    }

    [Fact]
    public void HitGauge_IsNothingWithoutABand_OrOutsideIt()
    {
        float y = LauncherView.GaugeTop(true) + 10f;
        Assert.Equal(-1, LauncherView.HitGauge(280, y, 5, band: false));
        Assert.Equal(-1, LauncherView.HitGauge(280, y, 0, band: true));
        Assert.Equal(-1, LauncherView.HitGauge(280, LauncherView.RowY(0, true) + 2, 5, band: true));
    }

    // ---- arcs inside a stacked gauge --------------------------------------------------------------

    [Fact]
    public void HitRing_FindsEachArcByItsRadius()
    {
        // the storage circle is three nested rings; pointing at the middle one has to say the middle drive
        float cell = (LauncherView.W - 24f) / 4f;
        float cx = 12f + cell * 2f + cell / 2f;                       // the third gauge of four
        float cy = LauncherView.GaugeTop(true) + LauncherView.GaugeSize / 2f;
        float outer = LauncherView.GaugeSize / 2f - LauncherView.StackBand / 2f;

        for (int i = 0; i < 3; i++)
        {
            float r = outer - i * LauncherView.StackStep;
            Assert.Equal(i, LauncherView.HitRing(cx, cy - r, 2, 4, 3, band: true));
        }
    }

    [Fact]
    public void HitRing_IsNothingForALoneRing_OrTheEmptyMiddle()
    {
        float cell = (LauncherView.W - 24f) / 4f;
        float cx = 12f + cell * 2f + cell / 2f;
        float cy = LauncherView.GaugeTop(true) + LauncherView.GaugeSize / 2f;

        // a single-ring gauge has no arc to single out - the gauge IS the answer
        Assert.Equal(-1, LauncherView.HitRing(cx, cy, 2, 4, 1, band: true));
        // and the hole in the middle is not any of them
        Assert.Equal(-1, LauncherView.HitRing(cx, cy, 2, 4, 3, band: true));
    }

    [Fact]
    public void NoPointIsBothARowAndAGauge()
    {
        // the property that matters: one pixel, one meaning
        for (float y = 0; y < LauncherView.Height(6, band: true); y += 0.5f)
            Assert.False(LauncherView.HitRow(280, y, 6, true) >= 0 && LauncherView.HitGauge(280, y, 5, true) >= 0,
                         $"y={y} was both a row and a gauge");
    }

    [Fact]
    public void TheRowBelowTheBandStartsWhereHitTestingSaysItDoes()
    {
        // the twelve-pixel split: BandPadTop moved where the rings are drawn without moving where the rows
        // below them are hit, so the highlight and the click disagreed
        float justInside = LauncherView.RowY(1, band: true) + 1f;
        Assert.Equal(1, LauncherView.HitRow(280, justInside, 6, band: true));

        float justAbove = LauncherView.RowY(1, band: true) - 1f;
        Assert.Equal(-1, LauncherView.HitRow(280, justAbove, 6, band: true));
    }

    [Fact]
    public void Height_MakesRoomForTheBand()
        => Assert.Equal(LauncherView.Height(6) + LauncherView.BandH, LauncherView.Height(6, band: true));

    [Fact]
    public void HeaderAndRowsNeverOverlap()
    {
        // the property that actually matters: no y is both draggable chrome and a clickable row
        for (float y = 0; y < LauncherView.Height(6); y += 0.5f)
            Assert.False(LauncherView.InHeader(y) && LauncherView.HitRow(280, y, 6) >= 0, $"y={y} was both");
    }
}
