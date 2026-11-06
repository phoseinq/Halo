using Halo.ClaudeCode;

namespace Halo.Tests;

// The claude daemon respawns persisted background/fork sessions as headless processes that never end.
// The hooks tag them background:true (their claude process is parented by claude, not a shell); the
// store hides them unless they are actually doing something the user could care about or act on.
public class BackgroundSessionTests
{
    private static CcStatus St(bool background, string? state) =>
        new() { Background = background, State = state };

    [Fact]
    public void Idle_background_session_is_hidden()
        => Assert.True(StatusStore.BackgroundHidden(St(true, "idle")));

    [Fact]
    public void Stateless_background_session_is_hidden()
        => Assert.True(StatusStore.BackgroundHidden(St(true, null)));

    [Theory]
    [InlineData("working")]
    [InlineData("compacting")]
    [InlineData("waiting_input")] // the pill's ask flow is how a headless session gets answered
    public void Busy_background_session_stays_visible(string state)
        => Assert.False(StatusStore.BackgroundHidden(St(true, state)));

    [Theory]
    [InlineData("idle")]
    [InlineData("working")]
    [InlineData(null)]
    public void Foreground_sessions_are_never_hidden_by_this_gate(string? state)
        => Assert.False(StatusStore.BackgroundHidden(St(false, state)));
}

// The desktop app keeps idle engine processes alive for sessions that are merely open in its sidebar.
public class DesktopIdleSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private static CcStatus St(string? state, string? startedAt, TimeSpan sinceUpdate, string? source = "desktop") =>
        new() { Source = source, State = state, StartedAt = startedAt, UpdatedAt = (Now - sinceUpdate).ToString("o") };

    [Fact]
    public void Never_prompted_desktop_session_is_hidden()
        => Assert.True(StatusStore.DesktopIdleHidden(St("idle", null, TimeSpan.Zero), Now));

    [Fact]
    public void Finished_desktop_session_lingers_then_hides()
    {
        Assert.False(StatusStore.DesktopIdleHidden(St("idle", "x", TimeSpan.FromSeconds(20)), Now));
        Assert.True(StatusStore.DesktopIdleHidden(St("idle", "x", TimeSpan.FromSeconds(40)), Now));
    }

    // the real shape after a stop: StartedAt cleared, the prompt still on record. It hid instantly.
    [Fact]
    public void Just_finished_desktop_session_lingers_though_stop_cleared_its_start()
    {
        var st = St("idle", null, TimeSpan.FromSeconds(5));
        st.LastPrompt = "fix the pill";
        Assert.False(StatusStore.DesktopIdleHidden(st, Now));
    }

    [Theory]
    [InlineData("working")]
    [InlineData("compacting")]
    [InlineData("waiting_input")]
    public void Busy_desktop_session_is_always_shown(string state)
        => Assert.False(StatusStore.DesktopIdleHidden(St(state, null, TimeSpan.FromHours(1)), Now));

    [Fact]
    public void Cli_session_is_never_hidden_by_this_gate()
        => Assert.False(StatusStore.DesktopIdleHidden(St("idle", null, TimeSpan.FromHours(1), source: null), Now));
}
