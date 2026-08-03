using Halo.Shell;

namespace Halo.Tests;

public class ToggleCueTests
{
    [Fact]
    public void An_unfired_cue_shows_nothing()
    {
        var cue = default(ToggleCue);
        Assert.Equal(0f, cue.Alpha(10_000));
        Assert.False(cue.Alive(10_000));
    }

    [Fact]
    public void It_is_fully_up_through_the_hold()
    {
        var cue = new ToggleCue(1000);
        Assert.Equal(1f, cue.Alpha(1000 + ToggleCue.RiseMs));
        Assert.Equal(1f, cue.Alpha(1000 + ToggleCue.RiseMs + ToggleCue.HoldMs - 1));
    }

    [Fact]
    public void It_rises_then_falls()
    {
        var cue = new ToggleCue(1000);
        float mid = cue.Alpha(1000 + ToggleCue.RiseMs / 2);
        Assert.InRange(mid, 0.01f, 0.99f);
        float falling = cue.Alpha(1000 + ToggleCue.RiseMs + ToggleCue.HoldMs + ToggleCue.FallMs / 2);
        Assert.InRange(falling, 0.01f, 0.99f);
    }

    // The whole point of Alive: a finished cue must stop asking for frames, or the pill never drops back
    // to its idle rate and a one-off toast costs battery for as long as the app runs.
    [Fact]
    public void It_ends_and_stops_asking_for_frames()
    {
        var cue = new ToggleCue(1000);
        Assert.True(cue.Alive(1000 + ToggleCue.TotalMs - 1));
        Assert.False(cue.Alive(1000 + ToggleCue.TotalMs));
        Assert.Equal(0f, cue.Alpha(1000 + ToggleCue.TotalMs));
    }

    // TickCount64 does not go backwards, but a cue read on the same tick it was fired must not flash.
    [Fact]
    public void It_is_zero_at_the_instant_it_fires()
    {
        var cue = new ToggleCue(1000);
        Assert.Equal(0f, cue.Alpha(1000));
    }

    [Fact]
    public void A_time_before_it_fired_shows_nothing()
    {
        var cue = new ToggleCue(5000);
        Assert.Equal(0f, cue.Alpha(4000));
    }

    // It has to stay readable for over a second - the text is the entire reason it exists. Fired at a
    // real tick, not 0: zero is the "never fired" sentinel that makes default(ToggleCue) draw nothing.
    [Fact]
    public void It_stays_readable_for_at_least_a_second()
    {
        var cue = new ToggleCue(9_000_000);
        Assert.Equal(1f, cue.Alpha(9_001_000));
    }
}
