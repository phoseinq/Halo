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
        // The numbers moved when the ramp was repainted at real saturation - the midpoint is DownInk, and
        // by half a sweep the brightness ramp is already at full, so this is DownInk exactly.
        var atHalfSweep = NetWidget.RateInk(RateForFrac(0.5f));
        Assert.Equal(96, atHalfSweep.R);    // exactly DownInk, the ramp's midpoint
        Assert.Equal(230, atHalfSweep.G);
        Assert.Equal(128, atHalfSweep.B);
    }

    // ---- the rim, and which figure matches it -------------------------------------------------------
    //
    // The rim used to be a hand-written slate-to-teal lerp, so load showed only as it getting lighter -
    // reported as "it is only turquoise and its brightness goes up and down, I want the colour to change
    // too". It also read the DOWNLOAD rate, so an upload-heavy minute left it at idle slate while the row
    // plainly showed megabytes going out.

    private static float Hue(System.Drawing.Color c) => c.GetHue();

    [Fact]
    public void TheRimChangesHueWithLoad_NotJustBrightness()
    {
        var idle = NetWidget.EdgeInk(0f);
        var busy = NetWidget.EdgeInk(0.5f);
        var flat = NetWidget.EdgeInk(1f);

        Assert.True(MathF.Abs(Hue(idle) - Hue(flat)) > 60f,
                    $"idle {Hue(idle):0} and full {Hue(flat):0} are the same hue - the rim is still one colour");
        Assert.True(MathF.Abs(Hue(busy) - Hue(flat)) > 30f,
                    $"mid {Hue(busy):0} and full {Hue(flat):0} barely differ");
    }

    [Fact]
    public void TheRimStaysDarkEvenFlatOut()
    {
        // A rim reads against the desktop behind the pill, not against the pill: a bright one glows like a
        // strip light along the top of the screen. That is the constraint the hue ramp had to respect.
        for (float t = 0f; t <= 1f; t += 0.1f)
        {
            var c = NetWidget.EdgeInk(t);
            int brightest = Math.Max(c.R, Math.Max(c.G, c.B));
            Assert.True(brightest <= 130, $"the rim reaches {brightest} at {t:0.0} - that is a light strip");
        }
    }

    [Fact]
    public void AQuietLinkIsDimAndABusyOneIsNot()
    {
        // "When the speed is low you do not need to make it bright." Level is carried in the VALUE as well
        // as the hue, so a glance reads it without reading the digits - and so the vivid stops that replaced
        // the pastel ones cannot make an idle machine louder than the old ramp was.
        int Bright(System.Drawing.Color c) => Math.Max(c.R, Math.Max(c.G, c.B));

        int idle = Bright(NetWidget.RateInkAt(0f));
        int busy = Bright(NetWidget.RateInkAt(0.5f));
        int flat = Bright(NetWidget.RateInkAt(1f));

        Assert.True(idle < busy - 40, $"idle {idle} is nearly as bright as busy {busy}");
        Assert.True(busy <= flat, $"busy {busy} is brighter than flat out {flat}");
    }

    [Fact]
    public void TheRimReadsWhicheverDirectionIsBusier()
    {
        Assert.Equal(900d, NetWidget.Louder(900d, 12d, downLeads: true));
        Assert.Equal(900d, NetWidget.Louder(12d, 900d, downLeads: false));
    }

    [Fact]
    public void OnlyTheLeadingFigureMatchesTheRim()
    {
        // The follower keeps its own direction colour, so the pair is still two readings. Ramping both would
        // spend the hue saying what the two arrows already say.
        double fast = 8 * MB;
        Assert.Equal(NetWidget.RateInk(fast), NetWidget.FigureInk(fast, leads: true, rising: false));
        Assert.NotEqual(NetWidget.RateInk(fast), NetWidget.FigureInk(fast, leads: false, rising: true));
    }

    [Fact]
    public void TheFollowerIsPlainWhite()
    {
        // Corrected on the monitor: "only the bigger one takes colour, the other stays the faint white it
        // was". Two versions before this gave the follower its own hue and then dimmed it - both were
        // solving collisions that only existed because it was coloured at all. White has no hue to clash
        // with the ramp and no brightness to balance against it; DrawRate's own lead factor makes it faint.
        foreach (bool rising in new[] { true, false })
        {
            var c = NetWidget.FigureInk(8 * MB, leads: false, rising: rising);
            Assert.Equal(c.R, c.G);
            Assert.Equal(c.G, c.B);
        }
    }

    [Fact]
    public void EitherDirectionCanBeTheOneThatMatches()
    {
        // the whole ask: "let just one of them match the bar - upload or download, whichever is bigger"
        double fast = 8 * MB;
        Assert.Equal(NetWidget.RateInk(fast), NetWidget.FigureInk(fast, leads: true, rising: true));
        Assert.Equal(NetWidget.RateInk(fast), NetWidget.FigureInk(fast, leads: true, rising: false));
    }

    [Fact]
    public void TheFigureAndTheRimWalkTheSameRamp()
    {
        // Not two ramps that happen to look alike: the rim deepens the SAME colour, so at full load the
        // leading figure and the rim have to agree on hue or the "match" is a coincidence.
        var figure = NetWidget.RateInkAt(1f);
        var rim = NetWidget.EdgeInk(1f);
        Assert.True(MathF.Abs(Hue(figure) - Hue(rim)) < 12f,
                    $"figure {Hue(figure):0} and rim {Hue(rim):0} are not the same ramp");
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
