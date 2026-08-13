using System;
using System.Linq;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The hourly store behind the panel's today window. The daily ledger cannot answer "when today", and a
// 24-bar chart is the only thing in this panel that is about a day rather than about a fortnight.
public class NetHoursTests
{
    private static readonly DateTime Noon = new(2026, 8, 12, 12, 34, 56);

    [Fact]
    public void Everything_inside_one_hour_lands_in_the_same_bucket()
    {
        var hours = new NetHours();
        hours.Add(Noon, NetLink.Wifi, 100, 10);
        hours.Add(Noon.AddMinutes(20), NetLink.Wifi, 50, 5);
        Assert.Equal((150L, 15L), hours.Hour(Noon));
    }

    [Fact]
    public void The_links_are_kept_apart_and_add_up_together()
    {
        var hours = new NetHours();
        hours.Add(Noon, NetLink.Wifi, 100, 10);
        hours.Add(Noon, NetLink.Lan, 7, 3);
        Assert.Equal((100L, 10L), hours.Hour(Noon, NetLink.Wifi));
        Assert.Equal((7L, 3L), hours.Hour(Noon, NetLink.Lan));
        Assert.Equal((107L, 13L), hours.Hour(Noon));
    }

    // A poll that measured nothing must not create a bucket, or a machine left idle overnight would come back
    // with 24 hours of "data" that is really 24 hours of zeros.
    [Fact]
    public void A_poll_with_no_bytes_creates_nothing()
    {
        var hours = new NetHours();
        hours.Add(Noon, NetLink.Wifi, 0, 0);
        Assert.Equal((0L, 0L), hours.Hour(Noon));
        Assert.Empty(hours.Save());
    }

    [Fact]
    public void The_series_is_dense_oldest_first_and_ends_on_the_current_hour()
    {
        var hours = new NetHours();
        hours.Add(Noon, NetLink.Wifi, 500, 50);
        hours.Add(Noon.AddHours(-3), NetLink.Lan, 90, 9);
        var series = hours.Series(Noon, 24);
        Assert.Equal(24, series.Count);
        Assert.Equal(NetHours.HourOf(Noon), series[^1].Hour);
        Assert.Equal(500, series[^1].Down);
        Assert.Equal(90, series[^4].Down);
        // the gap between them is zeros rather than missing rows, which is what keeps a bar per hour
        Assert.Equal(0, series[^2].Down);
        Assert.True(series.Zip(series.Skip(1)).All(p => p.First.Hour < p.Second.Hour));
    }

    [Fact]
    public void Anything_older_than_the_window_is_dropped()
    {
        var hours = new NetHours();
        hours.Add(Noon.AddHours(-40), NetLink.Wifi, 10, 1);
        hours.Add(Noon, NetLink.Wifi, 20, 2);
        hours.Trim(Noon);
        Assert.Equal((0L, 0L), hours.Hour(Noon.AddHours(-40)));
        Assert.Equal((20L, 2L), hours.Hour(Noon));
    }

    [Fact]
    public void A_saved_file_reads_back_the_same()
    {
        var hours = new NetHours();
        hours.Add(Noon, NetLink.Wifi, 1234, 56);
        hours.Add(Noon.AddHours(-2), NetLink.Lan, 78, 9);
        var back = NetHours.Load(hours.Save());
        Assert.Equal((1234L, 56L), back.Hour(Noon, NetLink.Wifi));
        Assert.Equal((78L, 9L), back.Hour(Noon.AddHours(-2), NetLink.Lan));
    }

    // Written every 30 seconds on a machine that gets powered off mid-write, so a torn line has to cost that
    // line and nothing else.
    [Fact]
    public void A_corrupt_line_costs_only_itself()
    {
        var back = NetHours.Load(
        [
            "not a row at all",
            "2026-08-12T12:00:00.0000000\twifi\t100\t10",
            "2026-08-12T13:00:00.0000000\twifi\tNaN\t10",
            "",
        ]);
        Assert.Equal((100L, 10L), back.Hour(new DateTime(2026, 8, 12, 12, 0, 0)));
        Assert.Equal((0L, 0L), back.Hour(new DateTime(2026, 8, 12, 13, 0, 0)));
    }
}
