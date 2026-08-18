using System.Collections.Generic;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// Which parts of a string have to be drawn in a different face, and which must NOT be touched.
//
// The second half is the one worth guarding. GDI+ font-links inside the BMP, so a heavy check and a heart
// already draw correctly through the ordinary path; routing them through the emoji face as well would
// change glyphs that were never broken, and would do it on every notification that contains a tick.
//
// Emoji are written as ConvertFromUtf32 rather than \uXXXX escapes on purpose: this file has to stay ASCII,
// and an editor that resolves an escape while saving puts the raw character back without saying so.
public class FxEmojiTests
{
    private static string Cp(int codepoint) => char.ConvertFromUtf32(codepoint);

    [Fact]
    public void Plain_text_has_no_emoji_runs()
    {
        Assert.Empty(Fx.EmojiRuns(""));
        Assert.Empty(Fx.EmojiRuns("just a notification body"));
    }

    // Measured with GetGlyphIndicesW: Segoe UI lacks all four of these, and all four still draw, because
    // Uniscribe links them. Claiming them here would be a regression dressed as a fix.
    [Theory]
    [InlineData(0x2705)]   // white heavy check mark
    [InlineData(0x2764)]   // heavy black heart, on its own
    [InlineData(0x26A0)]   // warning sign
    [InlineData(0x2B50)]   // star
    public void A_bmp_symbol_that_font_links_is_left_alone(int codepoint)
        => Assert.Empty(Fx.EmojiRuns("ok " + Cp(codepoint) + " done"));

    [Fact]
    public void An_astral_emoji_is_one_run_at_its_own_index()
    {
        var runs = Fx.EmojiRuns("hi " + Cp(0x1F600));
        var (start, length) = Assert.Single(runs);
        Assert.Equal(3, start);
        Assert.Equal(2, length);   // one surrogate pair
    }

    // The heart draws fine and the variation selector after it does not - it is invisible by intent and
    // still lands on .notdef. Taking the cluster whole is what stops a box appearing beside a good glyph.
    [Fact]
    public void A_variation_selector_pulls_its_base_character_in_with_it()
    {
        var runs = Fx.EmojiRuns(Cp(0x2764) + Cp(0xFE0F));
        var (start, length) = Assert.Single(runs);
        Assert.Equal(0, start);
        Assert.Equal(2, length);
    }

    // Splitting these at the codepoint is the failure this test exists for: a family drawn as four separate
    // people, a flag drawn as two letters, a thumb drawn beside a colour swatch.
    [Fact]
    public void A_zwj_sequence_is_a_single_run()
    {
        string family = Cp(0x1F468) + Cp(0x200D) + Cp(0x1F469) + Cp(0x200D) + Cp(0x1F467);
        var (start, length) = Assert.Single(Fx.EmojiRuns(family));
        Assert.Equal(0, start);
        Assert.Equal(family.Length, length);
    }

    [Fact]
    public void A_regional_indicator_pair_is_a_single_run()
    {
        string flag = Cp(0x1F1EE) + Cp(0x1F1F7);
        var (_, length) = Assert.Single(Fx.EmojiRuns(flag));
        Assert.Equal(4, length);   // two surrogate pairs, one flag
    }

    [Fact]
    public void A_skin_tone_modifier_stays_with_the_hand()
    {
        string thumb = Cp(0x1F44D) + Cp(0x1F3FD);
        var (_, length) = Assert.Single(Fx.EmojiRuns(thumb));
        Assert.Equal(4, length);
    }

    [Fact]
    public void Several_emoji_are_reported_in_order_with_the_text_between_them_skipped()
    {
        string s = Cp(0x1F525) + " build " + Cp(0x1F44D) + " ok";
        var runs = Fx.EmojiRuns(s);
        Assert.Equal(2, runs.Count);
        Assert.Equal((0, 2), runs[0]);
        Assert.Equal((9, 2), runs[1]);
        // and the run really is the emoji, not an off-by-one into the space beside it
        Assert.Equal(Cp(0x1F525), s.Substring(runs[0].Start, runs[0].Length));
        Assert.Equal(Cp(0x1F44D), s.Substring(runs[1].Start, runs[1].Length));
    }
}
