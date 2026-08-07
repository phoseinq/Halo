using Halo.ClaudeCode;
using Halo.Widgets;

namespace Halo.Tests;

// The context alert fires once per session and then latches, and the latch is keyed by this string. What
// goes into the key therefore decides how often the user is told their context is full.
//
// It used to be pid + StartedAt. StartedAt is the TURN's start - the hook rewrites it on every prompt for
// the elapsed-time readout - so the key changed every message, the latch re-armed every message, and a
// session sitting above the threshold announced itself again on every turn.
public sealed class ContextAlertKeyTests
{
    private static CcStatus Session(int pid, string? sessionId, string? startedAt) =>
        new() { Pid = pid, SessionId = sessionId, StartedAt = startedAt };

    // the regression, stated directly
    [Fact]
    public void TheKeySurvivesANewTurn()
    {
        var first = ClaudeCodeWidget.ContextKey(Session(21660, "df3cbced", "2026-08-07T12:00:00Z"));
        var next = ClaudeCodeWidget.ContextKey(Session(21660, "df3cbced", "2026-08-07T12:31:04Z"));
        Assert.Equal(first, next);
    }

    // ...and past a compact, which rewrites startedAt too (pre-compact sets it, post-compact nulls it)
    [Fact]
    public void TheKeySurvivesACompact()
    {
        var before = ClaudeCodeWidget.ContextKey(Session(21660, "df3cbced", "2026-08-07T12:00:00Z"));
        var after = ClaudeCodeWidget.ContextKey(Session(21660, "df3cbced", null));
        Assert.Equal(before, after);
    }

    // a /clear mints a new session id, and a fresh context deserves a fresh warning
    [Fact]
    public void ClearingTheContextArmsItAgain()
    {
        var before = ClaudeCodeWidget.ContextKey(Session(21660, "df3cbced", null));
        var after = ClaudeCodeWidget.ContextKey(Session(21660, "9a10bb2e", null));
        Assert.NotEqual(before, after);
    }

    // a resumed session carries its id into a new process; both are live and each warns for itself
    [Fact]
    public void TwoProcessesSharingASessionIdAreTwoSessions()
    {
        Assert.NotEqual(
            ClaudeCodeWidget.ContextKey(Session(21660, "df3cbced", null)),
            ClaudeCodeWidget.ContextKey(Session(30012, "df3cbced", null)));
    }

    // generic-agent files need not carry an id; the pid is the identity there and must still be usable
    [Fact]
    public void AMissingSessionIdStillYieldsAStableKey()
    {
        Assert.Equal(
            ClaudeCodeWidget.ContextKey(Session(21660, null, "2026-08-07T12:00:00Z")),
            ClaudeCodeWidget.ContextKey(Session(21660, null, "2026-08-07T13:00:00Z")));
    }

    [Fact]
    public void NothingLiveHasNoKey() => Assert.Null(ClaudeCodeWidget.ContextKey(null));
}
