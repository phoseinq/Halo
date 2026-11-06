using Halo.Agents;
using Halo.Widgets;

namespace Halo.Tests;

// The 30s hold after a turn, and the pill's words for it: done + how long, or the permission being asked.
public class TurnRecapTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Linger_holds_thirty_seconds_after_the_last_working_frame()
    {
        var l = new DoneLinger();
        Assert.False(l.Holds(false, 0));            // never worked: nothing to hold
        Assert.True(l.Holds(true, 1_000));
        Assert.True(l.Holds(false, 2_000));         // the edge
        Assert.True(l.Holds(false, 2_000 + DoneLinger.Ms - 1));
        Assert.False(l.Holds(false, 2_000 + DoneLinger.Ms));
    }

    [Fact]
    public void A_finished_turn_says_done_and_how_long_it_took()
    {
        var r = new TurnRecap();
        string start = T0.ToString("o");
        r.Observe("working", start, T0.AddSeconds(5), 5_000, null);
        Assert.Null(r.Line(5_000));
        r.Observe("idle", null, T0.AddSeconds(208), 208_000, null);
        Assert.Equal(("done :)", "3m 28s"), r.Line(210_000));
        Assert.Null(r.Line(208_000 + DoneLinger.Ms));
    }

    [Fact]
    public void An_interrupted_turn_says_stopped()
    {
        var r = new TurnRecap();
        string start = T0.ToString("o");
        r.Observe("working", start, T0, 0, null);
        r.Observe("idle", null, T0.AddSeconds(12), 12_000, start);
        Assert.Equal(("stopped", "12s"), r.Line(12_000));
    }

    [Fact]
    public void A_question_mid_turn_is_not_the_end_of_it()
    {
        var r = new TurnRecap();
        r.Observe("working", T0.ToString("o"), T0, 0, null);
        r.Observe("waiting_input", T0.ToString("o"), T0.AddSeconds(3), 3_000, null);
        Assert.Null(r.Line(3_000));
    }

    [Theory]
    [InlineData("Claude needs your permission to use Bash", "allow Bash?")]
    [InlineData("Claude needs your permission to use mcp__serena__find_symbol", "allow serena?")]
    [InlineData("Claude is waiting for your input", null)]
    [InlineData(null, null)]
    public void A_permission_prompt_names_the_tool(string? message, string? expected)
        => Assert.Equal(expected, TurnRecap.AskLine(message));

    [Theory]
    [InlineData("committing\u2026", "committing")]
    [InlineData("still running...", "still running")]
    [InlineData("context 34% \u00B7 5h 61%", "context 34%, 5h 61%")]
    [InlineData("\u2026", "\u2026")]
    public void The_pill_shows_no_dots(string line, string expected)
        => Assert.Equal(expected, PlainWords.NoDots(line));

    [Theory]
    [InlineData("git commit", "committing")]
    [InlineData("git status", "checking git")]
    [InlineData("dotnet test", "running tests")]
    [InlineData("dotnet build", "building")]
    [InlineData("sed", "editing text")]
    [InlineData("npm.cmd install", "installing packages")]
    [InlineData("git frobnicate", "using git")]
    [InlineData("unknowntool", null)]
    public void Programs_read_as_plain_words(string target, string? expected)
        => Assert.Equal(expected, PlainWords.ForCommand(target));

    [Fact]
    public void A_toolbox_carries_its_subcommand()
    {
        Assert.Equal("git commit", PlainWords.WithSubcommand("git", "git -C repo commit -m x"));
        Assert.Equal("dotnet test", PlainWords.WithSubcommand("dotnet", "dotnet test tests/x.csproj"));
        Assert.Equal("sed", PlainWords.WithSubcommand("sed", "sed -i s/a/b/ f"));
    }

    [Fact]
    public void The_pill_says_what_the_step_does()
    {
        Assert.Equal("committing\u2026", Moods.Fact("running", "git commit"));
        Assert.Equal("running ffprobe\u2026", Moods.Fact("running", "ffprobe"));
        Assert.Equal("searching code\u2026", Moods.Fact("digging", "Ring|Context"));
        Assert.Equal("searching LiveContext\u2026", Moods.Fact("digging", "LiveContext"));
        Assert.Equal("finding files\u2026", Moods.Fact("digging", "**/*.cs"));
    }

    [Fact]
    public void At_rest_the_pill_shows_context_and_budget()
    {
        Assert.Equal("context 34%, 5h 61%", TurnRecap.Idle(0.34f, 0.61f, "5h"));
        Assert.Equal("context 34% full", TurnRecap.Idle(0.34f, 0.61f, "5h", maxChars: 17));   // too tight: context only
        Assert.Equal("week 20%", TurnRecap.Idle(-1f, 0.2f, "week"));
        Assert.Null(TurnRecap.Idle(-1f, -1f, "5h"));
    }
}
