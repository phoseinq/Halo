using System;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// A chart with no axis is meaningless unless one number on it is named, and the two shapes that would ship
// broken are the ones with no data: a machine that has never been on, and one that has been on for a day.
public class NetWidgetChartTests
{
    private static readonly DateOnly D = new(2026, 8, 11);

    private static (DateOnly, long, long)[] Days(params long[] downs)
    {
        var a = new (DateOnly, long, long)[downs.Length];
        for (int i = 0; i < downs.Length; i++) a[i] = (D.AddDays(i - downs.Length + 1), downs[i], 0);
        return a;
    }

    [Fact]
    public void The_tallest_day_is_full_height_and_the_rest_are_in_proportion()
    {
        var bars = NetWidget.ChartBars(Days(0, 50, 100));

        Assert.Equal(3, bars.Length);
        Assert.Equal(0f, bars[0]);
        Assert.Equal(0.5f, bars[1], 3);
        Assert.Equal(1f, bars[2]);
    }

    // Every day zero: the panel draws a baseline and no bars. Dividing by the peak here is a divide by zero.
    [Fact]
    public void An_empty_history_produces_no_bars_rather_than_a_crash()
    {
        var bars = NetWidget.ChartBars(Days(0, 0, 0, 0));

        Assert.Equal(4, bars.Length);
        Assert.All(bars, b => Assert.Equal(0f, b));
    }

    // One day of data is correct at full height: it IS the peak.
    [Fact]
    public void A_single_day_of_data_fills_its_own_bar()
    {
        var bars = NetWidget.ChartBars(Days(0, 0, 900));

        Assert.Equal(1f, bars[^1]);
    }

    [Fact]
    public void Upload_counts_towards_the_bar_as_well_as_download()
    {
        var series = new (DateOnly, long, long)[] { (D.AddDays(-1), 50, 50), (D, 0, 100) };

        var bars = NetWidget.ChartBars(series);

        Assert.Equal(1f, bars[0]);   // 100 total
        Assert.Equal(1f, bars[1]);   // also 100 total
    }

    [Fact]
    public void An_empty_series_returns_an_empty_array()
        => Assert.Empty(NetWidget.ChartBars(Array.Empty<(DateOnly, long, long)>()));

    [Fact]
    public void The_peak_is_the_tallest_days_combined_bytes()
        => Assert.Equal(150, NetWidget.Peak(new (DateOnly, long, long)[] { (D, 100, 50), (D.AddDays(-1), 120, 0) }));

    [Fact]
    public void The_peak_of_nothing_is_zero()
        => Assert.Equal(0, NetWidget.Peak(Array.Empty<(DateOnly, long, long)>()));
}

public class NetWidgetCollapsedTests
{
    // The wash fills with speed and saturates, so a 400 MB/s link and a 50 MB/s one do not both read as
    // "some traffic" - and neither overflows the pill.
    [Fact]
    public void The_wash_fraction_grows_with_speed_and_saturates()
    {
        Assert.Equal(0f, NetWidget.WashFrac(0));
        Assert.Equal(1f, NetWidget.WashFrac(50L * 1024 * 1024));
        Assert.Equal(1f, NetWidget.WashFrac(400L * 1024 * 1024));
    }

    // The reported bug: the old ceiling was 12 MB/s, so every speed from there up drew one colour. The
    // scale has to still be climbing at 12 and only run out at 50.
    [Fact]
    public void A_fast_download_has_not_used_up_the_whole_scale()
    {
        float at12 = NetWidget.WashFrac(12L * 1024 * 1024);
        float at25 = NetWidget.WashFrac(25L * 1024 * 1024);

        Assert.InRange(at12, 0.6f, 0.85f);
        Assert.True(at25 > at12 + 0.05f, $"12 MB/s {at12:P0} and 25 MB/s {at25:P0} are too close");
    }

    // The pulse gets FASTER with speed, not bigger. A pulse that grew in amplitude would be a flashing pill
    // at exactly the moment there is a number worth reading, and the brief was "low brightness, soft".
    [Fact]
    public void The_pulse_period_shortens_as_speed_rises()
    {
        int slow = NetWidget.PulsePeriodMs(0f);
        int fast = NetWidget.PulsePeriodMs(1f);

        Assert.True(fast < slow, $"expected the period to shorten, got {slow} -> {fast}");
        Assert.Equal(2600, slow);
        Assert.Equal(900, fast);
    }

    [Fact]
    public void A_fraction_outside_zero_to_one_cannot_push_the_period_past_its_ends()
    {
        Assert.Equal(2600, NetWidget.PulsePeriodMs(-1f));
        Assert.Equal(900, NetWidget.PulsePeriodMs(9f));
    }
}

public class NetRingTests
{
    // Real browsing is a rounding error on a 50 MB/s scale, so a linear ring would sit at a few degrees all
    // day and only wake up during a download, which reads as broken rather than as "quiet". The log curve
    // has to give an ordinary page load a visible arc and still leave most of the sweep above it.
    [Fact]
    public void The_ring_spends_its_sweep_where_ordinary_traffic_actually_is()
    {
        float share = 200f * 1024f / (50f * 1024f * 1024f);   // where a linear scale would put it
        float ring = NetWidget.RingFrac(200 * 1024);

        Assert.True(share < 0.01f, $"200 KB/s is {share:P1} of the scale");
        Assert.True(ring > 0.10f, $"but the ring should still show it, got {ring:P1}");
        Assert.True(ring < 0.35f, $"and not spend a third of the arc on it, got {ring:P1}");
    }

    [Fact]
    public void The_ring_still_ends_at_full_and_starts_at_nothing()
    {
        Assert.Equal(0f, NetWidget.RingFrac(0));
        Assert.Equal(1f, NetWidget.RingFrac(50L * 1024 * 1024));
        Assert.Equal(1f, NetWidget.RingFrac(400L * 1024 * 1024));
    }

    [Fact]
    public void The_ring_never_shrinks_as_speed_rises()
    {
        float prev = 0f;
        for (double bps = 0; bps <= 60L * 1024 * 1024; bps += 256 * 1024)
        {
            float f = NetWidget.RingFrac(bps);
            Assert.True(f >= prev, $"went backwards at {bps}");
            prev = f;
        }
    }
}

public class NetLeadInkTests
{
    [Fact]
    public void The_bigger_rate_takes_the_bright_ink()
    {
        Assert.True(NetWidget.DownLeads(leading: false, down: 900_000, up: 10_000));
        Assert.False(NetWidget.DownLeads(leading: true, down: 10_000, up: 900_000));
    }

    // Two rates running close together is exactly when both are worth reading, and a plain comparison traded
    // the highlight back and forth every frame there.
    [Fact]
    public void A_rate_within_the_deadband_cannot_steal_the_ink()
    {
        Assert.True(NetWidget.DownLeads(leading: true, down: 100_000, up: 110_000));
        Assert.False(NetWidget.DownLeads(leading: false, down: 110_000, up: 100_000));
    }

    [Fact]
    public void Past_the_deadband_it_does_swap()
    {
        Assert.False(NetWidget.DownLeads(leading: true, down: 100_000, up: 120_000));
        Assert.True(NetWidget.DownLeads(leading: false, down: 120_000, up: 100_000));
    }

    [Fact]
    public void Two_idle_directions_leave_the_holder_alone()
        => Assert.True(NetWidget.DownLeads(leading: true, down: 0, up: 0));
}

// Upload keeps its own latch and its own constant, even though the bar is the same megabit as down today.
// It was half a megabit, on the reasoning that sending is rarely something the machine does by itself - which
// is false on a machine with a VPN, whose tunnel idles at 30-95 KB/s up and crossed that bar every few
// seconds, holding the row on permanently.
public class NetUploadLatchTests
{
    [Fact]
    public void A_steady_upload_floor_plus_a_small_download_does_not_reach_the_download_bar()
    {
        // The machine this was reported from: a VPN holding ~50 KB/s up as its floor while nothing much is
        // being downloaded. The download latch used to be fed down+up, so the two added to 72 KB and then any
        // burst tipped the SUM past the 1 Mbit bar - the row appeared claiming a megabit nobody downloaded.
        const double up = 50 * 1024;    // the VPN's floor, under the upload bar
        const double down = 22 * 1024;  // nowhere near the 125 KB download bar

        var (onSum, _) = NetRate.Latch(false, down + up, 0, 1.0);   // what it used to ask
        var (onDown, _) = NetRate.Latch(false, down, 0, 1.0);       // what it asks now
        var (onUp, _) = NetRate.Latch(false, up, 0, 1.0, NetRate.UpOnBytesPerSec);

        Assert.False(onDown);
        Assert.False(onUp);
        // and the sum is not over the bar either at this exact pair - the bug was that it CLIMBED there on a
        // burst that left the download itself far short, so the guard is that download alone is what is asked
        Assert.False(onSum);

        // one burst: the download climbs to 80 KB - still well under the 125 KB bar, about two thirds of a
        // megabit - but added to the upload floor it clears 125 and the row used to appear
        const double burst = 80 * 1024;
        Assert.True(NetRate.Latch(false, burst + up, 0, 1.0).On);
        Assert.False(NetRate.Latch(false, burst, 0, 1.0).On);
    }

    [Fact]
    public void An_idle_vpn_tunnels_upload_floor_stays_under_the_bar()
    {
        // Measured on the machine this was reported from, over two sampling runs: the tunnel idles between 30
        // and 95 KB/s up with nothing being sent. The whole point of raising the bar is that its CEILING is
        // under it, so pin the worst case rather than the average.
        const double idleCeiling = 95 * 1024;
        Assert.False(NetRate.Latch(false, idleCeiling, 0, 1.0, NetRate.UpOnBytesPerSec).On);
    }

    [Fact]
    public void A_megabit_down_turns_it_on_and_so_does_a_megabit_up()
    {
        // The rule, stated once: the two questions are independent and each keeps its own bar.
        Assert.True(NetRate.Latch(false, NetRate.OnBytesPerSec, 0, 1.0).On);
        Assert.True(NetRate.Latch(false, NetRate.UpOnBytesPerSec, 0, 1.0, NetRate.UpOnBytesPerSec).On);
        Assert.False(NetRate.Latch(false, NetRate.OnBytesPerSec - 1, 0, 1.0).On);
        Assert.False(NetRate.Latch(false, NetRate.UpOnBytesPerSec - 1, 0, 1.0, NetRate.UpOnBytesPerSec).On);
    }

    [Fact]
    public void The_upload_bar_turns_it_on_by_itself()
    {
        var (on, _) = NetRate.Latch(false, NetRate.UpOnBytesPerSec, 0, 1.0,
                                    NetRate.UpOnBytesPerSec);

        Assert.True(on);
    }

    [Fact]
    public void A_full_bars_worth_of_upload_leaves_the_download_latch_alone()
    {
        // This used to assert that the same rate coming DOWN was not enough, which only said "the up bar is
        // lower than the down one" - and stopped being true the moment both became a megabit. What has to
        // hold whatever the two numbers are is that they are separate questions: an upload at its own bar
        // turns the row on through the upload latch and contributes NOTHING to the download one. Feeding
        // down+up to the download latch is exactly the bug that shipped.
        double idleDown = 0;

        Assert.True(NetRate.Latch(false, NetRate.UpOnBytesPerSec, 0, 1.0, NetRate.UpOnBytesPerSec).On);
        Assert.False(NetRate.Latch(false, idleDown, 0, 1.0).On);
    }

    [Fact]
    public void It_holds_through_a_brief_gap_rather_than_flickering()
    {
        var (on, quiet) = NetRate.Latch(true, 1_000, 0, 1.0,
                                        NetRate.UpOnBytesPerSec);

        Assert.True(on);
        Assert.Equal(1.0, quiet);
    }

    [Fact]
    public void It_goes_off_once_the_hold_has_run_out()
    {
        var (on, _) = NetRate.Latch(true, 1_000, NetRate.OffHoldSeconds, 1.0,
                                    NetRate.UpOnBytesPerSec);

        Assert.False(on);
    }

    [Fact]
    public void Every_window_and_split_pair_asks_for_its_own_frame()
    {
        // Version is what tells the controller a redraw is owed, and on an idle link it is the ONLY thing that
        // does - the rates are 0 and nothing else moves. The split's flag was 0x4000, which is also the window's
        // own contribution at index 4, so Quarter+closed and Hour+open hashed the same and one of those chip
        // clicks drew nothing at all. Every pair, so a sixth window colliding fails here.
        var meter = new NetMeter();
        meter.Seed(0, 0, NetLink.Wifi, new NetLedger());
        var seen = new Dictionary<int, string>();
        foreach (NetWindow w in System.Enum.GetValues<NetWindow>())
            foreach (bool split in new[] { false, true })
            {
                var widget = new NetWidget(meter) { Window = w, SplitOpen = split };
                string label = $"{w}+{(split ? "split" : "closed")}";
                Assert.False(seen.TryGetValue(widget.Version, out var clash),
                             $"{label} hashes the same as {clash}");
                seen[widget.Version] = label;
            }
    }

    [Fact]
    public void A_pinned_panel_keeps_asking_for_frames_after_the_link_goes_quiet()
    {
        // IsActive and Animating have to agree about the pin, or a pinned panel stays on screen with its
        // reveals frozen: the eases only advance on frames, and with no traffic nothing else requests one.
        var meter = new NetMeter();
        meter.Seed(0, 0, NetLink.Wifi, new NetLedger());
        var widget = new NetWidget(meter) { Pinned = true };

        Assert.True(widget.IsActive);
        Assert.True(widget.Animating);
    }
}
