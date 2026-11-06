using System;
using Halo.Shell;
using Xunit;

namespace Halo.Tests;

public class TabMatchTests
{
    // the measured shape of a real Claude Code tab title: a spinner glyph, a space, then the session's own
    // summary of what it is doing - in this case Persian, written here as escapes per the ASCII rule.
    // "\u0648\u06CC\u0631\u0627\u06CC\u0634 \u067E\u0631\u0648\u0698\u0647" = "editing project".
    private const string Spinner = "\u2810 ";
    private const string Persian = "\u0648\u06CC\u0631\u0627\u06CC\u0634 \u067E\u0631\u0648\u0698\u0647";

    [Fact]
    public void Finds_the_tab_whose_title_was_recorded()
    {
        var tabs = new[] { "npm run dev", Spinner + Persian, "pwsh" };
        Assert.Equal(1, TabMatch.Find(Spinner + Persian, tabs));
    }

    [Fact]
    public void Spinner_may_differ_between_the_recording_and_the_tab()
    {
        // the whole reason titles are cleaned: the glyph cycles every frame, so the recorded one is almost
        // never the one on screen when reveal runs
        var tabs = new[] { "pwsh", "\u28F6 " + Persian };
        Assert.Equal(1, TabMatch.Find("\u2810 " + Persian, tabs));
    }

    [Fact]
    public void No_match_returns_minus_one_rather_than_a_guess()
    {
        var tabs = new[] { "npm run dev", "pwsh" };
        Assert.Equal(-1, TabMatch.Find(Spinner + Persian, tabs));
    }

    [Fact]
    public void Two_tabs_with_the_same_title_are_not_confident()
    {
        var tabs = new[] { Spinner + Persian, "pwsh", "\u28F6 " + Persian };
        Assert.Equal(-1, TabMatch.Find(Spinner + Persian, tabs));
    }

    [Fact]
    public void A_long_shared_head_still_matches_when_the_tail_has_moved_on()
    {
        // the recording is seconds stale at worst, so the session may have rewritten the end of its title
        var tabs = new[] { "pwsh", "building the installer for arm64" };
        Assert.Equal(1, TabMatch.Find("building the installer for x64", tabs));
    }

    [Fact]
    public void A_short_shared_head_is_not_enough()
    {
        // every tab of the same tool starts the same way; this is the failure being replaced, so it must
        // not come back through the prefix arm
        var tabs = new[] { "claude code alpha", "claude code beta" };
        Assert.Equal(-1, TabMatch.Find("claude code gamma", tabs));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u2810")]     // a spinner and nothing else cleans away to nothing
    public void Nothing_worth_matching_returns_minus_one(string? recorded)
    {
        Assert.Equal(-1, TabMatch.Find(recorded, new[] { "pwsh", Spinner + Persian }));
    }

    [Fact]
    public void No_tabs_returns_minus_one()
    {
        Assert.Equal(-1, TabMatch.Find(Spinner + Persian, Array.Empty<string?>()));
        Assert.Equal(-1, TabMatch.Find(Spinner + Persian, null!));
    }

    [Fact]
    public void Punctuation_and_case_do_not_decide_it()
    {
        var tabs = new[] { "pwsh", "Halo - Build:  Release" };
        Assert.Equal(1, TabMatch.Find("halo   build release", tabs));
    }

    [Fact]
    public void A_null_tab_name_is_skipped_not_matched()
    {
        var tabs = new string?[] { null, Spinner + Persian };
        Assert.Equal(1, TabMatch.Find(Spinner + Persian, tabs));
        Assert.Equal(-1, TabMatch.Find(null, tabs));
    }
}
