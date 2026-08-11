using Halo.Notifications;
using Xunit;

namespace Halo.Tests;

// Live text: a message arriving while its own banner is still up is written under the lines already there,
// instead of queueing behind it and playing its own open-and-close. The banner cannot be screenshotted while
// it happens and the arrivals are seconds apart, so the rules are pinned here rather than by watching.
public class LiveTextTests
{
    private static NotifItem Msg(string title, string body, string aumid = "app.messages")
        => new() { Id = 1, Aumid = aumid, App = "Messages", Title = title, Body = body };

    [Fact]
    public void The_next_message_of_the_same_conversation_folds()
        => Assert.True(LiveText.CanFold(Msg("Ali", "on my way"), Msg("Ali", "5 minutes")));

    // A messaging app puts the sender in the title, so this is the line between "the same conversation" and
    // "a second person also wrote to you" - and the second is a banner of its own.
    [Fact]
    public void A_different_sender_is_a_different_banner()
        => Assert.False(LiveText.CanFold(Msg("Ali", "on my way"), Msg("Sara", "hi")));

    [Fact]
    public void A_different_app_is_a_different_banner()
        => Assert.False(LiveText.CanFold(Msg("Ali", "on my way"), Msg("Ali", "hi", "app.other")));

    // An empty Aumid is every locally synthesised banner - battery, CPU, the hourly chime. They share a title
    // with the next one of their kind and would otherwise all fold into each other.
    [Fact]
    public void A_banner_with_no_app_id_never_folds()
        => Assert.False(LiveText.CanFold(
            new NotifItem { Title = "Battery critical", Body = "7%" },
            new NotifItem { Title = "Battery critical", Body = "6%" }));

    // Each of these is a banner that owns something a second message would invalidate: a Kind banner is
    // meant to be replaced, a Preview is a screenshot thumbnail, and a Code banner's Copy button can only
    // point at one code.
    [Fact]
    public void A_kind_banner_is_replaced_rather_than_accumulated()
    {
        var a = Msg("Language", "EN"); a.Kind = "language";
        var b = Msg("Language", "FA"); b.Kind = "language";
        Assert.False(LiveText.CanFold(a, b));
    }

    [Fact]
    public void A_code_banner_does_not_accumulate_a_second_code()
    {
        var a = Msg("Bank", "your code is 123456"); a.Code = "123456";
        var b = Msg("Bank", "your code is 654321"); b.Code = "654321";
        Assert.False(LiveText.CanFold(a, b));
    }

    [Fact]
    public void An_empty_message_is_not_worth_folding()
        => Assert.False(LiveText.CanFold(Msg("Ali", "on my way"), Msg("Ali", "   ")));

    [Fact]
    public void The_new_line_is_written_under_the_old_one()
        => Assert.Equal("on my way\n5 minutes", LiveText.Append("on my way", "5 minutes"));

    [Fact]
    public void The_first_line_lands_on_an_empty_body()
        => Assert.Equal("on my way", LiveText.Append("", "on my way"));

    // Phone Link and several chat apps re-post the whole thread instead of just the new line. Written
    // underneath, that shows the same sentence twice and then three times.
    [Fact]
    public void A_body_that_repeats_the_thread_replaces_it_instead_of_doubling_it()
        => Assert.Equal("on my way\n5 minutes",
            LiveText.Append("on my way", "on my way\n5 minutes"));

    [Fact]
    public void The_same_message_delivered_twice_does_not_double()
        => Assert.Equal("on my way", LiveText.Append("on my way", "on my way"));

    // DetailHeight tops out at 280px, so an uncapped thread would peg the sheet there and then push the line
    // that just arrived off the bottom of it.
    [Fact]
    public void The_oldest_lines_fall_off_the_top_past_the_cap()
    {
        string body = "1";
        for (int i = 2; i <= LiveText.MaxLines + 3; i++) body = LiveText.Append(body, i.ToString());

        var lines = body.Split('\n');
        Assert.Equal(LiveText.MaxLines, lines.Length);
        Assert.Equal((LiveText.MaxLines + 3).ToString(), lines[^1]);   // the newest survives
        Assert.Equal("4", lines[0]);                                   // the oldest three are gone
    }

    // The two Tail tests that were here moved to NotifBannerLiveScrollTests: what the summary shows is now
    // decided in wrapped LINES by a scrolling viewport rather than in whole messages by a string helper.

    [Fact]
    public void Dwell_grows_with_the_thread_but_is_capped()
    {
        Assert.Equal(7.5, LiveText.Extend(6));
        Assert.Equal(12, LiveText.Extend(11));
        Assert.Equal(12, LiveText.Extend(12));
    }
}
