extern alias settingsasm;
using settingsasm::Halo.Settings;
using Xunit;

namespace Halo.Tests;

// The word on the hook rows' button, over every reading the row can be showing. It is one switch, but the
// wrong word here installs or uninstalls the user's agent hooks on a single click, and two of the readings
// are ones nobody sees while developing: the row is almost always "Connected" on this machine.
public class HookActionTests
{
    [Theory]
    [InlineData("Connected", "Disconnect")]
    [InlineData("Not connected", "Connect")]
    // The mark Halo writes when the user disconnects, which is a different string from the absence of hooks
    // and has to lead back - Halo never reconnects an agent it was told to leave alone, so if this row does
    // not offer the way back there is none.
    [InlineData("Disconnected", "Connect")]
    public void The_verb_is_the_one_the_reading_leaves_undone(string reading, string label)
        => Assert.Equal(label, Live.HookAction(reading));

    // A fetch that threw. Offering "Connect" over it would be guessing not-connected from a failure to look,
    // and Actions.Run refuses the same reading for the same reason - the click can only ask again.
    [Fact]
    public void A_reading_that_could_not_be_taken_offers_only_another_read()
        => Assert.Equal("Retry", Live.HookAction(Live.Unavailable));

    // Not yet fetched. MainWindow disables the button on exactly this word, so anything unrecognised has to
    // land here rather than on a verb: null is the first paint of every hook row.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Whatever a later reading might say")]
    public void An_unread_row_says_so_and_stays_dead(string? reading)
        => Assert.Equal(Live.Checking, Live.HookAction(reading));

    // The sentinel has to be something the helper cannot say, and the property that guarantees it is the
    // SIGN - process exit codes here are non-negative. An earlier version of this test listed 0, 1, 2 and
    // 4 and called that exhaustive; it was not, because Autostart.PackagedAnswer is 3, and a test that
    // enumerates the codes has to be revisited every time the helper grows one. This does not.
    [Fact]
    public void The_could_not_run_code_is_not_one_the_helper_can_return()
        => Assert.True(Live.CouldNotRun < 0, "the sentinel must not collide with any exit code");

    // An unreadable row must never offer a verb, whichever way it became unreadable. This is the whole
    // point of the tri-state: "Connect" here installs hooks the user may have been trying to remove.
    [Fact]
    public void An_unreadable_row_never_offers_to_change_anything()
    {
        string label = Live.HookAction(Live.Unavailable);
        Assert.NotEqual("Connect", label);
        Assert.NotEqual("Disconnect", label);
    }

    // Tone's arm for this is behaviourally dead - the default returns Neutral too - so without a test the
    // invariant is enforced by nothing, and a later edit that sweeps "unavailable" into the Attention list
    // beside "missing" would be silent. Halo failing to look is not the user's fault and must not be
    // painted as one.
    [Fact]
    public void An_unreadable_reading_is_never_painted_as_a_fault()
    {
        Assert.Equal(Live.State.Neutral, Live.Tone(Live.Unavailable));
        Assert.Equal(Live.State.Attention, Live.Tone("Missing"));   // the colour it must not share
    }
}
