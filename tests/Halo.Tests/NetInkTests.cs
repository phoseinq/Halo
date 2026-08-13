using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

public class NetInkTests
{
    private const double KB = 1024, MB = 1024 * 1024;

    [Fact]
    public void IdleIsTheCalmEndAndAFloodIsTheAmberOne()
    {
        var idle = NetWidget.RateInk(0);
        var flood = NetWidget.RateInk(80 * MB);   // past the 50 MB/s ceiling: clamped, not wrapped

        Assert.True(idle.B > idle.R);             // teal: blue over red
        Assert.True(flood.R > flood.B);           // amber: red over blue
        Assert.Equal(255, idle.A);
        Assert.Equal(255, flood.A);
    }

    [Fact]
    public void ATrickleStaysAtTheCalmEnd()
    {
        // 6 KB/s is a machine idling, not using the network, and the agreed ramp calls that pale teal. It sits
        // below the green leg by design - the first version of this test asserted otherwise and was wrong.
        var ink = NetWidget.RateInk(6 * KB);
        Assert.True(ink.B > ink.G);
    }

    [Theory]
    [InlineData(200 * KB)]
    [InlineData(1.4 * MB)]
    [InlineData(11 * MB)]
    public void EverydaySpeedsStayInTheGreenFamily(double rate)
    {
        // The middle of the ramp is where nearly all real traffic sits, and it must not go muddy there: green
        // means green is the strongest channel.
        var ink = NetWidget.RateInk(rate);
        Assert.True(ink.G > ink.R);
        Assert.True(ink.G > ink.B);
    }

    [Fact]
    public void TheRampOnlyEverWarmsAsTheSpeedRises()
    {
        // Monotonic in the direction that carries the meaning - red never falls back as traffic grows - so a
        // speed climbing through the ramp never reads as slowing down.
        double[] ladder = [0, 50 * KB, 500 * KB, 2 * MB, 8 * MB, 20 * MB, 50 * MB];
        for (int i = 1; i < ladder.Length; i++)
            Assert.True(NetWidget.RateInk(ladder[i]).R >= NetWidget.RateInk(ladder[i - 1]).R,
                        $"{ladder[i]} B/s went cooler than {ladder[i - 1]} B/s");
    }

    [Fact]
    public void TheRampIsTiedToTheSameCurveTheRingSweeps()
    {
        // Not a second scale to learn: the colour turns where the arc is, so half a sweep is half a ramp.
        var atHalfSweep = NetWidget.RateInk(RateForFrac(0.5f));
        Assert.Equal(132, atHalfSweep.R);   // exactly DownInk, the ramp's midpoint
        Assert.Equal(231, atHalfSweep.G);
        Assert.Equal(196, atHalfSweep.B);
    }

    // Invert WashFrac by bisection - the curve is monotonic, so this is exact enough to land on the midpoint.
    private static double RateForFrac(float want)
    {
        double lo = 0, hi = 50 * MB;
        for (int i = 0; i < 60; i++)
        {
            double mid = (lo + hi) / 2;
            if (NetWidget.WashFrac(mid) < want) lo = mid; else hi = mid;
        }
        return (lo + hi) / 2;
    }
}
