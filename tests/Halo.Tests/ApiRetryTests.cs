using System;
using Halo.ClaudeCode;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The pill reported a session as IDLE while it was retrying an API call for minutes. Every piece of that is
// pinned here: what the terminal line means, and the rule that used to call the turn over.
public class ApiRetryTests
{
    // The line as Claude Code actually draws it, reported from a live session. Built by code point, not
    // written as a glyph or an escape: source here stays ASCII and an editor that resolves an escape puts
    // the real character back the moment the line is saved - the same reason CompactProgress spells its
    // bar characters this way. 0x2733 is the spinner asterisk, 0x00b7 the dots between the clauses.
    private static readonly string Dot = " " + (char)0x00b7 + " ";
    private static readonly string RealLine =
        (char)0x2733 + " Waiting for API response" + Dot + "will retry in 1m 38s" + Dot + "check your network";

    [Fact]
    public void Reads_the_countdown_off_the_line_the_agent_draws()
        => Assert.Equal(98, ApiRetry.RetryIn(RealLine));

    // the wording around it is not the contract - only "retry in <time>" is, so a reworded build still reads
    [Theory]
    [InlineData("retrying in 5s", 5)]
    [InlineData("Retrying in 12 seconds", 12)]
    [InlineData("will retry in 2m", 120)]
    [InlineData("  API Error (Overloaded) - retrying in 1m 5s", 65)]
    public void Reads_the_shapes_the_countdown_comes_in(string line, int expected)
        => Assert.Equal(expected, ApiRetry.RetryIn(line));

    // A bare "retry in" is what a wrapped or half-drawn line leaves on screen. Parsing it as zero would put
    // a "0s" countdown on the pill and, worse, assert a retry that may not be happening.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nothing to see here")]
    [InlineData("will retry in ")]
    [InlineData("retry in soon")]
    public void Refuses_a_line_that_carries_no_figure(string? line)
        => Assert.Null(ApiRetry.RetryIn(line));

    // absurd readings are a line that happened to look like one, not a real backoff
    [Fact]
    public void Refuses_a_backoff_no_cli_would_wait()
        => Assert.Null(ApiRetry.RetryIn("retry in 999m"));

    [Theory]
    [InlineData(5, "5s")]
    [InlineData(98, "1m 38s")]
    [InlineData(120, "2m")]
    [InlineData(-1, "")]
    public void Spells_the_countdown_the_way_the_cli_does(int seconds, string expected)
        => Assert.Equal(expected, ApiRetry.Caption(seconds));

    // THE bug. A turn with no tool name that has gone quiet is treated as over, because an interrupt fires
    // no hook and leaves exactly that trace - but so does a retry loop, and that session is alive.
    [Fact]
    public void A_quiet_turn_is_over_unless_it_is_retrying()
    {
        var now = DateTimeOffset.UtcNow;
        var st = new CcStatus
        {
            State = "working",
            Pid = 4242,
            StartedAt = now.AddMinutes(-20).ToString("o"),
            UpdatedAt = now.AddMinutes(-10).ToString("o"),   // long past SettleAfterSeconds
        };

        Assert.True(ClaudeCodeWidget.TurnOver(st, now));
        Assert.False(ClaudeCodeWidget.TurnOver(st, now, retrying: true));
    }

    // a tool that is genuinely running still wins - the backstop was only ever meant for the thinking gap
    [Fact]
    public void A_running_tool_is_never_called_over()
    {
        var now = DateTimeOffset.UtcNow;
        var st = new CcStatus
        {
            State = "working", Pid = 1, CurrentTool = "Bash",
            UpdatedAt = now.AddMinutes(-10).ToString("o"),
        };
        Assert.False(ClaudeCodeWidget.TurnOver(st, now));
    }

    [Fact]
    public void A_status_with_no_stamp_is_not_quiet_it_is_unknown()
        => Assert.Equal(-1, ClaudeCodeWidget.QuietFor(new CcStatus { State = "working" }, DateTimeOffset.UtcNow));

    // the figure is per-session: a reading taken for one pid must not describe another
    [Fact]
    public void A_reading_belongs_to_the_session_it_was_taken_for()
    {
        try
        {
            ApiRetry.Track(0);
            Assert.False(ApiRetry.LiveFor(0));
            Assert.False(ApiRetry.LiveFor(1234));
        }
        finally { ApiRetry.Track(0); }
    }

    // The window is bounded at BOTH ends: an Esc'd turn sits on "working" with no tool for the life of the
    // process, and an open-ended rule would attach to that terminal once a second forever.
    [Theory]
    [InlineData(3, false, false)]     // an ordinary thinking gap - not worth a console attach
    [InlineData(30, false, true)]     // quiet long enough to be worth asking about
    [InlineData(600, false, false)]   // past the backstop with nothing found: the guess has been made
    [InlineData(600, true, true)]     // except a retry was found, which proves it is still alive
    public void Only_watches_a_terminal_while_the_answer_is_still_worth_having(
        int quietSeconds, bool retrying, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        var st = new CcStatus
        {
            State = "working", Pid = 7,
            UpdatedAt = now.AddSeconds(-quietSeconds).ToString("o"),
        };
        Assert.Equal(expected, ClaudeCodeWidget.WatchForRetry(st, now, retrying));
    }
}
