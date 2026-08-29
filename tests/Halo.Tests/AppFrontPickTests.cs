using Halo.Shell;

namespace Halo.Tests;

// Choosing between several windows of ONE process - the Windows Terminal case, where two agent sessions
// in two windows share a pid. The interesting assertions are the refusals: this used to take the topmost
// candidate, which is a coin flip that raised the wrong session's terminal.
public sealed class AppFrontPickTests
{
    private static (IntPtr, string) W(int id, string title) => (new IntPtr(id), title);

    [Fact]
    public void NoWindows_IsNothing()
        => Assert.Equal(IntPtr.Zero, AppFront.Pick([], "halo"));

    [Fact]
    public void OneWindow_NeedsNoHintAtAll()
    {
        // the ordinary case: one terminal, and the hint is irrelevant even when it does not match
        Assert.Equal(new IntPtr(7), AppFront.Pick([W(7, "pwsh")], null));
        Assert.Equal(new IntPtr(7), AppFront.Pick([W(7, "pwsh")], "something-else"));
    }

    [Fact]
    public void SeveralWindows_WithNoHint_RefusesToGuess()
    {
        var found = new[] { W(1, "pwsh - alpha"), W(2, "pwsh - beta") };
        Assert.Equal(IntPtr.Zero, AppFront.Pick(found, null));
        Assert.Equal(IntPtr.Zero, AppFront.Pick(found, "   "));
    }

    [Fact]
    public void SeveralWindows_TheHintPicksTheOneThatMatches()
    {
        var found = new[] { W(1, "pwsh - alpha"), W(2, "pwsh - beta") };
        Assert.Equal(new IntPtr(2), AppFront.Pick(found, "beta"));
    }

    [Fact]
    public void TheHintIsCaseInsensitive()
        => Assert.Equal(new IntPtr(2), AppFront.Pick([W(1, "alpha"), W(2, "Halo - main")], "halo"));

    [Fact]
    public void AHintThatMatchesNothing_RefusesRatherThanFallingBack()
    {
        // falling back to "first anyway" is the original bug wearing a hint
        var found = new[] { W(1, "pwsh - alpha"), W(2, "pwsh - beta") };
        Assert.Equal(IntPtr.Zero, AppFront.Pick(found, "gamma"));
    }

    [Fact]
    public void AHintThatMatchesTwoWindows_IsStillAmbiguous()
    {
        var found = new[] { W(1, "halo - one"), W(2, "halo - two") };
        Assert.Equal(IntPtr.Zero, AppFront.Pick(found, "halo"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(@"C:\Users\me\Projects\Halo", "Halo")]
    [InlineData(@"C:\Users\me\Projects\Halo\", "Halo")]
    [InlineData("/home/me/projects/halo", "halo")]
    [InlineData(@"C:\x", null)]          // one character matches half the desktop
    [InlineData("Halo", "Halo")]
    public void PathLeaf_TakesTheFolderName(string? path, string? expected)
        => Assert.Equal(expected, AppFront.PathLeaf(path));
}

// The defect behind "double-click opens the wrong session's terminal", after the title hint had already
// been added and it still happened.
//
// Pick was only ever consulted on ONE of the four paths RevealPrimaryApp tries. It refused correctly when
// two Windows Terminal windows both claimed the session's pid - and then the next fallback took the first
// window in z-order and raised it anyway. A refusal that is followed by a guess is not a refusal; the
// guessing path is simply the one that answers.
//
// So the rule is about the whole set of paths, not about any one of them: every route from a session to a
// window has to be able to say "I do not know". These pin the decision Pick makes on all of them.
public class AppFrontAmbiguityTests
{
    private static (IntPtr, string) W(int h, string title) => (new IntPtr(h), title);

    [Fact]
    public void Two_windows_of_one_process_with_nothing_to_go_on_is_refused()
        => Assert.Equal(IntPtr.Zero,
            AppFront.Pick([W(11, "Windows Terminal"), W(12, "Windows Terminal")], null));

    // ...and refused just as hard when the hint matches BOTH, which is the real Windows Terminal case: two
    // tabs open on sibling folders whose names share a prefix. Raising the wrong one is worse than nothing.
    [Fact]
    public void A_hint_that_matches_two_windows_is_still_refused()
        => Assert.Equal(IntPtr.Zero,
            AppFront.Pick([W(11, "halo - pwsh"), W(12, "halo-docs - pwsh")], "halo"));

    [Fact]
    public void A_hint_that_matches_exactly_one_wins()
        => Assert.Equal(new IntPtr(12),
            AppFront.Pick([W(11, "notes - pwsh"), W(12, "halo - pwsh")], "halo"));

    // The hint is the leaf of the session's working directory, and a one-character leaf matches half the
    // titles on the desktop - which would turn the guard back into a coin flip through the front door.
    [Fact]
    public void A_useless_hint_is_not_offered_at_all()
    {
        Assert.Null(AppFront.PathLeaf(@"C:\a"));
        Assert.Null(AppFront.PathLeaf(""));
        Assert.Equal("halo", AppFront.PathLeaf(@"C:\Projects\halo\"));
    }
}
