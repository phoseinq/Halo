using Halo.Launcher;

namespace Halo.Tests;

public sealed class LaunchStatsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-23T12:00:00Z");

    [Fact]
    public void FreshLaunch_ScoresItsCount()
    {
        Assert.Equal(4.0, LaunchStats.Score(new LaunchRecord(4, Now), Now), 3);
    }

    [Fact]
    public void OneHalfLife_HalvesTheScore()
    {
        var r = new LaunchRecord(8, Now.AddDays(-30));

        Assert.Equal(4.0, LaunchStats.Score(r, Now), 3);
    }

    [Fact]
    public void AYearOfSilence_ForgetsAHeavilyUsedApp()
    {
        // the whole point of decaying: a list that only counts is a list that never lets go of an app
        // you stopped using, and it sits above the one you use daily for months
        var stale = new LaunchRecord(500, Now.AddDays(-365));
        var daily = new LaunchRecord(20, Now.AddDays(-1));

        Assert.True(LaunchStats.Score(stale, Now) < LaunchStats.Score(daily, Now));
        Assert.True(LaunchStats.Score(stale, Now) < 1.0);
    }

    [Fact]
    public void FutureTimestamp_DoesNotInflateTheScore()
    {
        // a clock change or a hand-edited file must not mint a permanently top-ranked app
        var r = new LaunchRecord(3, Now.AddDays(30));

        Assert.Equal(3.0, LaunchStats.Score(r, Now), 3);
    }

    [Fact]
    public void Record_CountsAndMovesTheClock()
    {
        var s = new LaunchStats();
        s.Record("app", Now.AddDays(-10));
        s.Record("app", Now);

        Assert.Equal(2.0, s.ScoreOf("app", Now), 3);
    }

    [Fact]
    public void UnknownApp_ScoresZero()
    {
        Assert.Equal(0.0, new LaunchStats().ScoreOf("never-seen", Now), 3);
    }

    [Fact]
    public void RoundTrip_KeepsCountsAndTimes()
    {
        var s = new LaunchStats();
        s.Record("a", Now.AddDays(-5));
        s.Record("a", Now.AddDays(-5));
        s.Record("b", Now);

        var back = LaunchStats.FromJson(s.ToJson(Now));

        Assert.Equal(s.ScoreOf("a", Now), back.ScoreOf("a", Now), 3);
        Assert.Equal(s.ScoreOf("b", Now), back.ScoreOf("b", Now), 3);
    }

    [Fact]
    public void Writing_KeepsOnlyTheTopEntries()
    {
        var s = new LaunchStats();
        for (int i = 0; i < LaunchStats.MaxEntries + 50; i++)
            for (int n = 0; n <= i; n++) s.Record("app" + i, Now);

        var back = LaunchStats.FromJson(s.ToJson(Now));

        // the 50 least-used are gone, the most-used is not
        Assert.Equal(0.0, back.ScoreOf("app0", Now), 3);
        Assert.True(back.ScoreOf("app" + (LaunchStats.MaxEntries + 49), Now) > 0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{{{")]
    [InlineData("{\"apps\":5}")]
    public void CorruptJson_ReadsAsEmpty(string? json)
    {
        Assert.Equal(0.0, LaunchStats.FromJson(json).ScoreOf("anything", Now), 3);
    }
}
