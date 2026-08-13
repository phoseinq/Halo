using Halo.ClaudeCode;
using Xunit;

namespace Halo.Tests;

// A digit answers the plain single-select list and only MOVES the selection in the preview variant, whose own
// hint line names no digit at all. Both shapes below come from real --probe-console dumps taken while each
// component was on screen: the plain one closed inside the same second the pill typed into it, the preview one
// sat there selected until an Enter arrived by hand.
//
// The markers are escapes rather than characters for the same reason AskBoxReadTests' are: they are the
// parser's input, and an editor that helpfully rewrites one is a fixture that stops testing what it claims.
public class AskConfirmTests
{
    private const string Caret = "\u276F";   // the mark the box draws against the focused row
    private const string Tick = "\u2714";    // multi-select only: what sits between its brackets

    // The preview component: options on the left, the panel on the right, caret on row 1. The rows carry no
    // checkbox and no space after the dot, which is why a reader written for the multi-select box sees nothing
    // here at all.
    private static readonly string Preview = string.Join("\n",
        Caret + " 1.first option              +---------------------+",
        "  2.second option             | row 1               |",
        "  3.third option              |                     |",
        "                              +---------------------+",
        "  Chat about this",
        "Enter to select - up/down to navigate - n to add notes - Esc to cancel");

    // the same box after the pill's digit moved the selection to row 2, which is the state an Enter is owed
    private static readonly string PreviewOnRow2 = Preview
        .Replace(Caret + " 1.", "  1.")
        .Replace("  2.second", Caret + " 2.second");

    // answered: the list is gone and the prompt is back, so an Enter would land in whatever the user has
    // started typing there
    private static readonly string Closed = string.Join("\n",
        "* User answered Claude's questions:",
        "    second option -> which one did you pick?",
        "",
        Caret);

    // The whole bug in one table. A preview question is the only one the pill has to finish by hand.
    [Fact]
    public void Only_a_preview_question_is_owed_an_enter()
    {
        Assert.True(AskStore.WantsEnter(AskDelivery.Option, multiSelect: false, hasPreview: true));
        // the plain list already answered - an Enter here lands in the prompt behind the closed box
        Assert.False(AskStore.WantsEnter(AskDelivery.Option, multiSelect: false, hasPreview: false));
        // multiSelect commits through its own Submit tab, never through Enter
        Assert.False(AskStore.WantsEnter(AskDelivery.Option, multiSelect: true, hasPreview: true));
        // the free-text row sends its own Enter from ON the field, and the chat row has no numbered row to
        // sit on in the preview component at all
        Assert.False(AskStore.WantsEnter(AskDelivery.FreeText, multiSelect: false, hasPreview: true));
        Assert.False(AskStore.WantsEnter(AskDelivery.Chat, multiSelect: false, hasPreview: true));
        Assert.False(AskStore.WantsEnter(AskDelivery.Submit, multiSelect: true, hasPreview: false));
    }

    [Fact]
    public void The_row_the_digit_selected_is_owed_an_enter()
        => Assert.True(AskStore.NeedsConfirm(PreviewOnRow2, 2, multiSelect: false));

    [Fact]
    public void A_caret_still_sitting_on_row_one_confirms_row_one()
        => Assert.True(AskStore.NeedsConfirm(Preview, 1, multiSelect: false));

    // The digit went somewhere this cannot vouch for. Sending Enter anyway would answer the question with
    // whatever row the box is holding instead of the one that was clicked.
    [Fact]
    public void A_caret_on_another_row_sends_nothing()
        => Assert.False(AskStore.NeedsConfirm(PreviewOnRow2, 3, multiSelect: false));

    // The plain component answers on the digit alone - measured, the box was gone in the same second - so
    // there is nothing left to confirm and the Enter must not be sent.
    [Fact]
    public void A_box_that_has_already_closed_sends_nothing()
        => Assert.False(AskStore.NeedsConfirm(Closed, 1, multiSelect: false));

    [Fact]
    public void An_unreadable_console_sends_nothing()
    {
        Assert.False(AskStore.NeedsConfirm("", 1, multiSelect: false));
        Assert.False(AskStore.NeedsConfirm("   ", 2, multiSelect: false));
    }

    // Enter in the multi-select component acts on the focused row rather than committing - the failure
    // WalkToSubmit exists to avoid, and it must not come back through this door.
    [Fact]
    public void A_multi_select_is_never_confirmed_this_way()
    {
        string box = string.Join("\n",
            "  1. [ ] first",
            Caret + " 2. [" + Tick + "] second",
            "     Submit");
        Assert.False(AskStore.NeedsConfirm(box, 2, multiSelect: true));
        // and it stays refused when a caller passes the wrong flag: the bracket on the caret row is what the
        // component IS, whatever the payload claimed
        Assert.False(AskStore.NeedsConfirm(box, 2, multiSelect: false));
    }

    [Fact]
    public void A_row_number_below_one_is_not_a_row()
        => Assert.False(AskStore.NeedsConfirm(Preview, 0, multiSelect: false));
}
