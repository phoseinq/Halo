using Halo.Launcher;

namespace Halo.Tests;

public sealed class ReminderStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FormatAndParse_RoundTrip()
    {
        var r = new Reminder("abc123", Now, "walk the dog");
        var back = ReminderStore.Parse(ReminderStore.Format(r));

        Assert.NotNull(back);
        Assert.Equal(r.Id, back!.Id);
        Assert.Equal(r.Text, back.Text);
        Assert.Equal(r.When.ToUnixTimeSeconds(), back.When.ToUnixTimeSeconds());
    }

    [Fact]
    public void Format_FlattensNewlines_SoOneRecordStaysOneLine()
    {
        var line = ReminderStore.Format(new Reminder("id", Now, "two\r\nlines"));
        Assert.DoesNotContain("\n", line);
        Assert.DoesNotContain("\r", line);
    }

    [Fact]
    public void Parse_KeepsPipesInTheText()
    {
        // the split is on the FIRST two pipes only, so the message may contain them
        var back = ReminderStore.Parse("id|1756036800|a | b | c");
        Assert.Equal("a | b | c", back!.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-pipes-here")]
    [InlineData("id|onlyone")]
    [InlineData("id|notanumber|text")]
    [InlineData("|1756036800|text")]
    [InlineData("id|1756036800|")]
    public void Parse_RejectsAnythingItCannotUse(string? line)
        => Assert.Null(ReminderStore.Parse(line));

    [Theory]
    [InlineData("in 20m walk the dog", 20 * 60, "walk the dog")]
    [InlineData("in 2h stretch", 2 * 3600, "stretch")]
    [InlineData("in 90s tea", 90, "tea")]
    [InlineData("IN 5M shout", 5 * 60, "shout")]
    // The forms a person actually types, and every one of them used to be refused. "in 20 minutes" is not
    // an exotic phrasing - it is the obvious one, and the single-letter-only syntax rejected it.
    [InlineData("in 20 minutes walk the dog", 20 * 60, "walk the dog")]
    [InlineData("in 20 min walk the dog", 20 * 60, "walk the dog")]
    [InlineData("in 1 hour stretch", 3600, "stretch")]
    [InlineData("in 1h30m tea", 90 * 60, "tea")]
    [InlineData("in 1h 30m tea", 90 * 60, "tea")]
    [InlineData("in 2 hours 15 minutes stretch", 2 * 3600 + 15 * 60, "stretch")]
    [InlineData("in 2 days renew it", 2 * 86400, "renew it")]
    public void ParseCommand_ReadsARelativeTime(string input, int seconds, string text)
    {
        var got = ReminderStore.ParseCommand(input, Now, out string? complaint);
        Assert.NotNull(got);
        Assert.Null(complaint);
        Assert.Equal(seconds, (int)(got!.Value.When - Now).TotalSeconds);
        Assert.Equal(text, got.Value.Text);
    }

    // A reminder's natural form is a CLOCK TIME, and there was no way to say one at all.
    [Theory]
    [InlineData("at 17:30 call mum", 17, 30)]
    [InlineData("at 5pm call mum", 17, 0)]
    [InlineData("at 5:30pm call mum", 17, 30)]
    [InlineData("at 5 pm call mum", 17, 0)]
    [InlineData("at 11pm call mum", 23, 0)]
    [InlineData("AT 21:05 call mum", 21, 5)]
    public void ParseCommand_ReadsAClockTimeLaterToday(string input, int hour, int minute)
    {
        // Now is 12:00, so every one of these is still ahead
        var got = ReminderStore.ParseCommand(input, Now, out string? complaint);
        Assert.NotNull(got);
        Assert.Null(complaint);
        Assert.Equal(Now.Date, got!.Value.When.Date);
        Assert.Equal(hour, got.Value.When.Hour);
        Assert.Equal(minute, got.Value.When.Minute);
        Assert.Equal("call mum", got.Value.Text);
    }

    [Fact]
    public void AClockTimeThatHasPassed_RollsToTomorrow_RatherThanFiringAtOnce()
    {
        // 09:00 against a 12:00 clock. The alternative is a reminder that is already due the instant it is
        // set, which is the one behaviour nobody types this expecting.
        var got = ReminderStore.ParseCommand("at 9am stand up", Now, out _);
        Assert.NotNull(got);
        Assert.Equal(Now.Date.AddDays(1), got!.Value.When.Date);
        Assert.Equal(9, got.Value.When.Hour);
    }

    [Fact]
    public void Tomorrow_TakesAClockOnTheNextDay()
    {
        var got = ReminderStore.ParseCommand("tomorrow 9am dentist", Now, out string? complaint);
        Assert.NotNull(got);
        Assert.Null(complaint);
        Assert.Equal(Now.Date.AddDays(1), got!.Value.When.Date);
        Assert.Equal(9, got.Value.When.Hour);
        Assert.Equal("dentist", got.Value.Text);
    }

    [Fact]
    public void ParseCommand_IgnoresTextThatIsNotACommandAtAll()
    {
        // no complaint either: the user was filtering, not addressing the parser
        Assert.Null(ReminderStore.ParseCommand("dentist", Now, out string? complaint));
        Assert.Null(complaint);
    }

    [Theory]
    [InlineData("in ")]
    [InlineData("in soon")]
    [InlineData("in 20")]
    [InlineData("in 20x thing")]
    [InlineData("in 20m")]
    [InlineData("in 0m nothing")]
    [InlineData("at ")]
    [InlineData("at teatime")]
    [InlineData("at 25:00 nope")]
    [InlineData("at 17:30")]
    [InlineData("tomorrow")]
    [InlineData("tomorrow lunch")]
    public void ParseCommand_ExplainsItselfWhenTheShapeIsNearlyRight(string input)
    {
        Assert.Null(ReminderStore.ParseCommand(input, Now, out string? complaint));
        Assert.False(string.IsNullOrWhiteSpace(complaint));
    }

    // A bare number is only part of a duration when a unit follows it. Without this rule "in 20m 5 things"
    // eats the 5 and reminds you about "things".
    [Fact]
    public void ABareNumberInTheMessage_IsNotEatenAsPartOfTheDuration()
    {
        var got = ReminderStore.ParseCommand("in 20m 5 things to do", Now, out _);
        Assert.NotNull(got);
        Assert.Equal(20 * 60, (int)(got!.Value.When - Now).TotalSeconds);
        Assert.Equal("5 things to do", got.Value.Text);
    }

    [Fact]
    public void DueAndPending_SplitOnTheClock_AndTogetherLoseNothing()
    {
        Reminder[] all =
        [
            new("a", Now.AddMinutes(-5), "past"),
            new("b", Now, "exactly now"),
            new("c", Now.AddMinutes(5), "future"),
        ];

        var due = ReminderStore.Due(all, Now);
        var pending = ReminderStore.Pending(all, Now);

        Assert.Equal(["past", "exactly now"], due.Select(r => r.Text));
        Assert.Equal(["future"], pending.Select(r => r.Text));
        Assert.Equal(all.Length, due.Count + pending.Count);
    }

    [Fact]
    public void Pending_ComesBackInTimeOrder()
    {
        Reminder[] all =
        [
            new("c", Now.AddHours(3), "third"),
            new("a", Now.AddHours(1), "first"),
            new("b", Now.AddHours(2), "second"),
        ];

        Assert.Equal(["first", "second", "third"],
                     ReminderStore.Pending(all, Now).Select(r => r.Text));
    }

    // ---- the when-menu -------------------------------------------------------------------------------

    [Fact]
    public void Choices_OfferSomethingSoonAndSomethingTomorrow()
    {
        var c = ReminderStore.Choices(Now);
        Assert.All(c, x => Assert.True(x.When > Now, $"{x.Label} was not in the future"));
        Assert.Contains(c, x => x.When - Now < TimeSpan.FromHours(1));
        Assert.Contains(c, x => x.When.Date == Now.Date.AddDays(1));
        Assert.Equal(c.Length, c.Select(x => x.Label).Distinct().Count());
    }

    [Fact]
    public void ThisEvening_IsOfferedWhileItIsStillAhead()
    {
        // Now is noon
        var c = ReminderStore.Choices(Now);
        var evening = Assert.Single(c, x => x.Label == "this evening");
        Assert.Equal(18, evening.When.Hour);
        Assert.Equal(Now.Date, evening.When.Date);
    }

    [Fact]
    public void ThisEvening_IsDroppedOnceItHasGone_RatherThanMeaningTomorrow()
    {
        // 23:00. An "evening" row that silently meant TOMORROW evening is exactly the quietly-wrong
        // reminder this whole file is shaped to avoid.
        var late = new DateTimeOffset(2026, 8, 24, 23, 0, 0, TimeSpan.Zero);
        var c = ReminderStore.Choices(late);
        Assert.DoesNotContain(c, x => x.Label == "this evening");
        Assert.All(c, x => Assert.True(x.When > late));
    }

    [Fact]
    public void TomorrowsRowsAlwaysStand_BecauseTomorrowAlwaysHasAMorningLeft()
    {
        var late = new DateTimeOffset(2026, 8, 24, 23, 59, 0, TimeSpan.Zero);
        var c = ReminderStore.Choices(late);
        Assert.Contains(c, x => x.Label == "tomorrow morning" && x.When.Hour == 9);
        Assert.Contains(c, x => x.Label == "tomorrow evening" && x.When.Hour == 18);
    }
}
