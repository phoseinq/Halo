using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The reveal is a function of the pull, not of a clock: the sheet is DRAGGED, so a timed stagger would run
// on while a finger held the sheet still. Driving it off the same t the geometry uses keeps every card
// finger-linked, and makes the whole thing testable without a surface that cannot be screenshotted.
public class HistoryRevealTests
{
    [Fact]
    public void Nothing_is_out_before_the_pull_starts()
        => Assert.Equal(0f, HistoryLayout.Reveal(0, 0f), 3);

    [Fact]
    public void Every_visible_row_has_landed_when_the_pull_completes()
    {
        for (int i = 0; i <= HistoryLayout.Visible; i++)   // Visible itself is the deck's index
            Assert.Equal(1f, HistoryLayout.Reveal(i, 1f), 3);
    }

    [Fact]
    public void The_top_card_leads_the_bottom_one()
        => Assert.True(HistoryLayout.Reveal(0, 0.5f) > HistoryLayout.Reveal(4, 0.5f));

    [Fact]
    public void Each_row_only_ever_advances()
    {
        for (int i = 0; i < 5; i++)
        {
            float prev = -1f;
            for (float t = 0f; t <= 1f; t += 0.02f)
            {
                float v = HistoryLayout.Reveal(i, t);
                Assert.True(v >= prev, $"row {i} went backwards at t={t}: {v} after {prev}");
                prev = v;
            }
        }
    }

    // A row past the deck must not start later and later forever - the expanded list has more rows than the
    // shade does, and a twelfth card that only begins at t=0.9 would arrive after the sheet has stopped.
    [Fact]
    public void A_row_past_the_cap_shares_the_last_ones_timing()
        => Assert.Equal(HistoryLayout.Reveal(4, 0.6f), HistoryLayout.Reveal(11, 0.6f), 3);

    [Fact]
    public void It_folds_from_the_bottom_up()
    {
        // closing runs the same function backwards: the largest start reaches zero first, so the deck end
        // of the stack retracts into the pill before the top card does
        float t = 0.2f;
        Assert.Equal(0f, HistoryLayout.Reveal(4, t), 3);
        Assert.True(HistoryLayout.Reveal(0, t) > 0f);
    }

    // It comes DOWN into place, and the drop is short: the version that lerped every card from one line
    // piled four of them inside 24 pixels at t=0.25, which is text over text rather than a shade.
    [Fact]
    public void A_card_drops_a_little_way_into_its_slot()
    {
        float slot = HistoryLayout.RowTop(3, 0f);
        Assert.True(HistoryLayout.RevealTop(slot, 0f) < slot);
        Assert.True(slot - HistoryLayout.RevealTop(slot, 0f) < HistoryLayout.RowH / 2f);
        Assert.Equal(slot, HistoryLayout.RevealTop(slot, 1f), 3);
    }

    // the gaps between cards stay the gaps between cards while they are moving
    [Fact]
    public void Two_cards_mid_flight_never_overlap()
    {
        for (float t = 0f; t <= 1f; t += 0.05f)
            for (int i = 1; i < HistoryLayout.Visible; i++)
            {
                float above = HistoryLayout.RevealTop(HistoryLayout.RowTop(i - 1, 0f),
                                                      HistoryLayout.Reveal(i - 1, t));
                float here = HistoryLayout.RevealTop(HistoryLayout.RowTop(i, 0f), HistoryLayout.Reveal(i, t));
                Assert.True(here >= above + HistoryLayout.CardH,
                            $"row {i} at t={t} sat {here} under a card ending {above + HistoryLayout.CardH}");
            }
    }

    // The drop is short, and what keeps an in-flight card off "clear all" is the body clip at BodyTop -
    // not the header's height, which is now only a gap. The button lives above that line, so anything a
    // rising card puts there is cut rather than painted over it.
    [Fact]
    public void The_drop_is_short_and_the_body_clip_is_what_hides_it()
    {
        Assert.True(HistoryLayout.RevealRise < HistoryLayout.RowH / 2f);
        Assert.True(HistoryLayout.ClearAll().Bottom <= HistoryLayout.BodyTop);
        float slot = HistoryLayout.RowTop(4, 0f);
        for (float p = 0f; p <= 1f; p += 0.05f)
            Assert.InRange(slot - HistoryLayout.RevealTop(slot, p), 0f, HistoryLayout.RevealRise);
    }
}
