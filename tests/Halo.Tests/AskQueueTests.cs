using Halo.ClaudeCode;

namespace Halo.Tests;

// Two sessions can be waiting at once. One banner at a time, FIFO, and an expired head must not sit
// there blocking a live question behind it - that is the failure that would make the queue worse than
// no queue at all.
public sealed class AskQueueTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    private static PendingAsk Ask(string nonce, DateTimeOffset expires) =>
        new(nonce, 1, "sess", "AskUserQuestion", null, "Which?",
            [new AskOption("A", "a"), new AskOption("B", "b")], expires);

    [Fact]
    public void EmptyQueueHasNoHead() => Assert.Null(new AskQueue().Head(T0));

    [Fact]
    public void HeadIsTheFirstSeen()
    {
        var q = new AskQueue();
        q.Observe(Ask("first", T0.AddSeconds(20)));
        q.Observe(Ask("second", T0.AddSeconds(20)));
        Assert.Equal("first", q.Head(T0)!.Nonce);
    }

    [Fact]
    public void AnsweringTheHeadPromotesTheNext()
    {
        var q = new AskQueue();
        q.Observe(Ask("first", T0.AddSeconds(20)));
        q.Observe(Ask("second", T0.AddSeconds(20)));
        q.Remove("first");
        Assert.Equal("second", q.Head(T0)!.Nonce);
    }

    // the one that matters: a question nobody answered must not hold the banner hostage
    [Fact]
    public void ExpiredHeadDoesNotBlockTheNext()
    {
        var q = new AskQueue();
        q.Observe(Ask("stale", T0.AddSeconds(5)));
        q.Observe(Ask("live", T0.AddSeconds(60)));
        Assert.Equal("live", q.Head(T0.AddSeconds(10))!.Nonce);
    }

    [Fact]
    public void EverythingExpiredMeansNoBanner()
    {
        var q = new AskQueue();
        q.Observe(Ask("a", T0.AddSeconds(5)));
        q.Observe(Ask("b", T0.AddSeconds(5)));
        Assert.Null(q.Head(T0.AddSeconds(10)));
    }

    // the directory is rescanned on every poll, so the same ask arrives again and again
    [Fact]
    public void ObservingTheSameNonceTwiceKeepsOneEntry()
    {
        var q = new AskQueue();
        q.Observe(Ask("dup", T0.AddSeconds(20)));
        q.Observe(Ask("dup", T0.AddSeconds(20)));
        Assert.Equal(1, q.Count);
    }

    // re-observing must not reorder: a rescan that reshuffled the queue would swap the banner under the
    // user's cursor between one poll and the next
    [Fact]
    public void ReObservingDoesNotChangeOrder()
    {
        var q = new AskQueue();
        q.Observe(Ask("first", T0.AddSeconds(20)));
        q.Observe(Ask("second", T0.AddSeconds(20)));
        q.Observe(Ask("first", T0.AddSeconds(20)));
        Assert.Equal("first", q.Head(T0)!.Nonce);
    }

    [Fact]
    public void SweepReportsAndDropsExpired()
    {
        var q = new AskQueue();
        q.Observe(Ask("stale", T0.AddSeconds(5)));
        q.Observe(Ask("live", T0.AddSeconds(60)));

        var dropped = q.Sweep(T0.AddSeconds(10));

        Assert.Equal(["stale"], dropped);
        Assert.Equal(1, q.Count);
        Assert.Equal("live", q.Head(T0.AddSeconds(10))!.Nonce);
    }

    [Fact]
    public void RemovingSomethingAbsentIsHarmless()
    {
        var q = new AskQueue();
        q.Observe(Ask("only", T0.AddSeconds(20)));
        q.Remove("never-existed");
        Assert.Equal(1, q.Count);
    }
    // A question is announced with a banner rather than mirrored on the pill ("give me a notification that
    // Claude asked, instead of the pill turning into the question"), so the pill's head skips questions and
    // a permission from another session behind one is not starved by it.
    private static PendingAsk Permission(string nonce, DateTimeOffset expires) =>
        new(nonce, 2, "other", "Bash", "rm -rf build", null, [], expires);

    [Fact]
    public void ThePillsHeadSkipsQuestions()
    {
        var q = new AskQueue();
        q.Observe(Ask("question", T0.AddSeconds(60)));
        q.Observe(Permission("permission", T0.AddSeconds(20)));
        Assert.Equal("permission", q.Permission(T0)!.Nonce);
    }

    [Fact]
    public void AQuestionIsAnnouncedOnce()
    {
        var q = new AskQueue();
        q.Observe(Ask("question", T0.AddSeconds(60)));
        Assert.Equal("question", q.TakeNewQuestion(T0)!.Nonce);
        q.Observe(Ask("question", T0.AddSeconds(60)));   // the next rescan sees the same file again
        Assert.Null(q.TakeNewQuestion(T0));
    }

    [Fact]
    public void AMultiQuestionBoxIsOneAnnouncement()
    {
        var q = new AskQueue();
        q.Observe(Ask("first", T0.AddSeconds(60)) with { Index = 0, Total = 2 });
        Assert.NotNull(q.TakeNewQuestion(T0));
        // its sibling landing on a later poll is still the same box in the terminal
        q.Observe(Ask("second", T0.AddSeconds(60)) with { Index = 1, Total = 2 });
        Assert.Null(q.TakeNewQuestion(T0));
    }

    [Fact]
    public void TheNextBoxFromTheSameSessionIsAnnouncedAgain()
    {
        var q = new AskQueue();
        q.Observe(Ask("first", T0.AddSeconds(60)));
        q.TakeNewQuestion(T0);
        q.Remove("first");   // answered in the terminal; the hook cleared it
        q.Observe(Ask("next", T0.AddSeconds(60)));
        Assert.Equal("next", q.TakeNewQuestion(T0)!.Nonce);
    }

    [Fact]
    public void AnExpiredQuestionIsNotAnnounced()
    {
        var q = new AskQueue();
        q.Observe(Ask("stale", T0.AddSeconds(5)));
        Assert.Null(q.TakeNewQuestion(T0.AddSeconds(10)));
    }
}
