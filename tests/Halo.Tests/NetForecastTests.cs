using System.Collections.Generic;
using System.Linq;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

public class NetForecastTests
{
    private const long GB = 1024L * 1024 * 1024;

    private static List<long> Days(int count, long each) => Enumerable.Repeat(each, count).ToList();

    [Fact]
    public void TooShortAHistoryLearnsNothing()
    {
        // The point of returning null rather than a guess: six days is not a week, and the panel shows how far
        // along the learning is rather than a threshold it cannot stand behind. See the Learning tests below for
        // what stands in the cap's place meanwhile.
        Assert.Null(NetForecast.Learn(Days(NetForecast.MinDays - 1, 3 * GB)));
        Assert.Null(NetForecast.Learn(new List<long>()));
        Assert.Null(NetForecast.Learn(null!));
    }

    [Fact]
    public void SteadyMachineGetsATypicalDayAndACapAboveIt()
    {
        var usual = NetForecast.Learn(Days(14, 4 * GB));
        Assert.NotNull(usual);
        Assert.Equal(4 * GB, usual!.Value.Typical);
        Assert.True(usual.Value.Cap > usual.Value.Typical);
        // No spread at all still cannot make an ordinary day heavy - that is the minimum-room rule.
        Assert.False(usual.Value.IsHeavy(4 * GB));
        Assert.True(usual.Value.IsHeavy(8 * GB));
    }

    [Fact]
    public void OneHugeDayDoesNotDragTheCapUp()
    {
        // The whole reason this is a median and not a mean. Thirteen ordinary days and one 400GB day: a mean
        // would put the cap somewhere near 30GB and never fire again.
        var days = Days(13, 2 * GB);
        days.Add(400 * GB);
        var usual = NetForecast.Learn(days)!.Value;

        Assert.Equal(2 * GB, usual.Typical);
        Assert.True(usual.Cap < 6 * GB);
        Assert.True(usual.IsHeavy(20 * GB));
    }

    [Fact]
    public void AnErraticMachineGetsARoomierCapThanASteadyOne()
    {
        var steady = NetForecast.Learn(Days(14, 5 * GB))!.Value;
        var erratic = NetForecast.Learn(new List<long>
        {
            1 * GB, 9 * GB, 2 * GB, 8 * GB, 5 * GB, 500 * 1024 * 1024, 10 * GB,
            3 * GB, 7 * GB, 5 * GB, 1 * GB, 9 * GB, 4 * GB, 6 * GB,
        })!.Value;

        // Both sit around 5GB a day, but "unusual" means something different on each: 8GB is an ordinary
        // Tuesday on the erratic one and a notable day on the steady one.
        Assert.True(erratic.Cap > steady.Cap);
        Assert.True(steady.IsHeavy(8 * GB));
        Assert.False(erratic.IsHeavy(8 * GB));
    }

    [Fact]
    public void AQuietMachineDoesNotCallAFewHundredMegabytesHeavy()
    {
        // A fortnight of near-nothing has a median of zero and a MAD of zero. Without the floor the cap would
        // be zero too and the first web page of the day would trip it.
        var usual = NetForecast.Learn(Days(14, 0))!.Value;
        Assert.False(usual.IsHeavy(200 * 1024 * 1024));
        Assert.True(usual.IsHeavy(4 * GB));
    }

    [Fact]
    public void IdleDaysCountTowardsNormal()
    {
        // A machine used hard on three days of seven has a lower normal than one used hard on all seven.
        var everyDay = NetForecast.Learn(Days(14, 6 * GB))!.Value;
        var someDays = NetForecast.Learn(new List<long>
        {
            6 * GB, 0, 0, 6 * GB, 0, 0, 6 * GB, 6 * GB, 0, 0, 6 * GB, 0, 0, 6 * GB,
        })!.Value;

        Assert.True(someDays.Typical < everyDay.Typical);
    }

    [Fact]
    public void APartLearnedCapReportsHowFarAlongItIs()
    {
        // The bug this closes: a three-day-old ledger showed "avg 2.1 GB/day" in the cap's row, which is a real
        // number for a different question, so the cap looked unbuilt rather than unfinished.
        var sofar = NetForecast.Learning(3);
        Assert.NotNull(sofar);
        Assert.Equal(3, sofar!.Value.Have);
        Assert.Equal(NetForecast.MinDays, sofar.Value.Need);
    }

    [Fact]
    public void NothingMeasuredIsNotLearning()
    {
        // "0 of 7" on a first run reads as a broken counter, not as a machine nobody has watched yet.
        Assert.Null(NetForecast.Learning(0));
        Assert.Null(NetForecast.Learning(-1));
    }

    [Fact]
    public void TheLearningRowStopsExactlyWhereTheCapStarts()
    {
        // The two states have to hand over cleanly: every day count either learns a cap or reports progress
        // toward one, and never both or neither.
        for (int days = 1; days <= NetForecast.MinDays + 3; days++)
        {
            bool learned = NetForecast.Learn(Days(days, 4 * GB)) is not null;
            bool learning = NetForecast.Learning(days) is not null;
            Assert.True(learned ^ learning, $"{days} days: learned={learned} learning={learning}");
        }
    }

    [Fact]
    public void LearningDoesNotReorderTheCallersDays()
    {
        // The caller passes the chart's own series in more than one place; a sort with a side effect on it
        // would silently reorder the bars.
        var days = new List<long> { 9 * GB, 1 * GB, 5 * GB, 2 * GB, 8 * GB, 3 * GB, 7 * GB };
        var copy = days.ToList();
        NetForecast.Learn(days);
        Assert.Equal(copy, days);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(24)]
    [InlineData(30)]
    [InlineData(90)]
    public void FewerBarsAreFatterBars(int count)
    {
        float fill = NetPanelLayout.FillFraction(count);
        Assert.InRange(fill, 0.54f, 0.82f);
        // Monotonic, and the two ends are the two windows that actually exist.
        if (count > 7) Assert.True(fill < NetPanelLayout.FillFraction(7));
        if (count < 90) Assert.True(fill > NetPanelLayout.FillFraction(90));
    }

    [Fact]
    public void AWeekOfBarsIsVisiblyFatterThanAQuarterOfThem()
    {
        Assert.True(NetPanelLayout.FillFraction(7) - NetPanelLayout.FillFraction(90) > 0.2f);
    }
}
