using Halo.ClaudeCode;
using Xunit;

namespace Halo.Tests;

// Reading the question box instead of guessing at it. Every fixture here is a real --probe-console dump of
// Claude Code's own multi-select box, trimmed to the rows.
//
// The measurement that produced these: the digit for the free-text row TICKS it from a distance and leaves
// the caret where it was, so the letters typed after it went to whatever row the caret was really on and the
// row came back ticked and empty. The caret is what feeds the field, so the pill has to know where it is.
public class AskBoxReadTests
{
    private const string Caret = "\u276F";   // the marker the box draws against the focused row
    private const string Tick = "\u2714";    // what it puts between the brackets - a character, not a colour

    // caret on the free-text row, which is holding the word it was sent
    private static readonly string Filled = string.Join("\n",
        "  1. [ ] Ignore me",
        "  2. [ ] Ignore me too",
        Caret + " 3. [" + Tick + "] ping",
        "     Submit",
        "  4. Chat about this");

    // the same box a moment earlier: nothing typed, so the row shows the CLI's placeholder
    private static readonly string Empty = string.Join("\n",
        "  1. [ ] Ignore me",
        "  2. [ ] Ignore me too",
        Caret + " 3. [ ] Type something",
        "     Submit",
        "  4. Chat about this");

    [Fact]
    public void The_caret_says_which_row_has_focus()
        => Assert.Equal(3, AskStore.CaretRowIn(Filled));

    // it parks on the Submit line between the list and the chat row, which carries no number
    [Fact]
    public void A_caret_on_an_unnumbered_line_is_row_zero()
        => Assert.Equal(0, AskStore.CaretRowIn(string.Join("\n", "  1. [ ] Ignore me", Caret + "     Submit")));

    // Not the same answer as zero, and the difference is the whole safety of the walk: a console that read
    // back nothing means the pill has no idea where the caret is, and pressing Down there is how a real
    // option gets ticked by accident.
    [Fact]
    public void An_unreadable_console_is_not_a_caret_on_row_zero()
        => Assert.Equal(-1, AskStore.CaretRowIn(""));

    [Fact]
    public void A_ticked_row_reads_back_the_words_it_is_holding()
        => Assert.Equal("ping", AskStore.RowTextIn(Filled, 3));

    // The placeholder sits in exactly the same place as an answer would. The checkbox is what tells them
    // apart - and it has to be, because the placeholder is the CLI's own text in the CLI's own language.
    [Fact]
    public void A_blank_checkbox_is_an_empty_field_whatever_text_follows_it()
        => Assert.Equal("", AskStore.RowTextIn(Empty, 3));

    [Fact]
    public void An_option_row_reads_back_as_empty_too()
        => Assert.Equal("", AskStore.RowTextIn(Filled, 1));

    [Fact]
    public void A_row_that_is_not_on_screen_reads_back_as_null()
        => Assert.Null(AskStore.RowTextIn(Filled, 9));

    // "13." must not answer for "3.", or a long question would have the pill correcting the wrong field.
    [Fact]
    public void A_two_digit_row_is_not_mistaken_for_its_last_digit()
    {
        string dump = "  13. [" + Tick + "] thirteen";
        Assert.Null(AskStore.RowTextIn(dump, 3));
        Assert.Equal("thirteen", AskStore.RowTextIn(dump, 13));
    }

    // the caret sits in front of the row it is on, and that must not be read as part of the number
    [Fact]
    public void The_caret_does_not_disturb_the_row_it_marks()
        => Assert.Equal("ping", AskStore.RowTextIn(Filled, 3));

    // Measured, and a limitation worth pinning rather than hiding: the row's own digit unticks it but leaves
    // the words in the field, and an unticked row reads back as empty whatever it is holding. Nothing outside
    // the box can tell "3. [ ] keep" from "3. [ ] Type something" - which is exactly why the pill retracts an
    // answer by emptying the field rather than by pressing the digit.
    [Fact]
    public void An_unticked_row_reads_back_empty_even_while_it_still_holds_words()
        => Assert.Equal("", AskStore.RowTextIn("  3. [ ] keep", 3));

    [Fact]
    public void Words_that_read_back_unchanged_have_landed()
        => Assert.True(AskStore.SameWords("ping", "ping"));

    // The buffer holds what the terminal SHOWS, and a right-to-left answer is not shown in the order it was
    // typed. Both sides here are the real measurement off dump010: "test 1" in Persian went in as
    // 062A 0633 062A 0031 and came back 0031 062A 0633 062A - digit first, letters reversed. Compared as
    // strings that says "wrong" forever, and the field was cleared and retyped on every round for nothing.
    [Fact]
    public void A_right_to_left_answer_reads_back_reordered_and_is_still_the_same_answer()
        => Assert.True(AskStore.SameWords("1\u062A\u0633\u062A", "\u062A\u0633\u062A1"));

    [Fact]
    public void Different_words_of_the_same_length_are_a_real_difference()
        => Assert.False(AskStore.SameWords("\u062A\u0633\u062A1", "\u062A\u0633\u062A2"));

    // the case that came back as new+old: a field holding one answer and asked for another
    [Fact]
    public void A_replacement_is_never_mistaken_for_what_the_field_already_holds()
    {
        Assert.False(AskStore.SameWords("ping", "pinged"));
        Assert.False(AskStore.SameWords("", "ping"));
    }
}
