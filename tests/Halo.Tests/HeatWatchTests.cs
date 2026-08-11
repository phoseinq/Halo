using System;
using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// The threshold IS the feature: an alert about the weather that fires on the ordinary daily cycle is noise,
// and one that never fires is not there. Every case below is a false alarm that had to be excluded, or the
// one real case that has to survive.
public class HeatWatchTests
{
    private static readonly DateTime T0 = new(2026, 8, 11, 12, 0, 0);

    [Fact]
    public void An_abrupt_rise_while_it_is_hot_fires_once()
    {
        var w = new HeatWatch();
        Assert.Null(w.Observe(26, T0));                              // first reading has nothing to compare to
        Assert.Equal(5, w.Observe(31, T0.AddMinutes(60)));           // +5C in an hour
    }

    // The one that would have made this feature unshippable. Dawn to noon in a hot city is 10 degrees at
    // 1-2 degrees an hour, every day of summer.
    [Fact]
    public void The_ordinary_daily_climb_never_fires()
    {
        var w = new HeatWatch();
        int t = 24;
        for (int hour = 0; hour < 8; hour++, t += 2)
            Assert.Null(w.Observe(t, T0.AddHours(hour)));
    }

    // Same swing, spread wide enough that it is the day warming up rather than the weather turning.
    [Fact]
    public void A_rise_that_takes_longer_than_the_window_never_fires()
    {
        var w = new HeatWatch();
        Assert.Null(w.Observe(26, T0));
        Assert.Null(w.Observe(31, T0.AddMinutes(HeatWatch.WindowMinutes + 30)));
    }

    // "It got hot" has to be about heat, or the sentence is a lie.
    [Fact]
    public void A_big_rise_that_is_still_cool_never_fires()
    {
        var w = new HeatWatch();
        Assert.Null(w.Observe(8, T0));
        Assert.Null(w.Observe(16, T0.AddMinutes(45)));
    }

    // Almanac refreshes every half hour, so without a cooldown a hot afternoon re-announces itself on every
    // sample that still clears the window.
    [Fact]
    public void It_does_not_announce_the_same_afternoon_twice()
    {
        var w = new HeatWatch();
        w.Observe(26, T0);
        Assert.NotNull(w.Observe(31, T0.AddMinutes(60)));
        Assert.Null(w.Observe(26, T0.AddMinutes(90)));
        Assert.Null(w.Observe(32, T0.AddMinutes(150)));   // inside the cooldown
    }

    [Fact]
    public void It_can_fire_again_the_next_day()
    {
        var w = new HeatWatch();
        w.Observe(26, T0);
        Assert.NotNull(w.Observe(31, T0.AddMinutes(60)));
        var next = T0.AddDays(1);
        Assert.Null(w.Observe(26, next));
        Assert.Equal(5, w.Observe(31, next.AddMinutes(60)));
    }

    // A machine that slept comes back with a hole in the history. Comparing across it would call the daily
    // cycle abrupt, which is exactly the alarm the window exists to prevent.
    [Fact]
    public void A_gap_from_sleep_does_not_become_an_abrupt_rise()
    {
        var w = new HeatWatch();
        Assert.Null(w.Observe(24, T0));
        Assert.Null(w.Observe(33, T0.AddHours(5)));   // the 24 is long out of the window
    }

    // The swing is what matters, so a window that dipped and came back still counts - and the dip is the
    // reason this compares against the coldest reading rather than the oldest.
    [Fact]
    public void The_swing_is_measured_from_the_coldest_reading_in_the_window()
    {
        var w = new HeatWatch();
        Assert.Null(w.Observe(30, T0));
        Assert.Null(w.Observe(26, T0.AddMinutes(30)));
        Assert.Equal(5, w.Observe(31, T0.AddMinutes(60)));
    }

    // A clock that jumps backwards (a manual change, or a timezone resync) must not leave readings stamped in
    // the future sitting in the window forever.
    [Fact]
    public void Readings_stamped_in_the_future_are_discarded()
    {
        var w = new HeatWatch();
        Assert.Null(w.Observe(26, T0.AddHours(3)));
        Assert.Null(w.Observe(31, T0));
    }
}
