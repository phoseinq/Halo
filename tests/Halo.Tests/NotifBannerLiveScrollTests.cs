using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The summary body is a scrolling viewport two lines tall. A folded message slides the thread up to the
// bottom while the line that caused it fades in - motion that no single screenshot can show and that the pill
// cannot be screenshotted doing, so the two functions that decide it are pinned here and looked at as a
// filmstrip through --render-livetext.
public class NotifBannerLiveScrollTests
{
    // A thread that fits the box does not scroll at all. This is every banner that is not a conversation, and
    // it is the whole reason a scrolling viewport is safe to put on the ordinary draw path.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_body_that_fits_never_scrolls(int total)
    {
        Assert.Equal(0f, NotifBanner.LiveScroll(total, older: total, visible: 2, fold: 0f));
        Assert.Equal(0f, NotifBanner.LiveScroll(total, older: total, visible: 2, fold: 1f));
    }

    // Two lines on screen, a third arrives: the viewport ends one line further down, so what is shown is
    // lines 2 and 3 rather than 1 and 2.
    [Fact]
    public void A_third_line_scrolls_the_viewport_down_by_exactly_one_line()
    {
        Assert.Equal(0f, NotifBanner.LiveScroll(total: 3, older: 2, visible: 2, fold: 0f));
        Assert.Equal(1f, NotifBanner.LiveScroll(total: 3, older: 2, visible: 2, fold: 1f));
    }

    // Fractional on the way, which is what makes it a scroll rather than a jump: at half the animation the
    // lines sit half a line height up.
    [Fact]
    public void The_scroll_is_continuous_rather_than_stepped()
        => Assert.Equal(0.5f, NotifBanner.LiveScroll(total: 3, older: 2, visible: 2, fold: 0.5f));

    // A message that wraps to two lines moves the viewport by two, which is the case a whole-message tail got
    // wrong: it scrolled by one message regardless of how much room that message took.
    [Fact]
    public void A_message_that_wraps_scrolls_by_what_it_actually_occupies()
    {
        Assert.Equal(1f, NotifBanner.LiveScroll(total: 5, older: 3, visible: 2, fold: 0f));
        Assert.Equal(3f, NotifBanner.LiveScroll(total: 5, older: 3, visible: 2, fold: 1f));
    }

    [Fact]
    public void Fold_outside_zero_to_one_is_clamped()
    {
        Assert.Equal(0f, NotifBanner.LiveScroll(total: 3, older: 2, visible: 2, fold: -1f));
        Assert.Equal(1f, NotifBanner.LiveScroll(total: 3, older: 2, visible: 2, fold: 9f));
    }

    // A scroll against a hard clip cuts a row of glyphs in half against the top edge, which reads as a
    // rendering fault and not as motion. A line only partly inside the box fades by how much of it is in.
    [Fact]
    public void A_line_fully_inside_the_viewport_is_at_full_ink()
        => Assert.Equal(1f, NotifBanner.ClipFade(lineTop: 10f, lineH: 19f, viewTop: 10f, viewH: 44f));

    [Fact]
    public void A_line_half_out_of_the_top_is_at_half_ink()
        => Assert.Equal(0.5f, NotifBanner.ClipFade(lineTop: 0.5f, lineH: 19f, viewTop: 10f, viewH: 44f), 3);

    [Fact]
    public void A_line_wholly_outside_contributes_nothing()
    {
        Assert.Equal(0f, NotifBanner.ClipFade(lineTop: -30f, lineH: 19f, viewTop: 10f, viewH: 44f));
        Assert.Equal(0f, NotifBanner.ClipFade(lineTop: 90f, lineH: 19f, viewTop: 10f, viewH: 44f));
    }

    [Fact]
    public void A_zero_height_line_does_not_divide_by_zero()
        => Assert.Equal(0f, NotifBanner.ClipFade(lineTop: 10f, lineH: 0f, viewTop: 10f, viewH: 44f));
}
