using System.Drawing;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The panel's geometry, which used to live as constants scattered through DrawContent and could only be
// checked by looking at a PNG. Every rectangle the redesign draws comes from here, so a bar leaving the
// track or a chip leaving the panel is a failing test rather than something noticed later on screen.
public class NetPanelLayoutTests
{
    // One Fact rather than a Theory over the enum: an internal type cannot be a parameter of a public test
    // method, and making the panel's window enum public to please the test runner would be the wrong way round.
    [Fact]
    public void Each_window_names_its_own_span()
    {
        // minutes, hours, then days: the unit changes with the window, and the number is how many BUCKETS the
        // chart draws rather than how long the span is in any one unit
        Assert.Equal(60, NetPanelLayout.Span(NetWindow.Hour));
        Assert.Equal(24, NetPanelLayout.Span(NetWindow.Today));
        Assert.Equal(7, NetPanelLayout.Span(NetWindow.Week));
        Assert.Equal(30, NetPanelLayout.Span(NetWindow.Month));
        // the ledger keeps 90 days, so this is the widest window that is not partly fiction
        Assert.Equal(NetLedger.KeepDays, NetPanelLayout.Span(NetWindow.Quarter));
    }

    // The three tests below are the guard the compiler will not give. A switch with a `_ =>` arm cannot warn
    // about a new enum member, and a switch without one warns about casts instead (CS8524) - which would have to
    // be silenced with a throw on the render path. So the exhaustiveness lives here: every one of these walks
    // Enum.GetValues, so a sixth window fails a test the moment it is added rather than quietly inheriting
    // whatever the catch-all arm returns. That inheritance is exactly what shipped Hour and Quarter showing
    // today's bytes.
    [Fact]
    public void Every_window_maps_to_a_day_span_and_only_the_day_windows_are_longer_than_one()
    {
        foreach (NetWindow w in System.Enum.GetValues<NetWindow>())
        {
            int days = NetPanelLayout.WindowDays(w);
            Assert.True(days >= 1, $"{w} mapped to {days} days");
            // Hour and Today are both "one day of the ledger" - the hour window totals from the minute store
            // and never uses this, and today is a single day by definition.
            bool sub = w is NetWindow.Hour or NetWindow.Today;
            Assert.True(sub ? days == 1 : days > 1, $"{w} mapped to {days} days");
        }
        Assert.Equal(1, NetPanelLayout.WindowDays(NetWindow.Today));
        Assert.Equal(7, NetPanelLayout.WindowDays(NetWindow.Week));
        Assert.Equal(30, NetPanelLayout.WindowDays(NetWindow.Month));
        Assert.Equal(NetLedger.KeepDays, NetPanelLayout.WindowDays(NetWindow.Quarter));
    }

    [Fact]
    public void Every_window_names_a_unit_that_matches_one_of_its_bars()
    {
        // The unit belongs to the BAR, not to the window: the hour window draws minutes, so its average is per
        // minute. It used to read `Today ? perHour : perDay`, which called the hour window's per-minute average
        // "per day" from the moment that window existed.
        Assert.Equal("net.avgMinute", NetPanelLayout.UnitKey(NetWindow.Hour));
        Assert.Equal("net.avgHour", NetPanelLayout.UnitKey(NetWindow.Today));
        foreach (NetWindow w in System.Enum.GetValues<NetWindow>())
            Assert.False(string.IsNullOrEmpty(NetPanelLayout.UnitKey(w)), $"{w} has no unit");
    }

    [Fact]
    public void The_windows_span_in_bars_agrees_with_its_span_in_days()
    {
        // The two tables answer different questions - bars drawn versus days totalled - and the day windows are
        // the ones where they must agree. Hour and Today are where they legitimately differ, and conflating
        // them is what made a total in bars-units land in a row labelled in days.
        foreach (NetWindow w in System.Enum.GetValues<NetWindow>())
        {
            if (w is NetWindow.Hour or NetWindow.Today) continue;
            Assert.Equal(NetPanelLayout.Span(w), NetPanelLayout.WindowDays(w));
        }
    }

    [Fact]
    public void The_chips_sit_inside_the_panel_and_do_not_overlap()
    {
        var chips = NetPanelLayout.Chips(560f);
        Assert.Equal(5, chips.Length);
        Assert.True(chips[0].Left > 0f);
        Assert.Equal(NetPanelLayout.ChipRight, chips[^1].Right, 1f);
        // and they clear the glass by more than the chart does, which is the margin the redesign was told
        // to take back
        Assert.True(chips[^1].Right <= 560f - NetPanelLayout.Pad);
        for (int i = 1; i < chips.Length; i++)
            Assert.True(chips[i - 1].Right <= chips[i].Left);
        // The chips may sit a little left of the chart's own edge - the gutter between the two is empty. What
        // must hold is that they never reach the info column, and this caught it happening for real: widening
        // the column to fit both rates on one line pushed ColRight past the leftmost chip.
        Assert.True(chips[0].Left > NetPanelLayout.ColRight);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
    public void Bars_fill_the_track_without_leaving_it(int count)
    {
        var heights = new float[count];
        for (int i = 0; i < count; i++) heights[i] = 1f;
        var bars = NetPanelLayout.Bars(560f, 100f, 198f, count, heights);
        Assert.Equal(count, bars.Length);
        // The chart starts at ChartLeft now, not at the panel's margin: everything left of it is the info
        // column. And the NEWEST bucket - the last of the array - is the one at the left end of the track.
        Assert.True(bars[0].Left >= NetPanelLayout.ChartLeft);
        Assert.True(bars[^1].Right <= NetPanelLayout.TrackRight);
        for (int i = 1; i < bars.Length; i++)
        {
            Assert.Equal(bars[0].Width, bars[i].Width, 0.01f);
            Assert.True(bars[i - 1].Right <= bars[i].Left + 0.01f);   // later in time = further right
        }
        // The ceiling is 34 rather than 30 since the fill fraction started following the bucket count: a week
        // is seven bars and they are meant to be slabs. The floor is what keeps ninety of them apart.
        Assert.InRange(bars[0].Width, 3f, 34f);
        // Gaps never close: a bar that fills its whole cell reads as one solid block, not as a chart.
        float pitch = (NetPanelLayout.TrackRight - NetPanelLayout.ChartLeft) / count;
        Assert.True(bars[0].Width < pitch);
    }

    // A day with nothing still occupies its cell. Packing only the drawn bars would slide every later day
    // sideways, and the pointer would then resolve to the wrong one.
    [Fact]
    public void A_day_with_nothing_keeps_its_cell()
    {
        var bars = NetPanelLayout.Bars(560f, 100f, 198f, 3, [1f, 0f, 0.5f]);
        Assert.Equal(0f, bars[1].Height);
        Assert.True(bars[1].Left > bars[0].Left && bars[1].Left < bars[2].Left);
    }

    // Oldest at the left, newest at the right. This was built newest-left first, on the earlier instruction, and
    // settled here on the second look: "new data on the right, old on the left". The arrays stay chronological in
    // both, which is what made the reversal a one-line change.
    [Fact]
    public void The_newest_bucket_is_drawn_at_the_right_end_of_the_track()
    {
        var bars = NetPanelLayout.Bars(560f, 100f, 198f, 14, new float[14]);
        Assert.True(bars[13].Left > bars[0].Left, "the newest bucket is the last of the array");
        Assert.True(bars[13].Right > NetPanelLayout.TrackRight - 20f, "and it ends the track");
        Assert.True(bars[0].Left < NetPanelLayout.ChartLeft + 10f, "the oldest starts it");
        Assert.Equal(13, NetPanelLayout.LitBar(NetWindow.Today, 14));
    }

    // HoverDay reads the track's extent by scanning for the min and max rather than off bars[0] and bars[^1],
    // which is what survived the orientation changing twice: reading the ends of the ARRAY rejected the whole
    // chart the moment the drawing order stopped matching it, and the card would have died silently.
    [Fact]
    public void The_hover_range_does_not_depend_on_the_orientation()
    {
        var bars = NetPanelLayout.Bars(560f, 100f, 198f, 7, [1f, 1f, 1f, 1f, 1f, 1f, 1f]);
        var newest = new PointF(bars[6].Left + bars[6].Width / 2f, 150f);
        var oldest = new PointF(bars[0].Left + bars[0].Width / 2f, 150f);
        Assert.Equal(6, NetPanelLayout.HoverDay(newest, bars, 100f, 198f));
        Assert.Equal(0, NetPanelLayout.HoverDay(oldest, bars, 100f, 198f));
    }

    // The column owns the left third and the chart starts clear of it, so a figure and a bar can never overlap.
    [Fact]
    public void The_info_column_and_the_chart_do_not_share_any_x()
    {
        Assert.True(NetPanelLayout.ColRight < NetPanelLayout.ChartLeft);
        var bars = NetPanelLayout.Bars(560f, 100f, 198f, 30, new float[30]);
        foreach (var bar in bars)
            Assert.True(bar.Left >= NetPanelLayout.ColRight, $"a bar at {bar.Left} is over the column");
    }

    // Nothing is written under the chart any more, so the baseline may sit closer to the glass - but not past it.
    [Fact]
    public void The_baseline_leaves_no_room_for_a_caption_and_stays_inside()
    {
        Assert.Equal(204f, NetPanelLayout.BaseY(220f));
        Assert.True(NetPanelLayout.BaseY(220f) < 220f - 8f);
        Assert.True(NetPanelLayout.BaseY(420f) > NetPanelLayout.BaseY(220f), "a taller panel gives the chart the pixels");
    }

    // The bottom two rows ride the panel's height; the top block does not. A drag-resized panel must not leave
    // the link mark floating in the middle of the column.
    [Fact]
    public void The_columns_last_rows_follow_the_panels_bottom_edge()
    {
        var small = NetPanelLayout.Column(220f);
        var tall = NetPanelLayout.Column(420f);
        Assert.Equal(small.RatesY, tall.RatesY);
        Assert.Equal(small.TotalY, tall.TotalY);
        Assert.True(tall.MarkCy > small.MarkCy);
        Assert.True(tall.FootY > small.FootY);
        Assert.True(small.MarkCy < small.FootY);
        // every row inside a 220px panel, in order: the rates PAIR on one line and the window total at rest,
        // then what a pointer on the rates reveals, then the link mark and its foot line at the bottom edge
        Assert.True(small.RatesY < small.TotalY);
        Assert.True(small.TotalY < small.ShareY);
        Assert.True(small.ShareY < small.BandRow1);
        Assert.True(small.BandRow1 < small.BandRow2);
        Assert.True(small.BandRow2 < small.BandRow3);
        Assert.True(small.BandRow3 < small.MarkCy - 10f);
        Assert.True(small.FootY < 220f - 14f);
    }

    // The three reveal zones must not overlap, or a pointer would open two things at once and the panel would be
    // as loud as the version this replaced.
    [Fact]
    public void The_hover_zones_do_not_overlap()
    {
        var rates = NetPanelLayout.RatesZone(220f);
        var link = NetPanelLayout.LinkZone(220f);
        var chart = NetPanelLayout.ChartZone(560f, 220f);
        Assert.False(rates.IntersectsWith(link));
        Assert.False(rates.IntersectsWith(chart));
        Assert.False(link.IntersectsWith(chart));
        // and the chart zone reaches ABOVE the resting spark, which is only 44px of target
        var band = NetPanelLayout.ChartBand(220f, 0f);
        Assert.True(chart.Top < band.Top, "the zone has to be easier to hit than the spark itself");
        Assert.True(chart.Bottom >= band.BaseY);
    }

    // At rest the history is a spark; a pointer grows it. Same baseline throughout, so nothing jumps when it does.
    [Fact]
    public void The_chart_grows_from_the_spark_without_moving_its_baseline()
    {
        var rest = NetPanelLayout.ChartBand(220f, 0f);
        var half = NetPanelLayout.ChartBand(220f, 0.5f);
        var open = NetPanelLayout.ChartBand(220f, 1f);
        Assert.Equal(rest.BaseY, open.BaseY);
        Assert.Equal(NetPanelLayout.SparkHeight, rest.BaseY - rest.Top, 0.01f);
        Assert.Equal(NetPanelLayout.OpenHeight, open.BaseY - open.Top, 0.01f);
        Assert.True(half.Top < rest.Top && half.Top > open.Top);
        // opened, it still clears the chips at the top of the panel
        Assert.True(open.Top > NetPanelLayout.ChipTop + NetPanelLayout.ChipH);
        // and it is smaller than the old full-height chart, which was the complaint
        Assert.True(open.BaseY - open.Top < 140f);
    }

    [Fact]
    public void The_pointer_takes_the_nearest_bar_across_the_gaps()
    {
        var bars = NetPanelLayout.Bars(560f, 100f, 198f, 7, [1f, 1f, 1f, 1f, 1f, 1f, 1f]);
        for (int i = 0; i < bars.Length; i++)
        {
            var centre = new PointF(bars[i].Left + bars[i].Width / 2f, 150f);
            Assert.Equal(i, NetPanelLayout.HoverDay(centre, bars, 100f, 198f));
        }
        // the gap between two bars belongs to one of them rather than to nothing: a month's bars are 10px
        // wide with 7px of air between them, and a pointer that only counted hits inside a bar would spend
        // its time answering "no day"
        float gap = (bars[0].Right + bars[1].Left) / 2f;
        Assert.InRange(NetPanelLayout.HoverDay(new PointF(gap - 1f, 150f), bars, 100f, 198f), 0, 1);
        Assert.Equal(-1, NetPanelLayout.HoverDay(new PointF(10f, 150f), bars, 100f, 198f));
        Assert.Equal(-1, NetPanelLayout.HoverDay(new PointF(550f, 150f), bars, 100f, 198f));
        // above the chart is the totals line, and a card must not follow the pointer up there
        Assert.Equal(-1, NetPanelLayout.HoverDay(new PointF(bars[0].Left, 60f), bars, 100f, 198f));
    }

    // The split used to push the chart down to make room for two rows across the top of the panel. It now opens
    // inside the info column, on the other side of the gutter, so the chart holds still - SplitRows went with it.
    [Fact]
    public void The_split_opens_in_the_column_and_leaves_the_chart_where_it_is()
    {
        var col = NetPanelLayout.Column(220f);
        // the two link rows open in the band, on the other side of the gutter from the chart
        Assert.True(col.BandRow1 < col.BandRow2);
        Assert.True(col.BandRow2 + 14f < col.MarkCy);
    }

    // Three reveals share the band, one at a time, and each has to fit inside it: the trace occupies the first
    // two rows' worth of height and the live band puts a text row under it.
    [Fact]
    public void The_band_holds_whichever_reveal_owns_it()
    {
        var col = NetPanelLayout.Column(220f);
        Assert.True(col.TraceTop >= col.ShareY + 4f, "the trace starts below the share bar");
        Assert.True(col.TraceTop + col.TraceH <= col.BandRow3 + 2f, "and ends above the row under it");
        Assert.True(col.BandRow3 + 16f < col.MarkCy - 14f, "the band clears the link mark");
        // and the live speed's zone covers everything that reveal draws, or the pointer moving onto the trace
        // would leave the zone and collapse it at the frame rate
        var zone = NetPanelLayout.RatesZone(220f);
        Assert.True(zone.Top <= col.RatesY - 8f);
        Assert.True(zone.Bottom >= col.BandRow3 + 16f);
        Assert.True(zone.Bottom < col.MarkCy - 14f, "but stops short of the link mark's own zone");
    }

    [Fact]
    public void Only_the_today_window_lights_a_bar()
    {
        Assert.Equal(13, NetPanelLayout.LitBar(NetWindow.Today, 14));
        Assert.Equal(-1, NetPanelLayout.LitBar(NetWindow.Week, 7));
        Assert.Equal(-1, NetPanelLayout.LitBar(NetWindow.Month, 30));
        // an empty series has no last bar to outline
        Assert.Equal(-1, NetPanelLayout.LitBar(NetWindow.Today, 0));
    }

    // Away from the user zooms in, towards the user zooms out, and both ends stop rather than wrap - a wheel
    // that jumps from a month back to an hour has lost the user's place for them.
    [Fact]
    public void The_wheel_steps_one_window_at_a_time_and_clamps()
    {
        Assert.Equal(NetWindow.Today, NetPanelLayout.Scroll(NetWindow.Week, 1));
        Assert.Equal(NetWindow.Week, NetPanelLayout.Scroll(NetWindow.Month, 1));
        Assert.Equal(NetWindow.Month, NetPanelLayout.Scroll(NetWindow.Quarter, 1));
        Assert.Equal(NetWindow.Hour, NetPanelLayout.Scroll(NetWindow.Today, 1));
        // both ends clamp rather than wrapping: a wheel that jumps from 90 days to one hour has lost the
        // user's place for them
        Assert.Equal(NetWindow.Hour, NetPanelLayout.Scroll(NetWindow.Hour, 1));
        Assert.Equal(NetWindow.Quarter, NetPanelLayout.Scroll(NetWindow.Quarter, -1));
        Assert.Equal(NetWindow.Week, NetPanelLayout.Scroll(NetWindow.Today, -1));
        // a fast flick arrives as several notches in one read, and it is still one step: five windows do not
        // need acceleration, and a flick that skipped the middle ones would make the chips the only way back
        Assert.Equal(NetWindow.Week, NetPanelLayout.Scroll(NetWindow.Month, 4));
        Assert.Equal(NetWindow.Today, NetPanelLayout.Scroll(NetWindow.Today, 0));
    }

    // The chart takes whatever height a drag-resized pill leaves it, down to a floor: the panel can be
    // shrunk until top and baseline cross, and a negative span drew bars hanging off the top edge.
    [Fact]
    public void A_shrunk_panel_still_leaves_the_chart_a_floor()
    {
        var bars = NetPanelLayout.Bars(560f, 100f, 104f, 7, [1f, 1f, 1f, 1f, 1f, 1f, 1f]);
        Assert.Equal(12f, bars[0].Height, 0.01f);
        Assert.Equal(104f, bars[0].Bottom, 0.01f);
    }
}
