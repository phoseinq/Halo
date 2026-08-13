using System;
using System.Linq;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The per-minute store behind the panel's hour window. It is memory-only and the only one of the three stores
// with no file behind it, which is exactly why nothing noticed it was never being trimmed: an unbounded
// dictionary costs nothing on disk and shows up only as an app that gets slower the longer it stays up.
public class NetMinutesTests
{
    private static readonly DateTime Noon = new(2026, 8, 12, 12, 34, 56);

    private const long MB = 1024L * 1024;

    [Fact]
    public void A_minute_holds_what_was_added_to_it_whichever_link_carried_it()
    {
        var m = new NetMinutes();
        m.Add(Noon, NetLink.Wifi, 4 * MB, 1 * MB);
        m.Add(Noon, NetLink.Lan, 6 * MB, 2 * MB);

        var both = m.Minute(NetMinutes.MinuteOf(Noon));
        Assert.Equal(10 * MB, both.Down);
        Assert.Equal(3 * MB, both.Up);
        Assert.Equal(6 * MB, m.Minute(NetMinutes.MinuteOf(Noon), NetLink.Lan).Down);
    }

    [Fact]
    public void Total_adds_up_the_window_and_stops_at_its_edges()
    {
        // The figure the hour window's total row prints. It used to print the DAY's total instead, because the
        // panel had no way to total minutes at all and fell through to the ledger.
        var m = new NetMinutes();
        for (int i = 0; i < 10; i++) m.Add(Noon.AddMinutes(-i), NetLink.Wifi, MB, 0);
        // and one that is outside a five-minute window looking back from noon
        var total = m.Total(Noon, 5);
        Assert.Equal(5 * MB, total.Down);
        Assert.Equal(10 * MB, m.Total(Noon, 60).Down);
    }

    [Fact]
    public void Total_can_be_asked_for_one_link()
    {
        var m = new NetMinutes();
        m.Add(Noon, NetLink.Wifi, 3 * MB, 0);
        m.Add(Noon, NetLink.Lan, 7 * MB, 0);
        Assert.Equal(7 * MB, m.Total(Noon, 60, NetLink.Lan).Down);
        Assert.Equal(10 * MB, m.Total(Noon, 60).Down);
    }

    [Fact]
    public void Series_is_oldest_first_zero_filled_and_lands_each_minute_in_its_own_slot()
    {
        // Series was rewritten from sixty full scans of the dictionary to one pass into slots, so the thing
        // worth pinning is that the rewrite puts every bucket back exactly where the old one did.
        var m = new NetMinutes();
        m.Add(Noon, NetLink.Wifi, 5 * MB, 0);                 // newest
        m.Add(Noon.AddMinutes(-3), NetLink.Lan, 2 * MB, 0);   // three back

        var series = m.Series(Noon, 5);
        Assert.Equal(5, series.Count);
        Assert.Equal(NetMinutes.MinuteOf(Noon), series[^1].Minute);
        Assert.True(series[0].Minute < series[^1].Minute);
        Assert.Equal(5 * MB, series[^1].Down);
        Assert.Equal(2 * MB, series[^4].Down);
        Assert.Equal(0, series[^2].Down);
        Assert.Equal(7 * MB, series.Sum(s => s.Down));
    }

    [Fact]
    public void Trim_drops_what_has_aged_out_and_keeps_the_window_the_chart_draws()
    {
        // Nothing called this. NetMeter.Poll now does, once a minute, and the store stops growing for as long
        // as the app is up - which on a machine left running for a week was ~20k entries that every lookup
        // walked.
        var m = new NetMinutes();
        for (int i = 0; i < NetMinutes.Keep + 40; i++)
            m.Add(Noon.AddMinutes(-i), NetLink.Wifi, MB, 0);

        m.Trim(Noon);

        // everything the 60-bar chart can draw is still there
        Assert.Equal(60 * MB, m.Total(Noon, 60).Down);
        // and the far side of the kept window is gone rather than merely hidden
        Assert.Equal(0, m.Minute(NetMinutes.MinuteOf(Noon.AddMinutes(-(NetMinutes.Keep + 5)))).Down);
    }

    [Fact]
    public void Series_agrees_with_asking_each_minute_on_its_own()
    {
        // The one-pass rewrite against the definition it replaced, over a store with both links and a gap.
        var m = new NetMinutes();
        m.Add(Noon, NetLink.Wifi, 3 * MB, 1 * MB);
        m.Add(Noon.AddMinutes(-1), NetLink.Lan, 4 * MB, 2 * MB);
        m.Add(Noon.AddMinutes(-1), NetLink.Wifi, 1 * MB, 0);
        m.Add(Noon.AddMinutes(-7), NetLink.Wifi, 9 * MB, 3 * MB);

        foreach (var (minute, down, up) in m.Series(Noon, 10))
        {
            var one = m.Minute(minute);
            Assert.Equal(one.Down, down);
            Assert.Equal(one.Up, up);
        }
    }
}
