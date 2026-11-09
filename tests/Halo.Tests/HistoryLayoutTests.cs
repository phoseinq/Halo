using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

public class HistoryLayoutTests
{
    // The whole point of the redesign: the sheet is no longer a morph of the expanded panel, so it is free
    // to be SHORTER than the panel for a short list. MinH existed only to stop the pull contracting the
    // pill and is gone, so a one-item sheet must now actually be one item tall.
    [Fact]
    public void A_one_item_sheet_is_barely_taller_than_one_card()
    {
        float h = HistoryLayout.Height(1);
        Assert.Equal(HistoryLayout.BodyTop + HistoryLayout.RowH + HistoryLayout.PadBottom, h, 1);
        Assert.True(h < 200f, $"a one-item shade should not be 200px tall, got {h}");
    }

    [Fact]
    public void The_sheet_grows_one_card_at_a_time()
    {
        Assert.Equal(HistoryLayout.RowH, HistoryLayout.Height(2) - HistoryLayout.Height(1), 1);
        Assert.Equal(HistoryLayout.RowH, HistoryLayout.Height(5) - HistoryLayout.Height(4), 1);
    }

    // ...and stops growing at Visible, because past that the remainder becomes a deck rather than more
    // rows. The deck adds its own fixed height exactly once, however many are hidden behind it.
    [Fact]
    public void Past_five_the_extra_notifications_become_a_deck_not_more_height()
    {
        float atCap = HistoryLayout.Height(HistoryLayout.Visible);
        Assert.Equal(atCap + HistoryLayout.DeckH, HistoryLayout.Height(HistoryLayout.Visible + 1), 1);
        Assert.Equal(HistoryLayout.Height(HistoryLayout.Visible + 1), HistoryLayout.Height(500), 1);
    }

    // The window is per-pixel alpha, so its width and the card's width are unrelated. This is the line that
    // makes "the rows are too wide" fixable at all: CardX used to be a constant 12 and the card spanned the
    // whole window.
    [Fact]
    public void The_card_is_narrower_than_the_window_and_centred_in_it()
    {
        var c = HistoryLayout.Card(HistoryLayout.BodyTop);
        Assert.Equal(HistoryLayout.CardW, c.Width, 1);
        Assert.True(c.Width < HistoryLayout.W);
        Assert.Equal(HistoryLayout.W - c.Right, c.X, 1);   // equal margins
    }

    // 9:1 was the reported "boxy" shape. Rounded means the radius is a real fraction of the height.
    [Fact]
    public void The_card_is_not_a_bar()
    {
        var c = HistoryLayout.Card(HistoryLayout.BodyTop);
        Assert.True(c.Width / c.Height < 6f, $"aspect {c.Width / c.Height:0.0}:1 still reads as a bar");
        Assert.True(HistoryLayout.CardR >= c.Height / 4f);
    }

    [Fact]
    public void Rows_start_below_the_pill_not_at_the_top_of_the_window()
    {
        Assert.Equal(HistoryLayout.BodyTop, HistoryLayout.RowTop(0, 0f, HistoryShift.None), 1);
        Assert.True(HistoryLayout.BodyTop >= HistoryLayout.PillH);
    }

    [Fact]
    public void An_empty_history_still_has_a_line_to_say_so_in()
    {
        Assert.True(HistoryLayout.Height(0) > HistoryLayout.BodyTop);
    }

    [Fact]
    public void Rows_stack_downward_from_under_the_header()
    {
        Assert.Equal(HistoryLayout.BodyTop, HistoryLayout.RowTop(0, 0f));
        Assert.Equal(HistoryLayout.BodyTop + HistoryLayout.RowH, HistoryLayout.RowTop(1, 0f));
    }

    [Fact]
    public void Scrolling_moves_the_rows_up()
    {
        Assert.Equal(HistoryLayout.RowTop(2, 0f) - 30f, HistoryLayout.RowTop(2, 30f));
    }

    [Fact]
    public void Nothing_scrolls_while_everything_fits()
    {
        Assert.Equal(0f, HistoryLayout.MaxScroll(1));
        Assert.Equal(0f, HistoryLayout.MaxScroll(HistoryLayout.Fits));
        Assert.True(HistoryLayout.MaxScroll(HistoryLayout.Fits + 4) > 0f);
    }

    [Fact]
    public void A_point_in_the_body_names_its_row()
    {
        Assert.Equal(0, HistoryLayout.RowAt(HistoryLayout.BodyTop + 4f, 5, 0f));
        Assert.Equal(1, HistoryLayout.RowAt(HistoryLayout.BodyTop + HistoryLayout.RowH + 4f, 5, 0f));
    }

    [Fact]
    public void The_header_never_falls_through_to_a_row()
    {
        // it carries "clear all", and a click that slips off that button must not open a message
        Assert.Equal(-1, HistoryLayout.RowAt(4f, 5, 0f));
        Assert.Equal(-1, HistoryLayout.RowAt(HistoryLayout.BodyTop - 1f, 5, 0f));
    }

    [Fact]
    public void Below_the_last_row_is_nothing()
    {
        Assert.Equal(-1, HistoryLayout.RowAt(HistoryLayout.Height(2) - 2f, 2, 0f));
    }

    [Fact]
    public void The_dismiss_target_sits_inside_its_own_row()
    {
        float top = HistoryLayout.RowTop(0, 0f);
        var x = HistoryLayout.Dismiss(top);
        Assert.True(x.Top >= top && x.Bottom <= top + HistoryLayout.CardH);
        Assert.True(x.Right <= HistoryLayout.CardX + HistoryLayout.CardW);
    }

    private static HistoryShift Opened(int row, float extra) => new(row, extra, -1, 0f);
    private static HistoryShift Leaving(int at, float gap) => new(-1, 0f, at, gap);

    [Fact]
    public void An_opened_row_pushes_the_ones_below_it_down_and_leaves_the_ones_above_alone()
    {
        var s = Opened(1, 60f);
        Assert.Equal(HistoryLayout.RowTop(0, 0f), HistoryLayout.RowTop(0, 0f, s));
        Assert.Equal(HistoryLayout.RowTop(1, 0f), HistoryLayout.RowTop(1, 0f, s));
        Assert.Equal(HistoryLayout.RowTop(2, 0f) + 60f, HistoryLayout.RowTop(2, 0f, s));
    }

    [Fact]
    public void Every_point_of_an_opened_row_still_names_that_row()
    {
        // the whole grown card is one target: a click on the body it opened to show must not report the
        // row below, which is where the unadjusted arithmetic put it
        var s = Opened(1, 60f);
        float top = HistoryLayout.RowTop(1, 0f, s);
        Assert.Equal(1, HistoryLayout.RowAt(top + 4f, 4, 0f, s));
        Assert.Equal(1, HistoryLayout.RowAt(top + HistoryLayout.RowH + 40f, 4, 0f, s));
        Assert.Equal(2, HistoryLayout.RowAt(top + HistoryLayout.RowH + 64f, 4, 0f, s));
    }

    [Fact]
    public void An_opened_row_gives_the_list_more_to_scroll_through()
    {
        Assert.True(HistoryLayout.MaxScroll(HistoryLayout.Fits, HistoryLayout.OpenExtra)
                    > HistoryLayout.MaxScroll(HistoryLayout.Fits));
    }

    [Fact]
    public void A_closing_gap_holds_the_rows_below_it_down_until_it_is_gone()
    {
        // the row that INHERITED the index is the one that must wait: at a full gap it sits exactly where
        // it did before the dismissal, and at zero it has arrived
        float was = HistoryLayout.RowTop(2, 0f);
        Assert.Equal(was, HistoryLayout.RowTop(1, 0f, Leaving(1, HistoryLayout.RowH)));
        Assert.Equal(HistoryLayout.RowTop(1, 0f), HistoryLayout.RowTop(1, 0f, Leaving(1, 0f)));
        Assert.Equal(HistoryLayout.RowTop(0, 0f), HistoryLayout.RowTop(0, 0f, Leaving(1, HistoryLayout.RowH)));
    }

    [Fact]
    public void The_card_that_is_leaving_starts_in_the_slot_it_had()
    {
        var s = Leaving(1, HistoryLayout.RowH);
        Assert.Equal(HistoryLayout.RowTop(1, 0f), HistoryLayout.LeavingTop(0f, s));
    }

    [Fact]
    public void The_sheet_only_shrinks_as_the_gap_closes_not_the_moment_the_row_goes()
    {
        // a sheet that snapped to its new height while the card was still flying out pulled the ground
        // out from under the gesture
        float mid = HistoryLayout.Height(3, HistoryLayout.RowH * 0.5f);
        Assert.True(mid > HistoryLayout.Height(3) && mid < HistoryLayout.Height(4));
    }

    [Fact]
    public void The_growth_is_exactly_what_the_body_needs_and_no_more()
    {
        // ExtraFor is the inverse of Body's height - if they drift, an opened row either clips its own
        // text or floats it above a gap, which is what a fixed 96 did
        float extra = HistoryLayout.ExtraFor(50f);
        Assert.Equal(50f, HistoryLayout.Body(0f, extra).Height, 1);
    }

    [Fact]
    public void A_body_too_long_to_show_stops_at_the_cap_rather_than_growing_forever()
    {
        Assert.Equal(HistoryLayout.OpenExtra, HistoryLayout.ExtraFor(5000f));
        Assert.True(HistoryLayout.ExtraFor(0f) > 0f);   // opening must always visibly do something
    }

    // The chip is gone - the card opens when you click the card - so the body owns the space it held and
    // the growth must still be exactly what the text needs.
    [Fact]
    public void An_opened_card_is_as_tall_as_its_own_body_and_no_taller()
    {
        foreach (float extra in new[] { 24f, 60f, HistoryLayout.OpenExtra })
        {
            var body = HistoryLayout.Body(0f, extra);
            var card = HistoryLayout.Card(0f, extra);
            Assert.True(body.Bottom <= card.Bottom - HistoryLayout.BodyPad + 0.001f);
            Assert.True(body.Top > card.Top);
        }
    }

    // and the one target left on a hovered row still sits inside it
    [Fact]
    public void The_dismiss_X_stays_inside_its_card()
    {
        var x = HistoryLayout.Dismiss(0f);
        Assert.True(HistoryLayout.Card(0f).Contains(x));
    }

    // an opened card spreads its growth over the frames instead of spending it on the first few
    [Fact]
    public void An_opening_card_never_jumps_in_one_frame()
    {
        const float dt = 1f / 120f;
        float t = 0f, was = 0f, largest = 0f;
        while (t < 1f)
        {
            t = Math.Min(1f, t + dt / HistoryLayout.OpenSeconds);
            float now = HistoryLayout.OpenEase(t);
            largest = Math.Max(largest, now - was);
            was = now;
        }
        Assert.Equal(1f, was, 3);
        Assert.True(largest < 0.06f, $"largest single-frame step {largest}");
        Assert.True(HistoryLayout.OpenEase(dt / HistoryLayout.OpenSeconds) < 0.01f);   // it eases in
    }

    // a card leaving the stack reaches its full width with no corner in the curve
    [Fact]
    public void The_stack_width_eases_and_keeps_its_resting_layers()
    {
        float front = HistoryLayout.Visible - 1;
        Assert.Equal(0f, HistoryLayout.Pose(front).Inset, 3);
        Assert.Equal(HistoryLayout.PeekInset, HistoryLayout.Pose(front + 1f).Inset, 3);
        Assert.Equal(HistoryLayout.MaxBack * HistoryLayout.PeekInset, HistoryLayout.Pose(front + HistoryLayout.MaxBack).Inset, 3);
        // near full width it is barely moving, where a straight line would still be at full speed
        Assert.True(HistoryLayout.Pose(front + 0.05f).Inset < HistoryLayout.PeekInset * 0.05f * 0.25f);
    }
}
