using System;
using System.Collections.Generic;
using Halo.ClaudeCode;
using Halo.Widgets;

namespace Halo.Tests;

// The ask banner used to lay itself out from constants, so a description longer than one line was
// ellipsised away while the banner had the whole screen above it to grow into. Rows now measure their own
// text. Nothing here checks pixels; it checks the properties that go wrong when geometry is derived from
// text: that a row grows for its content, that the rects Draw paints are the rects the hit-test clicks,
// and that recomputing every frame stays cheap.
// Serialised with the other classes that touch the language: the two built-in rows are measured from
// translated text now, and Strings.Use switches it for the whole process.
[Collection("locale")]
public class AskBannerLayoutTests
{
    private static PendingAsk Ask(params AskOption[] options)
        => new("n", 1, "s", "AskUserQuestion", null, "pick one", options,
            DateTimeOffset.UtcNow.AddMinutes(10));

    private static readonly string Long =
        "no code yet - sit on the profiler until the regression names itself, which is the option "
        + "that costs a day and saves three, and then some more words to force a third line";

    [Fact]
    public void A_row_with_a_long_description_is_taller_than_one_with_a_short_one()
    {
        var rows = AskBanner.Layout(Ask(
            new AskOption("short", "the CPU one"),
            new AskOption("long", Long)), AskBanner.W).Rows;

        Assert.True(rows[1].Rect.Height > rows[0].Rect.Height,
            $"long row {rows[1].Rect.Height} should exceed short row {rows[0].Rect.Height}");
    }

    [Fact]
    public void A_description_gets_the_lines_the_row_grew_for()
    {
        var rows = AskBanner.Layout(Ask(new AskOption("long", Long)), AskBanner.W).Rows;

        // the point of growing the row is that the text is drawn, not trimmed: one line's worth of height
        // would mean the row grew for nothing
        Assert.True(rows[0].Desc.Height > rows[0].Label.Height);
        Assert.True(rows[0].Desc.Bottom <= rows[0].Rect.Bottom);
    }

    [Fact]
    public void An_option_with_no_description_still_gets_a_clickable_row()
    {
        var rows = AskBanner.Layout(Ask(new AskOption("allow", "")), AskBanner.W).Rows;

        Assert.Equal(0f, rows[0].Desc.Height);
        Assert.True(rows[0].Rect.Height >= 40f);
    }

    // Halo's own two rows store a MARKER where their text used to be - Words() resolves what is drawn, so a
    // language change reaches them without breaking IsFreeText, which is a reference comparison. Layout has
    // to measure that same resolved text. Reading the stored value here instead shipped in v4.0.0: both rows
    // measured as having no description, reserved no height for one, and drew none - visibly shorter than
    // the agent's own options in --render-ask.
    [Fact]
    public void The_rows_Halo_adds_itself_reserve_room_for_their_sub_label()
    {
        var rows = AskBanner.Layout(Ask(new AskOption("one", "the CPU one")), AskBanner.W).Rows;

        Assert.Equal(3, rows.Count);
        Assert.True(AskBanner.IsFreeText(rows[1].Option));
        Assert.True(AskBanner.IsChat(rows[2].Option));
        Assert.All(rows, row => Assert.True(row.Desc.Height > 0f,
            "every row here has a sub-label, so every row must have reserved height for one"));
    }

    [Fact]
    public void Words_resolves_the_built_ins_and_leaves_the_agent_s_own_text_alone()
    {
        foreach (var (built, labelKey, subKey) in new[]
                 {
                     (AskBanner.FreeText, "ask.typeSomething", "ask.freeTextSub"),
                     (AskBanner.Chat, "ask.chat", "ask.chatSub"),
                 })
        {
            var (Label, Sub) = AskBanner.Words(built);
            Assert.False(string.IsNullOrWhiteSpace(Label));
            Assert.False(string.IsNullOrWhiteSpace(Sub));
            // Get falls through active -> English -> THE KEY ITSELF, so "not empty" stays green with every
            // ask.* key deleted and the banner would ship raw dotted key names in all eight languages.
            // These two assertions are the ones that fail in that case.
            Assert.NotEqual(labelKey, Label);
            Assert.NotEqual(subKey, Sub);
            // and never the identity marker the option stores
            Assert.DoesNotContain("__", Label);
        }

        // an agent's own option is not ours to translate
        Assert.Equal(("allow", "run it"), AskBanner.Words(new AskOption("allow", "run it")));
    }

    // the memo is keyed on the ask instance, which does not change when the language does
    [Fact]
    public void Switching_language_re_measures_instead_of_serving_the_old_heights()
    {
        var ask = Ask(new AskOption("one", "the CPU one"));
        try
        {
            Halo.Localization.Strings.Use("English");
            var english = AskBanner.Layout(ask, AskBanner.W);
            string englishLabel = AskBanner.Words(AskBanner.FreeText).Label;

            // the same language must still be served from the memo, or this proves nothing about the key
            Assert.Same(english, AskBanner.Layout(ask, AskBanner.W));

            Halo.Localization.Strings.Use("Persian");
            // guards the vacuous pass: with no fa.json beside the assembly the text would be identical and
            // only the object identity would differ, so the assertion below would mean nothing
            Assert.NotEqual(englishLabel, AskBanner.Words(AskBanner.FreeText).Label);
            Assert.NotSame(english, AskBanner.Layout(ask, AskBanner.W));
        }
        finally { Halo.Localization.Strings.Use("English"); }
    }

    [Fact]
    public void Rows_stack_without_overlapping_and_stay_inside_the_banner()
    {
        var layout = AskBanner.Layout(Ask(
            new AskOption("one", "short"),
            new AskOption("two", Long),
            new AskOption("three", "")), AskBanner.W);

        Assert.True(layout.Rows[0].Rect.Top > layout.Title.Bottom);
        for (int i = 1; i < layout.Rows.Count; i++)
            Assert.True(layout.Rows[i].Rect.Top >= layout.Rows[i - 1].Rect.Bottom,
                $"row {i} starts at {layout.Rows[i].Rect.Top}, above row {i - 1}'s bottom");
        Assert.True(layout.Rows[^1].Rect.Bottom < layout.Height);
    }

    // the number sits outside its option's glass, and clicking it has to count as clicking the option -
    // which only holds while the hit-test rect is the wider one
    [Fact]
    public void The_hit_test_rect_covers_the_number_as_well_as_the_body()
    {
        var ask = Ask(new AskOption("allow", "run it"));
        var row = AskBanner.Layout(ask, AskBanner.W).Rows[0];

        Assert.True(row.Rect.X < row.Body.X);
        Assert.Equal(row.Rect.Right, row.Body.Right, precision: 3);
        Assert.Equal(row.Rect.Height, row.Body.Height, precision: 3);
    }

    [Fact]
    public void Chips_and_Height_agree_with_the_layout_they_are_read_from()
    {
        var ask = Ask(new AskOption("one", "short"), new AskOption("two", Long));
        var layout = AskBanner.Layout(ask, AskBanner.W);
        var chips = AskBanner.Chips(ask, AskBanner.W);

        Assert.Equal(layout.Height, AskBanner.Height(ask, AskBanner.W));
        Assert.Equal(layout.Rows.Count, chips.Count);
        for (int i = 0; i < chips.Count; i++)
        {
            Assert.Equal(layout.Rows[i].Rect, chips[i].Rect);
            Assert.Same(layout.Rows[i].Option, chips[i].Option);
        }
    }

    // callers recompute layout every frame on purpose, rather than caching a height that can drift out of
    // step with what Draw paints; the memo is what keeps that affordable
    [Fact]
    public void Layout_is_memoised_per_ask_and_width()
    {
        var ask = Ask(new AskOption("one", "short"));

        Assert.Same(AskBanner.Layout(ask, AskBanner.W), AskBanner.Layout(ask, AskBanner.W));
        Assert.NotSame(AskBanner.Layout(ask, AskBanner.W), AskBanner.Layout(ask, AskBanner.W - 40));
    }

    // Claude Code's own question box carries a free-text field one row past the last option, so the banner
    // carries the same row. It is appended here rather than sent by the hook, which forwards only what the
    // tool offered - and what pins it is that it is there for a question, absent for a permission (a hook
    // decision is a word, not a sentence), and always last, because the number of options is exactly how
    // far the pill has to walk down to reach the field.
    [Fact]
    public void A_question_gets_both_rows_appended_after_the_options()
    {
        var rows = AskBanner.Layout(Ask(new AskOption("one", "a"), new AskOption("two", "b")),
            AskBanner.W).Rows;

        Assert.Equal(4, rows.Count);
        Assert.True(AskBanner.IsFreeText(rows[2].Option));
        Assert.True(AskBanner.IsChat(rows[3].Option));
        Assert.False(AskBanner.IsBuiltIn(rows[0].Option));
    }

    // The order is the box's, and it is what the row numbers are derived from - free text at N+1, chat at
    // N+2. Swapping them here would send every answer to the wrong row without failing a single assertion
    // about counts.
    [Fact]
    public void The_free_text_row_comes_before_the_chat_row()
    {
        var rows = AskBanner.Layout(Ask(new AskOption("one", "a")), AskBanner.W).Rows;

        Assert.True(AskBanner.IsFreeText(rows[1].Option));
        Assert.True(AskBanner.IsChat(rows[2].Option));
    }

    // Reference identity, not the words: a real option is allowed to say the same thing, and delivering it
    // as a built-in would answer with a row the user did not pick.
    [Fact]
    public void An_option_that_copies_a_built_in_label_is_still_an_option()
    {
        var impostor = new AskOption("Chat about this", "say it in your own words");

        Assert.False(AskBanner.IsChat(impostor));
        Assert.False(AskBanner.IsBuiltIn(impostor));
        Assert.True(AskBanner.IsBuiltIn(AskBanner.Chat));
        Assert.True(AskBanner.IsBuiltIn(AskBanner.FreeText));
    }

    // A question whose options carry previews is drawn by a different component in the box - a Notes field
    // and no numbered rows past the options - so appending them would be inventing rows that are not there,
    // and every number after them would be wrong.
    [Fact]
    public void A_question_with_previews_gets_no_built_in_rows()
    {
        var withPreview = new PendingAsk("n", 1, "s", "AskUserQuestion", null, "pick one",
            [new AskOption("a", ""), new AskOption("b", "")],
            DateTimeOffset.UtcNow.AddMinutes(10), MultiSelect: false, HasPreview: true);

        var rows = AskBanner.Layout(withPreview, AskBanner.W).Rows;

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => AskBanner.IsBuiltIn(r.Option));
    }

    [Fact]
    public void A_permission_ask_gets_no_built_in_rows()
    {
        var permission = new PendingAsk("n", 1, "s", "Bash", "git push", null,
            [new AskOption("allow", "run it"), new AskOption("deny", "skip it")],
            DateTimeOffset.UtcNow.AddMinutes(10));

        var rows = AskBanner.Layout(permission, AskBanner.W).Rows;

        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain(rows, r => AskBanner.IsBuiltIn(r.Option));
    }

    [Fact]
    public void A_permission_ask_reserves_a_line_for_its_target()
    {
        var opts = new List<AskOption> { new("allow", "run it"), new("deny", "skip it") };
        var withTarget = new PendingAsk("n", 1, "s", "Bash", "git push --force-with-lease origin master",
            null, opts, DateTimeOffset.UtcNow.AddMinutes(10));
        var without = new PendingAsk("n", 1, "s", "Bash", null, null, opts,
            DateTimeOffset.UtcNow.AddMinutes(10));

        Assert.True(AskBanner.Layout(withTarget, AskBanner.W).Target.Height > 0f);
        Assert.Equal(0f, AskBanner.Layout(without, AskBanner.W).Target.Height);
        Assert.True(AskBanner.Height(withTarget, AskBanner.W) > AskBanner.Height(without, AskBanner.W));
    }
}
