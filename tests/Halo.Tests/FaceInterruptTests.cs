using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// The launch interrupt's one decision: may the face barge in and say what was just launched?
//
// It deliberately does NOT ask the idle face's question. The idle face wants the desktop and an empty pill;
// the interrupt would then fire almost never, because a hotkey launcher is used from on top of other windows
// and the pill is often busy with music while you launch something else. What is left is precedence.
public class FaceInterruptTests
{
    private static bool Allowed(bool faceWanted = true, bool expanded = false, bool banner = false,
                                bool asking = false, bool greeting = false, bool privacy = false,
                                bool moving = false, bool alreadyBusy = false)
        => FaceInterrupt.Allowed(faceWanted, expanded, banner, asking, greeting, privacy, moving, alreadyBusy);

    [Fact]
    public void ACollapsedPillOverSomeoneElsesWindowIsTheNormalCase()
    {
        // the whole point: neither the desktop nor an empty pill is required
        Assert.True(Allowed());
    }

    [Fact]
    public void AnExpandedPanelIsNotTakenAway()
    {
        // the line between "costs a second of a strip nobody is reading" and "snatches away what is being read"
        Assert.False(Allowed(expanded: true));
    }

    [Fact]
    public void ABannerKeepsThePillBecauseItIsAMessage()
        => Assert.False(Allowed(banner: true));

    [Fact]
    public void AnAgentWaitingOnAnAnswerOutranksAnAcknowledgement()
        => Assert.False(Allowed(asking: true));

    [Fact]
    public void TheGreetingRunsToTheEndOrNotAtAll()
        => Assert.False(Allowed(greeting: true));

    [Fact]
    public void APrivacyDotIsNeverCoveredUp()
    {
        // an active mic or camera keeps a real slim tab precisely so its dot shows; a face over it would hide
        // the one thing the pill is obliged to display
        Assert.False(Allowed(privacy: true));
    }

    [Fact]
    public void NothingInterruptsAPillBeingDragged()
        => Assert.False(Allowed(moving: true));

    [Fact]
    public void ABeatAlreadyRunningIsNotRestartedUnderneathItself()
    {
        // two launches in quick succession, or a widget handover mid-flight: the second one is dropped rather
        // than cutting the first, because a costume half on and then replaced reads as a glitch
        Assert.False(Allowed(alreadyBusy: true));
    }

    [Fact]
    public void TurningTheFaceOffTurnsThisOffToo()
    {
        // one setting, not two - the interrupt is the face, so a user who switched the face off has already
        // answered this question
        Assert.False(Allowed(faceWanted: false));
    }
}
