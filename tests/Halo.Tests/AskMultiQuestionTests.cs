using System;
using System.Collections.Generic;
using Halo.ClaudeCode;
using Xunit;

namespace Halo.Tests;

// A box with more than one question in it. The arity gate in AskGate accepted exactly one, so a two-question
// AskUserQuestion published NOTHING - the pill stayed empty and the user never learned the terminal was waiting
// on them. Reported as "the notification for your question never came".
//
// The hook now writes one envelope per question, which puts the ordering problem on this side: they are written
// in the same instant with GUID filenames, so the directory hands them back in effectively random order, and the
// pill answers by TYPING into the terminal - showing question two while the terminal is on question one would
// put the wrong number in the box.
public class AskMultiQuestionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-13T01:00:00Z");

    private static PendingAsk Q(string nonce, int index, int total, int pid = 584)
        => new(nonce, pid, "session-a", "AskUserQuestion", null, $"question {index}",
               [new AskOption("yes", ""), new AskOption("no", "")], Now.AddMinutes(30),
               MultiSelect: false, HasPreview: false, Index: index, Total: total);

    [Fact]
    public void The_head_is_the_boxs_first_question_however_they_arrive()
    {
        var queue = new AskQueue();
        queue.Observe(Q("b", 1, 3));
        queue.Observe(Q("c", 2, 3));
        queue.Observe(Q("a", 0, 3));
        Assert.Equal("a", queue.Head(Now)?.Nonce);
    }

    // Answering the head is what advances the box, so the rest have to be waiting behind it in order.
    [Fact]
    public void Answering_one_moves_to_the_next_question_in_order()
    {
        var queue = new AskQueue();
        queue.Observe(Q("third", 2, 3));
        queue.Observe(Q("first", 0, 3));
        queue.Observe(Q("second", 1, 3));

        Assert.Equal("first", queue.Head(Now)?.Nonce);
        queue.Remove("first");
        Assert.Equal("second", queue.Head(Now)?.Nonce);
        queue.Remove("second");
        Assert.Equal("third", queue.Head(Now)?.Nonce);
    }

    // Between two different boxes it is arrival that decides, not the index - a later call's first question must
    // not jump the queue ahead of a question that has been on screen.
    [Fact]
    public void A_later_box_waits_its_turn()
    {
        var queue = new AskQueue();
        queue.Observe(Q("old", 0, 1, pid: 100));
        queue.Observe(Q("new", 0, 1, pid: 200));
        Assert.Equal("old", queue.Head(Now)?.Nonce);
    }

    // Re-observing is what a rescan does every poll, and it must not shuffle anything: the banner would swap
    // out from under the pointer between one poll and the next.
    [Fact]
    public void A_rescan_re_observing_everything_changes_no_order()
    {
        var queue = new AskQueue();
        List<PendingAsk> box = [Q("a", 0, 2), Q("b", 1, 2)];
        foreach (var ask in box) queue.Observe(ask);
        for (int poll = 0; poll < 3; poll++)
            foreach (var ask in box) queue.Observe(ask);

        Assert.Equal(2, queue.Count);
        Assert.Equal("a", queue.Head(Now)?.Nonce);
    }

    // An envelope from a hook that predates the index field: one question, and the queue must not try to order
    // it against anything.
    [Fact]
    public void An_older_single_question_envelope_still_works()
    {
        var single = new PendingAsk("solo", 584, "session-a", "AskUserQuestion", null, "just the one",
                                    [new AskOption("ok", "")], Now.AddMinutes(30));
        Assert.Equal(0, single.Index);
        Assert.Equal(1, single.Total);

        var queue = new AskQueue();
        queue.Observe(single);
        Assert.Equal("solo", queue.Head(Now)?.Nonce);
    }
}
