using Halo.Shell;
using Halo.Widgets;

namespace Halo.Tests;

public sealed class AgentNoticeTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-16T12:00:00Z");

    [Fact]
    public void WaitingInput_DoesNotStealThePill()
    {
        var state = new AgentNoticeCoordinator(primary: 0);

        // waiting_input used to pop the pill open; the user asked for it to stay put (compact-done
        // is still allowed to take over). This test pins that decision so it can't regress silently.
        state.Observe(widgetIndex: 2, new AgentNotice("waiting_input", null, "approve?"), Now);

        Assert.False(state.IsOpen(Now));
        Assert.Equal(0, state.Primary);
    }

    [Fact]
    public void CompactCompletion_HoldsForFourSeconds()
    {
        var state = new AgentNoticeCoordinator(primary: 0);
        state.Observe(widgetIndex: 1, new AgentNotice("compacting", null, null), Now);

        state.Observe(widgetIndex: 1, new AgentNotice("working", Now, null), Now);

        Assert.Equal(1, state.Primary);
        state.Tick(Now.AddSeconds(4));
        Assert.Equal(1, state.Primary);
        state.Tick(Now.AddSeconds(5));
        Assert.Equal(0, state.Primary);
    }

    [Fact]
    public void CancelledCompact_DoesNotAnnounceCompletion()
    {
        var state = new AgentNoticeCoordinator(primary: 0);
        state.Observe(widgetIndex: 1, new AgentNotice("compacting", null, null), Now);

        // esc-cancelled: state leaves "compacting" but compactedAt never gets written
        state.Observe(widgetIndex: 1, new AgentNotice("working", null, null), Now);

        Assert.False(state.IsOpen(Now)); // no "compacted :)" notice window
    }

    [Fact]
    public void StaleCompactedAt_DoesNotAnnounceAtStartup()
    {
        var state = new AgentNoticeCoordinator(primary: 0);

        state.Observe(widgetIndex: 1, new AgentNotice("idle", Now.AddMinutes(-10), null), Now);

        Assert.False(state.IsOpen(Now));
        Assert.Equal(0, state.Primary);
    }

    [Fact]
    public void SimultaneousNotices_PreferDesktopCodexWhenCurrentWidgetIsNotAnAgent()
    {
        var state = new AgentNoticeCoordinator(primary: 0);

        // two compact-done notices land in the same tick and neither widget is the current primary,
        // so the desktop-backed one wins. (Written with compact-done because waiting_input no longer
        // opens a notice window at all — see WaitingInput_DoesNotStealThePill.)
        state.Observe(widgetIndex: 1, new AgentNotice("compacting", null, null), Now);
        state.Observe(widgetIndex: 2, new AgentNotice("compacting", null, null), Now, desktopBacked: true);
        state.Observe(widgetIndex: 1, new AgentNotice("working", Now, null), Now);
        state.Observe(widgetIndex: 2, new AgentNotice("working", Now, null), Now, desktopBacked: true);

        Assert.Equal(2, state.Primary);
    }

    [Fact]
    public void Announce_PutsTheArrivalOnThePillThenGivesItBack()
    {
        var state = new AgentNoticeCoordinator(primary: 0);

        // the costume promised widget 3 was about to appear; before this the pill melted out of the
        // headphones straight back into the agent session it had interrupted, so the beat showed nothing
        state.Announce(widgetIndex: 3, Now.AddSeconds(4));
        state.Tick(Now);
        Assert.Equal(3, state.Primary);

        // ...but borrowed, not given: a music track must not permanently outrank a working agent
        state.Tick(Now.AddSeconds(5));
        Assert.Equal(0, state.Primary);
    }

    [Fact]
    public void Announce_MarksTheArrivalOnlyWhileItsWindowIsOpen()
    {
        var state = new AgentNoticeCoordinator(primary: 0);
        Assert.False(state.IsAnnouncing(3));

        state.Announce(widgetIndex: 3, Now.AddSeconds(4));
        Assert.True(state.IsAnnouncing(3));
        Assert.False(state.IsAnnouncing(0));   // the widget it was borrowed from is not the arrival

        // the flag is what suspends the "a working agent outranks media" rule, so it has to close with
        // the window - otherwise a track would keep the pill for good
        state.Tick(Now.AddSeconds(5));
        Assert.False(state.IsAnnouncing(3));
    }

    [Fact]
    public void Announce_DoesNotStrandThePillOnAWidgetThatWentAway()
    {
        var state = new AgentNoticeCoordinator(primary: 0);
        state.Announce(widgetIndex: 3, Now.AddSeconds(4));
        state.Tick(Now);

        // the widget that was announced closed while the pill was still on it, and the one it was
        // borrowed from is gone too - Primary must not be restored to a widget that is no longer there
        state.Tick(Now.AddSeconds(5), isActive: i => i != 0);
        Assert.Equal(3, state.Primary);
    }
    // A widget whose arrival is quiet keeps the pill collapsed while its announcement runs - "opening
    // Claude makes Halo big". The coordinator only has to say which open windows are arrivals: a compact
    // finishing on that same widget is real news and must still open the panel.
    [Fact]
    public void Announce_IsAnArrivalNotANotice()
    {
        var state = new AgentNoticeCoordinator(primary: 0);
        state.Announce(widgetIndex: 3, Now.AddSeconds(4));

        Assert.True(state.IsOpen(Now));
        Assert.False(state.HasNotice(3));
    }

    [Fact]
    public void CompactCompletion_IsANoticeEvenOnTheAnnouncedWidget()
    {
        var state = new AgentNoticeCoordinator(primary: 0);
        state.Announce(widgetIndex: 1, Now.AddSeconds(4));
        state.Observe(widgetIndex: 1, new AgentNotice("compacting", null, null), Now);

        state.Observe(widgetIndex: 1, new AgentNotice("working", Now, null), Now);

        Assert.True(state.HasNotice(1));
        state.Tick(Now.AddSeconds(5));
        Assert.False(state.HasNotice(1));
    }
}
