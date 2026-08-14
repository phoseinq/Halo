using System;
using System.Collections.Generic;
using System.Linq;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// Windows only offers a CUMULATIVE byte counter per adapter and resets it on reboot, so every "today / week /
// month" figure the panel shows is accumulated by Halo and written down. That makes this the half of the
// feature where a bug is permanent - a bad delta does not flicker, it lands in the daily total and stays - and
// none of it is visible on a pill that cannot be screenshotted.
public class NetRateTests
{
    [Fact]
    public void An_ordinary_delta_is_the_difference()
        => Assert.Equal(500, NetRate.Delta(1_000, 1_500));

    // The one that matters. The adapter was disabled and re-enabled, or the driver reloaded: taking `current`
    // would post the adapter's whole lifetime total as one interval, which on a machine up for a week is tens
    // of gigabytes appearing in a single second - and it would be in the day's total for good.
    [Fact]
    public void A_counter_that_went_backwards_contributes_nothing()
        => Assert.Equal(0, NetRate.Delta(9_000_000_000, 4_096));

    [Fact]
    public void An_unchanged_counter_is_zero()
        => Assert.Equal(0, NetRate.Delta(1_234, 1_234));

    // Two polls in the same millisecond turn a handful of bytes into a spectacular rate, and the pill would
    // show gigabytes per second for one frame.
    [Fact]
    public void A_zero_length_interval_does_not_divide()
    {
        Assert.Equal(0, NetRate.PerSecond(4_096, 0));
        Assert.Equal(0, NetRate.PerSecond(4_096, 0.001));
    }

    [Fact]
    public void A_normal_interval_divides()
        => Assert.Equal(2_048, NetRate.PerSecond(1_024, 0.5));

    [Fact]
    public void The_first_sample_is_taken_whole_rather_than_eased_up_from_zero()
        => Assert.Equal(1_000, NetRate.Smooth(0, 1_000));

    [Fact]
    public void Smoothing_moves_part_of_the_way_towards_the_sample()
        => Assert.Equal(1_350, NetRate.Smooth(1_000, 2_000, 0.35), 3);

    // One threshold makes the pill flicker in and out on every sample while a download sits near the line.
    [Fact]
    public void It_comes_on_at_one_megabyte()
    {
        Assert.True(NetRate.Latch(on: false, rate: NetRate.OnBytesPerSec, quietFor: 0, dt: 1).On);
        Assert.False(NetRate.Latch(on: false, rate: NetRate.OnBytesPerSec - 1, quietFor: 0, dt: 1).On);
    }

    [Fact]
    public void It_stays_on_between_the_two_marks()
        => Assert.True(NetRate.Latch(on: true, rate: NetRate.OnBytesPerSec, quietFor: 99, dt: 1).On);

    // A download that stalls for a second is still a download; the panel vanishing mid-glance is the thing
    // being avoided.
    [Fact]
    public void A_brief_stall_does_not_switch_it_off()
    {
        var r = NetRate.Latch(on: true, rate: 0, quietFor: 0, dt: 1);
        Assert.True(r.On);
        Assert.Equal(1, r.QuietFor);
    }

    [Fact]
    public void It_goes_off_once_it_has_been_quiet_for_the_hold()
        => Assert.False(NetRate.Latch(on: true, rate: 0, quietFor: NetRate.OffHoldSeconds, dt: 0.1).On);

    [Fact]
    public void Traffic_resets_the_quiet_timer()
        => Assert.Equal(0, NetRate.Latch(on: true, rate: NetRate.OnBytesPerSec, quietFor: 3, dt: 1).QuietFor);

    // The pill redraws this ten times a second: a string that changes width makes the row shuffle sideways.
    [Theory]
    [InlineData(0, "0 KB/s")]
    [InlineData(900, "1 KB/s")]
    [InlineData(1024 * 400, "400 KB/s")]
    [InlineData(1024 * 1024 * 3 / 2, "1.5 MB/s")]
    [InlineData(1024L * 1024 * 125, "125 MB/s")]
    public void Rates_are_formatted_at_a_fixed_width(double bps, string expected)
        => Assert.Equal(expected, NetRate.Format(bps));

    [Theory]
    [InlineData(0, "0 KB")]
    [InlineData(1024L * 900, "900 KB")]
    [InlineData(1024L * 1024 * 5, "5.0 MB")]
    [InlineData(1024L * 1024 * 1024 * 3, "3.00 GB")]
    public void Totals_are_formatted_for_the_panel(long bytes, string expected)
        => Assert.Equal(expected, NetRate.Size(bytes));
}

public class NetLedgerTests
{
    private static readonly DateOnly Today = new(2026, 8, 11);

    [Fact]
    public void Todays_bytes_add_up_across_polls()
    {
        var l = new NetLedger();
        l.Add(Today, NetLink.Wifi, 100, 10);
        l.Add(Today, NetLink.Wifi, 50, 5);

        Assert.Equal((150L, 15L), l.Today(Today));
    }

    // The user asked for LAN and Wi-Fi apart, which is the whole reason the link is part of the key.
    [Fact]
    public void The_two_kinds_of_link_are_counted_apart_and_together()
    {
        var l = new NetLedger();
        l.Add(Today, NetLink.Wifi, 100, 10);
        l.Add(Today, NetLink.Lan, 400, 40);

        Assert.Equal((100L, 10L), l.Today(Today, NetLink.Wifi));
        Assert.Equal((400L, 40L), l.Today(Today, NetLink.Lan));
        Assert.Equal((500L, 50L), l.Today(Today));
    }

    [Fact]
    public void Yesterdays_bytes_are_not_todays()
    {
        var l = new NetLedger();
        l.Add(Today.AddDays(-1), NetLink.Wifi, 999, 99);

        Assert.Equal((0L, 0L), l.Today(Today));
        Assert.Equal((999L, 99L), l.Week(Today));
    }

    // Rolling windows, not calendar ones: a calendar week reads as a collapse to nothing every Monday and
    // raises the question of which day the week starts on, which differs by country.
    [Fact]
    public void The_week_is_the_last_seven_days_inclusive()
    {
        var l = new NetLedger();
        l.Add(Today.AddDays(-6), NetLink.Lan, 1, 0);
        l.Add(Today.AddDays(-7), NetLink.Lan, 1000, 0);

        Assert.Equal(1L, l.Week(Today).Down);
    }

    [Fact]
    public void The_month_is_the_last_thirty_days_inclusive()
    {
        var l = new NetLedger();
        l.Add(Today.AddDays(-29), NetLink.Lan, 1, 0);
        l.Add(Today.AddDays(-30), NetLink.Lan, 1000, 0);

        Assert.Equal(1L, l.Month(Today).Down);
    }

    // A machine that was off for a week must not draw as a continuous line.
    [Fact]
    public void The_chart_series_is_dense_and_oldest_first()
    {
        var l = new NetLedger();
        l.Add(Today, NetLink.Wifi, 7, 0);
        l.Add(Today.AddDays(-4), NetLink.Wifi, 3, 0);

        var s = l.Series(Today, 5);

        Assert.Equal(5, s.Count);
        Assert.Equal(Today.AddDays(-4), s[0].Day);
        Assert.Equal(3L, s[0].Down);
        Assert.Equal(0L, s[1].Down);      // the machine was off
        Assert.Equal(Today, s[^1].Day);
        Assert.Equal(7L, s[^1].Down);
    }

    [Fact]
    public void A_saved_ledger_reads_back_identical()
    {
        var l = new NetLedger();
        l.Add(Today, NetLink.Wifi, 1_234_567_890, 42);
        l.Add(Today.AddDays(-3), NetLink.Lan, 5, 6);

        var back = NetLedger.Load(l.Save().ToList());

        Assert.Equal((1_234_567_890L, 42L), back.Today(Today));
        Assert.Equal((1_234_567_895L, 48L), back.Week(Today));
    }

    // A half-written line from a machine that lost power, or a file from a build that wrote a different shape,
    // must cost that line and nothing else. Usage history is not worth a crash.
    [Fact]
    public void A_corrupt_line_costs_only_itself()
    {
        var back = NetLedger.Load(new[]
        {
            "2026-08-11\tWifi\t100\t10",
            "this is not a row",
            "2026-08-11\tWifi\t",
            "not-a-date\tWifi\t1\t1",
            "2026-08-11\tCarrierPigeon\t1\t1",
            "2026-08-11\tLan\tnope\t1",
            "2026-08-11\tLan\t7\t7",
        });

        Assert.Equal((107L, 17L), back.Today(Today));
    }

    [Fact]
    public void Trimming_drops_days_past_the_retention_window()
    {
        var l = new NetLedger();
        l.Add(Today, NetLink.Wifi, 1, 1);
        l.Add(Today.AddDays(-(NetLedger.KeepDays - 1)), NetLink.Wifi, 1, 1);
        l.Add(Today.AddDays(-NetLedger.KeepDays), NetLink.Wifi, 1000, 1000);

        l.Trim(Today);

        Assert.Equal(2L, l.Total(DateOnly.MinValue, DateOnly.MaxValue).Down);
    }

    [Fact]
    public void A_negative_delta_is_never_stored()
    {
        var l = new NetLedger();
        l.Add(Today, NetLink.Wifi, -5, -5);

        Assert.Equal((0L, 0L), l.Today(Today));
    }
}

// Windows binds an NDIS lightweight filter instance per filter driver, and every one of them enumerates as
// its own NetworkInterface: its own Id, its own name ("<adapter>-QoS Packet Scheduler-0000"), and the SAME
// counters as the card underneath. The machine this was found on showed one wifi card eight times, so the
// ledger was recording every byte eight times over. They all carry the adapter's MAC, which is what tells
// them apart.
public class NetAdapterIdentityTests
{
    private static NetMeter.Sample S(string key, long rx, long tx) => new(key, NetLink.Wifi, rx, tx);

    [Fact]
    public void One_card_seen_through_eight_filter_drivers_is_counted_once()
    {
        var seen = new List<NetMeter.Sample>();
        for (int i = 0; i < 8; i++) seen.Add(S("A4B1C1D2E3F4", 1000, 500));

        var kept = NetMeter.Dedupe(seen);

        Assert.Single(kept);
        Assert.Equal(1000, kept[0].Rx);
    }

    [Fact]
    public void Two_real_adapters_are_both_kept()
    {
        var kept = NetMeter.Dedupe([S("AAAAAAAAAAAA", 10, 1), S("BBBBBBBBBBBB", 20, 2)]);

        Assert.Equal(2, kept.Count);
    }

    // The MAC is the identity, so which of the eight the enumeration happens to return first cannot change
    // what _last is keyed by - otherwise a reordered enumeration reseeds the baseline and drops an interval.
    [Fact]
    public void The_key_is_the_mac_not_the_interface_id()
    {
        Assert.Equal("A4B1C1D2E3F4", NetMeter.AdapterKey("A4B1C1D2E3F4", "{GUID-of-a-filter-instance}"));
    }

    // A virtual adapter can report no hardware address at all. Falling back to the Id keeps it counted once
    // rather than collapsing every such adapter into a single all-zero bucket.
    [Theory]
    [InlineData("")]
    [InlineData("000000000000")]
    public void An_adapter_with_no_hardware_address_falls_back_to_its_id(string mac)
    {
        Assert.Equal("{iface-2}", NetMeter.AdapterKey(mac, "{iface-2}"));
    }
}
