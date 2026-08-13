using System;
using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// "When the mouse is on the expanded pill and the notification's time is up it should stay open, it shouldn't
// suddenly jump." The pill never closed while hovered - what went away was its CONTENT: the widget being read
// goes inactive (traffic stops, a download finishes) or a notice window expires, and the panel swaps to another
// widget or tucks away mid-glance. The banner already made this promise; nothing else did.
public class HoverHoldTests
{
    [Fact]
    public void Only_an_open_panel_under_the_pointer_holds()
    {
        Assert.True(HoverHold.Holding(over: true, progress: 1f, banner: false, dropping: false));
        // a pointer crossing the collapsed strip on its way elsewhere is not reading anything, and holding
        // there would keep dead widgets on screen
        Assert.False(HoverHold.Holding(over: true, progress: 0.4f, banner: false, dropping: false));
        Assert.False(HoverHold.Holding(over: false, progress: 1f, banner: false, dropping: false));
        // a banner owns the pill while it is up and has its own hold; a drop is mid-flight
        Assert.False(HoverHold.Holding(over: true, progress: 1f, banner: true, dropping: false));
        Assert.False(HoverHold.Holding(over: true, progress: 1f, banner: false, dropping: true));
    }

    [Fact]
    public void The_widget_under_the_pointer_stays_in_the_active_set()
    {
        Assert.Equal([1, 4, 7], HoverHold.Keep([1, 7], primary: 4, holding: true));
        Assert.Equal([2, 5], HoverHold.Keep([2, 5], primary: 5, holding: true));      // already there
        Assert.Equal([2, 5], HoverHold.Keep([2, 5], primary: 4, holding: false));     // not holding
        Assert.Equal([], HoverHold.Keep([], primary: -1, holding: true));             // nothing is primary
    }

    // The indices ARE the strip's order and the fallback order. A held widget appearing at the wrong end of the
    // strip is the same kind of surprise this helper exists to prevent.
    [Fact]
    public void The_held_widget_keeps_its_place_in_the_strip_order()
    {
        Assert.Equal([0, 3, 9], HoverHold.Keep([3, 9], primary: 0, holding: true));
        Assert.Equal([3, 9, 12], HoverHold.Keep([3, 9], primary: 12, holding: true));
        Assert.Equal([6], HoverHold.Keep([], primary: 6, holding: true));
    }

    // A pill that empties while being read is the worst version of the bug: it tucks away to an invisible strip.
    [Fact]
    public void A_held_widget_keeps_the_pill_from_reading_as_empty()
    {
        Assert.NotEmpty(HoverHold.Keep([], primary: 2, holding: true));
        Assert.Empty(HoverHold.Keep([], primary: 2, holding: false));
    }

    [Fact]
    public void A_held_notice_window_is_pushed_forward_rather_than_expiring()
    {
        var start = DateTimeOffset.Parse("2026-08-13T02:00:00Z");
        var notices = new AgentNoticeCoordinator(primary: 0);
        notices.Observe(1, new Halo.Widgets.AgentNotice("working", start.AddSeconds(-1), null), start);
        Assert.True(notices.IsOpen(start));

        // four seconds later it is over, and the panel would swap back
        var later = start.AddSeconds(5);
        Assert.False(notices.IsOpen(later));

        // held: pushed out to the grace window, so the same instant is still open
        notices.Hold(later.AddSeconds(HoverHold.GraceSeconds));
        Assert.True(notices.IsOpen(later));
        // and it ends on its own once the pointer stops pushing
        Assert.False(notices.IsOpen(later.AddSeconds(HoverHold.GraceSeconds + 0.1)));
    }
}
