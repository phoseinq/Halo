using Halo.Launcher;

namespace Halo.Tests;

public sealed class AppIndexTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "halo-idx-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void BeforeStart_IsEmptyAndNotReady()
    {
        using var idx = new AppIndex(TempPath(), () => []);

        Assert.Empty(idx.Apps);
        Assert.False(idx.Ready);
    }

    [Fact]
    public void Start_LoadsTheCacheSynchronously()
    {
        // the whole point of the cache: the hotkey must never wait on a shell walk. A scan that throws
        // proves the cache alone answered.
        string path = TempPath();
        try
        {
            AppCache.Save(path, [new AppEntry("Cached App", "cached")]);
            using var idx = new AppIndex(path, () => throw new InvalidOperationException("must not scan"));

            idx.Start();

            Assert.Single(idx.Apps);
            Assert.Equal("Cached App", idx.Apps[0].Name);
            Assert.True(idx.Ready);
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void Refresh_ReplacesTheSnapshotAndWritesTheCache()
    {
        string path = TempPath();
        try
        {
            using var idx = new AppIndex(path, () => [new AppEntry("Scanned", "scanned")], debounceMs: 20);
            var done = new ManualResetEventSlim();
            idx.Changed += () => done.Set();

            idx.RefreshSoon();

            Assert.True(done.Wait(TimeSpan.FromSeconds(10)), "the background scan never reported back");
            Assert.Single(idx.Apps);
            Assert.Equal("Scanned", idx.Apps[0].Name);
            Assert.True(idx.Ready);
            Assert.Single(AppCache.Read(path));
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void AThrowingScan_KeepsThePreviousSnapshot()
    {
        // a failed probe is normal here and must degrade silently - the launcher still opens with
        // whatever it knew last
        string path = TempPath();
        try
        {
            AppCache.Save(path, [new AppEntry("Cached App", "cached")]);
            using var idx = new AppIndex(path, () => throw new InvalidOperationException("boom"),
                debounceMs: 20);
            idx.Start();

            var settled = new ManualResetEventSlim();
            idx.Changed += () => settled.Set();
            idx.RefreshSoon();
            Assert.True(settled.Wait(TimeSpan.FromSeconds(10)), "the failed scan never reported back");

            Assert.Single(idx.Apps);
            Assert.Equal("Cached App", idx.Apps[0].Name);
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [Fact]
    public void OverlappingRefreshes_DoNotStackScans()
    {
        string path = TempPath();
        try
        {
            int scans = 0;
            using var idx = new AppIndex(path, () =>
            {
                Interlocked.Increment(ref scans);
                Thread.Sleep(150);
                return [new AppEntry("One", "one")];
            }, debounceMs: 20);

            for (int i = 0; i < 8; i++) idx.RefreshSoon();
            Thread.Sleep(800);

            // an installer touches the Start Menu many times in a row; eight signals must not become
            // eight shell walks
            Assert.InRange(scans, 1, 2);
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
