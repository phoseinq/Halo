using Halo.Shell;

namespace Halo.Tests;

public sealed class LaunchWatchTests
{
    [Fact]
    public void AFreshProcessTakingTheForegroundIsALaunch()
    {
        var watch = new LaunchWatch();
        Assert.True(watch.Fresh(pid: 1234, ageMs: 800));
    }

    [Fact]
    public void SwitchingBackToTheSameAppIsNotOpeningItAgain()
    {
        var watch = new LaunchWatch();
        Assert.True(watch.Fresh(pid: 1234, ageMs: 800));

        // alt-tab away and back, still inside the freshness window - one launch, one beat
        Assert.False(watch.Fresh(pid: 1234, ageMs: 2_000));
    }

    [Fact]
    public void AnAppThatHasBeenOpenAWhileIsNotALaunch()
    {
        var watch = new LaunchWatch();

        // clicking on something you opened this morning is the common case, and it must be silent
        Assert.False(watch.Fresh(pid: 1234, ageMs: LaunchWatch.FreshMs + 1));
    }

    [Fact]
    public void AStartTimeInTheFutureIsNotGuessedAt()
    {
        var watch = new LaunchWatch();

        // Process.StartTime is local time, so a DST edge or a resume-from-sleep can put it ahead of now.
        // Answering "yes" there would fire a beat for every window the user touched.
        Assert.False(watch.Fresh(pid: 1234, ageMs: -5_000));

        // ...and it is not recorded either, so the process still gets its beat once the clock is sane
        Assert.True(watch.Fresh(pid: 1234, ageMs: 500));
    }

    [Fact]
    public void PidZeroIsNotAnApp()
    {
        var watch = new LaunchWatch();
        Assert.False(watch.Fresh(pid: 0, ageMs: 100));
    }

    [Fact]
    public void TheMemoryIsBoundedSoRecycledPidsCannotSwallowALaunchForever()
    {
        var watch = new LaunchWatch();
        Assert.True(watch.Fresh(pid: 7, ageMs: 100));
        Assert.False(watch.Fresh(pid: 7, ageMs: 100));

        // Windows recycles pids. Remembering them forever would mean that after enough churn a real
        // launch of a reused pid is silently dropped, so the oldest entries fall out.
        for (uint i = 100; i < 100 + 64; i++)
            Assert.True(watch.Fresh(i, ageMs: 100));

        Assert.True(watch.Fresh(pid: 7, ageMs: 100));
    }
}
