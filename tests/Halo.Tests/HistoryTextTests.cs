using System;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

public class HistoryTextTests
{
    private static readonly DateTime Now = new(2026, 9, 2, 14, 35, 0);

    [Fact]
    public void Just_arrived_says_now_rather_than_zero_minutes()
    {
        Assert.Equal(HistoryText.When(Now.AddSeconds(-5), Now), HistoryText.When(Now, Now));
    }

    [Fact]
    public void Inside_the_hour_it_is_a_duration()
    {
        Assert.Equal("12m", HistoryText.When(Now.AddMinutes(-12), Now));
        Assert.Equal("59m", HistoryText.When(Now.AddMinutes(-59), Now));
    }

    [Fact]
    public void Later_today_it_is_a_clock()
    {
        Assert.Equal("09:20", HistoryText.When(new DateTime(2026, 9, 2, 9, 20, 0), Now));
    }

    [Fact]
    public void Earlier_in_the_week_the_clock_alone_is_ambiguous_so_the_day_comes_with_it()
    {
        var s = HistoryText.When(new DateTime(2026, 8, 31, 9, 20, 0), Now);
        Assert.Contains("09:20", s);
        Assert.True(s.Length > "09:20".Length);
    }

    [Fact]
    public void Older_than_a_week_carries_a_date()
    {
        var s = HistoryText.When(new DateTime(2026, 7, 4, 9, 20, 0), Now);
        Assert.Contains("4", s);
        Assert.Contains("09:20", s);
    }

    [Fact]
    public void A_clock_that_stepped_back_is_not_the_future()
    {
        Assert.Equal(HistoryText.When(Now, Now), HistoryText.When(Now.AddMinutes(5), Now));
    }

    [Fact]
    public void The_gist_is_the_first_real_sentence()
    {
        Assert.Equal("Are you coming tonight?",
                     HistoryText.Gist("Are you coming tonight? I booked a table for eight."));
    }

    [Fact]
    public void A_full_stop_inside_a_word_does_not_end_it()
    {
        // "3.5" and "report.pdf" are where a naive split breaks, and both turn up in real notifications
        Assert.Equal("Build 3.5 finished and report.pdf is attached.",
                     HistoryText.Gist("Build 3.5 finished and report.pdf is attached."));
    }

    [Fact]
    public void Quoted_replies_are_not_the_point_of_the_message()
    {
        var body = "Sounds good to me.\n> On Tuesday you wrote:\n> the original question";
        Assert.Equal("Sounds good to me.", HistoryText.Gist(body));
    }

    [Fact]
    public void A_signature_ends_it()
    {
        Assert.Equal("Please review", HistoryText.Gist("Please review\n--\nSent from my phone"));
    }

    [Fact]
    public void Whitespace_from_every_direction_collapses()
    {
        Assert.Equal("one two three", HistoryText.Gist("  one\t\ttwo \n three  "));
    }

    [Fact]
    public void A_long_body_with_no_sentence_end_is_clipped_at_a_word()
    {
        var s = HistoryText.Gist(new string('a', 20) + " " + new string('b', 200), 40);
        Assert.True(s.Length <= 41);
        Assert.EndsWith("…", s);          // a real ellipsis, not three dots
        Assert.DoesNotContain("aaab", s);      // never mid-word
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void Nothing_to_summarise_is_an_empty_string_not_a_crash(string? body)
    {
        Assert.Equal("", HistoryText.Gist(body));
        Assert.Equal("", HistoryText.Full(body));
        Assert.False(HistoryText.HasMore(body));
    }

    [Fact]
    public void A_body_that_is_already_its_own_gist_has_nothing_more_to_read()
    {
        // the chip is drawn off this: offering "read it" on a row that would not change is a button that
        // visibly does nothing, which is worse than no button
        Assert.False(HistoryText.HasMore("Invoice #4821 for August is attached."));
        Assert.True(HistoryText.HasMore("Are you coming tonight? I booked a table for eight."));
    }

    [Fact]
    public void What_read_it_reveals_is_the_body_cleaned_the_same_way_the_gist_was()
    {
        // opening a row must not contradict its own summary by producing the quoted reply the gist dropped
        var body = "Sounds good to me.\nSee you at eight.\n> On Tuesday you wrote:\n> the original";
        // ...and its lines stay lines, so each paragraph can be set in its own direction
        Assert.Equal("Sounds good to me.\nSee you at eight.", HistoryText.Full(body));
    }
}

// Reported as "it hasn't summarised it". The cause was measurable rather than aesthetic: the default was
// 96 characters but only ~46 fit the gist line at CardW, so the cut almost never came from the summariser
// at all - the row overflowed and the layout clipped it.
public class HistoryGistBudgetTests
{
    [Fact]
    public void The_default_gist_fits_the_line_it_has_to_fit()
        => Assert.True(HistoryText.Gist(new string('a', 400)).Length <= 59);   // 58 plus the ellipsis

    // "Build failed: 3 tests" is a whole thought at the colon, and stopping there says more in fewer
    // characters than running on into the list does.
    [Fact]
    public void A_colon_ends_a_thought_the_same_way_a_full_stop_does()
        => Assert.Equal("Build failed:", HistoryText.Gist("Build failed: 3 tests in 2 suites, see the log"));

    // the colon rule must not fire on 14:30 - the character after it is a digit, not a space, which is the
    // same guard that already keeps "3.5" and "file.txt" from ending a sentence
    [Fact]
    public void A_clock_time_is_not_a_colon_that_ends_anything()
        => Assert.Equal("Standup at 14:30 in the usual room.",
                        HistoryText.Gist("Standup at 14:30 in the usual room. Bring notes."));

    [Fact]
    public void A_sentence_still_wins_when_there_is_one()
        => Assert.Equal("He is on his way.", HistoryText.Gist("He is on his way. Should be twenty minutes."));

    // Past halfway a comma is a real boundary; before it, stopping there would throw away most of the line
    // for nothing.
    [Fact]
    public void A_late_comma_beats_a_word_break()
    {
        // the budget is passed rather than defaulted: this pins the RULE, and the default is a measurement
        // of the card's gist line that moves whenever the card does
        string s = "the deployment finished cleanly, and the cache was warmed afterwards as usual";
        Assert.Equal("the deployment finished cleanly,", HistoryText.Gist(s, 52));
    }

    [Fact]
    public void An_early_comma_is_not_a_boundary()
    {
        string s = "Hi, the quarterly planning document is ready for your review whenever you have time";
        Assert.EndsWith("…", HistoryText.Gist(s));
        Assert.True(HistoryText.Gist(s).Length > 20);
    }

    [Fact]
    public void A_short_body_is_left_entirely_alone()
        => Assert.Equal("see you at three", HistoryText.Gist("see you at three"));
}
