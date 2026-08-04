extern alias settingsasm;
using System;
using System.IO;
using System.Threading;
using settingsasm::Halo.Settings;
using Xunit;

namespace Halo.Tests;

// Live's cache is process-global mutable state and one of these tests spawns the real helper, so the class
// is pinned to a collection that does not run in parallel - the same arrangement, and for the same reason,
// as BannerRegistryCollection. Two classes clobbering one environment variable is how a test in this repo
// once wrote to the developer's real notification settings.
[CollectionDefinition("LiveCache", DisableParallelization = true)]
public sealed class LiveCacheCollection { }

// The settings panel fetches its costly readings off the thread that draws it and rebuilds the page when
// they land. That callback is the dangerous part, and this is the test that would have caught it.
[Collection("LiveCache")]
public class LiveWarmTests : IDisposable
{
    // Longer than the 4s ceiling Live.Hooks gives the child it is waiting on. A budget shorter than the
    // production timeout is a test that fails when a cold apphost is slow, which on the public CI runner
    // is the normal case rather than the exception.
    private const int WaitMs = 6000;

    // The negative cases do not need that budget: the failure being pinned is a callback fired INSIDE
    // Warm, on the calling thread, before it returns - so a check the instant Warm returns already catches
    // it. This is the margin on top, for a regression that makes the same mistake asynchronously.
    private const int QuietMs = 500;

    private readonly string? _wasClaudePath;

    // The helper IS beside the test assembly - the project reference copies its apphost - so `query-claude
    // -hooks` really runs and, left alone, reads the developer's own ~/.claude/settings.json. Pointed at a
    // path that does not exist it answers "not installed" without touching anything real. Restored in
    // Dispose, because the variable is process-wide.
    public LiveWarmTests()
    {
        _wasClaudePath = Environment.GetEnvironmentVariable("HALO_CLAUDE_SETTINGS_PATH");
        Environment.SetEnvironmentVariable("HALO_CLAUDE_SETTINGS_PATH",
            Path.Combine(Path.GetTempPath(), "halo-live-warm-tests", Guid.NewGuid().ToString("n"), "settings.json"));
        Live.Forget();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HALO_CLAUDE_SETTINGS_PATH", _wasClaudePath);
        Live.Forget();   // in Dispose, so a failed assertion cannot leak a warm cache into the rest of the run
    }

    // Calling back with nothing fetched is an unbounded loop: the callback rebuilds the page, the rebuild
    // calls Warm again, it again has nothing to do, and it queues another rebuild at DispatcherPriority
    // .Normal - which outranks Input and Render, so the window pegs a core and stops painting. Every page
    // whose rows are all cheap hit it on open. It could not show up in --render-page, which builds one
    // tree and never pumps a dispatcher, so nothing but a test like this one sees it.
    [Fact]
    public void Warming_a_page_with_nothing_costly_never_calls_back()
        => Assert.False(Fired(["about.version", "api.token", "appearance.fpsMeasured"], QuietMs));

    [Fact]
    public void Warming_an_empty_page_never_calls_back()
        => Assert.False(Fired([], QuietMs));

    // The other half of the same property, and the one that makes the rebuild terminate: a page with a
    // costly row warms once, calls back once, and the rebuild that callback triggers finds the cache warm
    // and calls back no further. Without this the loop is merely slower, not gone.
    [Fact]
    public void A_warmed_page_does_not_warm_again()
    {
        Assert.True(Fired(["hooks.claude"], WaitMs));
        // Well inside FreshMs, so this asks the same question the rebuild would ask a frame later.
        Assert.False(Fired(["hooks.claude"], QuietMs));
    }

    // The three that launch a child process, and the reason the cache and the callback exist at all.
    [Fact]
    public void Only_the_three_readings_that_launch_a_process_are_costly()
    {
        Assert.True(Live.Costly("hooks.claude"));
        Assert.True(Live.Costly("hooks.codex"));
        Assert.True(Live.Costly("access.startup"));
        Assert.False(Live.Costly("about.version"));
        Assert.False(Live.Costly("api.token"));
        Assert.False(Live.Costly("reset.everything"));
    }

    // Signalled rather than slept on: the positive case returns the moment the callback lands, and the
    // negative case is the same wait spent proving nothing arrives. Sleeping a fixed 150ms for the negative
    // case made it pass while the regression was present - a callback allowed 4s to arrive has not failed
    // to arrive after 150ms.
    private static bool Fired(string[] keys, int waitMs)
    {
        using var landed = new ManualResetEventSlim(false);
        int calls = 0;
        Live.Warm(keys, () => { Interlocked.Increment(ref calls); landed.Set(); });
        bool signalled = landed.Wait(waitMs);
        Assert.Equal(signalled ? 1 : 0, Volatile.Read(ref calls));
        return signalled;
    }
}
