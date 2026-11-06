using Halo.Widgets;

namespace Halo.Tests;

// After a cover lands the widget keeps looking for a while, because a streaming player's first picture is
// often a placeholder that it swaps for the real artwork once the track has downloaded.
public class ArtWatchTests
{
    [Fact]
    public void Watches_closely_for_two_minutes_then_slowly_to_about_fifteen_then_stops()
    {
        Assert.Equal(4_000, MediaWidget.ArtWatchDelay(0));
        Assert.Equal(4_000, MediaWidget.ArtWatchDelay(29));
        Assert.True(MediaWidget.ArtWatchDelay(30) > 4_000);
        int total = 0, i = 0;
        for (; MediaWidget.ArtWatchDelay(i) > 0; i++) total += MediaWidget.ArtWatchDelay(i);
        Assert.InRange(total, 13 * 60_000, 17 * 60_000);
        Assert.Equal(-1, MediaWidget.ArtWatchDelay(i));
        Assert.Equal(-1, MediaWidget.ArtWatchDelay(-1));
    }

    [Fact]
    public void Identical_bytes_are_the_same_art_and_anything_else_is_not()
    {
        Assert.True(MediaWidget.SameArt([1, 2, 3], [1, 2, 3]));
        Assert.False(MediaWidget.SameArt([1, 2, 3], [1, 2, 4]));
        Assert.False(MediaWidget.SameArt([1, 2, 3], [1, 2]));
        Assert.False(MediaWidget.SameArt(null, [1]));   // no cover yet is never "the same"
    }
}
