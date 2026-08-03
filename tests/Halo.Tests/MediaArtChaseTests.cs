using Halo.Widgets;

namespace Halo.Tests;

// The art chase is one chain per widget, so what the chain does when the world moves under it IS the
// feature. The case that was a live bug: a track starting while another track's chase sleeps in its
// delay — the new track's ChaseArt call bounces off _chasing, so if the sleeping chain gives up on the
// epoch mismatch instead of restarting, nobody ever retries the new track's cover and it wears the app
// logo to the end. Restart, never give up, is the contract these pin down.
public class MediaArtChaseTests
{
    [Fact]
    public void A_track_change_with_no_art_restarts_the_chase_instead_of_ending_it()
        => Assert.Equal(MediaWidget.ArtChase.Restart,
                        MediaWidget.Decide(sessionAlive: true, trackMoved: true, hasArt: false));

    [Fact]
    public void Art_landing_ends_the_chase()
        => Assert.Equal(MediaWidget.ArtChase.Done,
                        MediaWidget.Decide(sessionAlive: true, trackMoved: false, hasArt: true));

    [Fact]
    public void A_new_track_that_already_has_art_needs_no_chase()
        => Assert.Equal(MediaWidget.ArtChase.Done,
                        MediaWidget.Decide(sessionAlive: true, trackMoved: true, hasArt: true));

    [Fact]
    public void A_dead_session_ends_the_chase_whatever_else_is_true()
    {
        Assert.Equal(MediaWidget.ArtChase.Done, MediaWidget.Decide(false, false, false));
        Assert.Equal(MediaWidget.ArtChase.Done, MediaWidget.Decide(false, true, false));
    }

    [Fact]
    public void Same_track_still_missing_art_keeps_fetching()
        => Assert.Equal(MediaWidget.ArtChase.Fetch,
                        MediaWidget.Decide(sessionAlive: true, trackMoved: false, hasArt: false));

    // The schedule is the other half of "never give up". Reported live: a spotify track played to the end
    // wearing the app logo, and seeking in the player produced the cover at once - so the cover existed and
    // the old six-step, fifteen-second schedule had simply stopped asking before it landed.
    [Fact]
    public void The_ramp_widens_so_the_first_seconds_are_cheap()
    {
        int prev = 0;
        for (int i = 0; i < 6; i++)
        {
            int delay = MediaWidget.ArtDelay(i);
            Assert.True(delay > prev, $"attempt {i} did not widen: {delay} after {prev}");
            prev = delay;
        }
    }

    [Fact]
    public void After_the_ramp_it_keeps_asking_on_a_steady_tail()
    {
        Assert.Equal(10_000, MediaWidget.ArtDelay(6));
        Assert.Equal(10_000, MediaWidget.ArtDelay(20));
    }

    // The point of the change, stated as a number: the old schedule spanned 15.5s and the reported track
    // was still logo-only well past that. Anything under a normal track's length would reproduce the bug.
    [Fact]
    public void The_schedule_covers_a_whole_normal_track()
    {
        long total = 0;
        for (int i = 0; MediaWidget.ArtDelay(i) > 0; i++) total += MediaWidget.ArtDelay(i);
        Assert.True(total > 4 * 60 * 1000, $"schedule only spans {total}ms");
    }

    // ...but it does stop. A session parked on a track that genuinely has no cover must not poll forever.
    [Fact]
    public void The_tail_is_capped_rather_than_endless()
    {
        Assert.Equal(-1, MediaWidget.ArtDelay(6 + 30));
        Assert.Equal(-1, MediaWidget.ArtDelay(10_000));
    }
}
