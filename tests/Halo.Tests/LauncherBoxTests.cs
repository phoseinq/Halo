using Halo.Launcher;

namespace Halo.Tests;

// Regression cover for "the letters slide while you type". The box animated its size with an
// exponential lerp, which approaches its target and never arrives, so the window sat forever at a size
// a fraction of a pixel off its natural one and every repaint recomputed a slightly different layout.
public sealed class LauncherBoxTests
{
    private const float FrameDt = 0.008f;      // the controller's 8ms tick
    private const float Speed = 1f / 0.40f;    // OpenSeconds

    [Fact]
    public void Step_ArrivesExactly_NotAsymptotically()
    {
        float t = 0f;
        for (int i = 0; i < 400; i++) t = LauncherBox.Step(t, 1f, FrameDt * Speed);
        Assert.Equal(1f, t);   // exact: an off-by-a-hair t is what moved the type
    }

    [Fact]
    public void Step_ArrivesExactly_WhenClosing()
    {
        float t = 1f;
        for (int i = 0; i < 400; i++) t = LauncherBox.Step(t, 0f, FrameDt * Speed);
        Assert.Equal(0f, t);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(8)]
    public void SettledBox_IsExactlyTheViewsNaturalSize(int rows)
    {
        var (w, h) = LauncherBox.BoxSize(1f, rows);
        Assert.Equal(LauncherView.W, w);
        Assert.Equal(LauncherView.Height(rows), h);
    }

    [Fact]
    public void OnceOpen_TheSizeStopsChanging()
    {
        float t = 0f;
        var seen = new List<(int, int)>();
        for (int i = 0; i < 400; i++)
        {
            t = LauncherBox.Step(t, 1f, FrameDt * Speed);
            if (i >= 380) seen.Add(LauncherBox.BoxSize(t, 6));
        }
        Assert.All(seen, s => Assert.Equal(seen[0], s));
    }

    [Fact]
    public void Closing_ReachesTheHidePointInAFractionOfASecond()
    {
        // The tail was the bug: an exponential decay run all the way to 0.001 spent about a second below
        // the point where nothing is drawn any more, holding the screen dim the whole time.
        const float ContentGone = 0.16f / 0.40f;
        float t = 1f, seconds = 0f;
        while (t > ContentGone && seconds < 5f)
        {
            t = LauncherBox.Step(t, 0f, FrameDt * (1f / 0.16f));
            seconds += FrameDt;
        }

        Assert.True(t <= ContentGone, "never reached the hide point");
        Assert.True(seconds < 0.25f, $"took {seconds:0.000}s to become invisible");
    }

    [Fact]
    public void GrowingBox_NeverExceedsItsSettledWidth()
    {
        // The width is what the content is laid out against, so a wider-than-final frame pushes every
        // row out and back. This is the test that caught EaseOutBack's overshoot doing exactly that.
        int settled = LauncherBox.BoxSize(1f, 6).W;
        for (float t = 0f; t <= 1f; t += 0.01f)
            Assert.True(LauncherBox.BoxSize(t, 6).W <= settled, $"t={t} was wider than settled");
    }
}
