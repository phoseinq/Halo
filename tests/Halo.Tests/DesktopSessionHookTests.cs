extern alias hooksasm;

namespace Halo.Tests;

// The desktop app's main process is named claude.exe and parents every session it runs, so the parent's
// name alone made every desktop session look like a daemon-hosted background one - hidden when idle.
public class DesktopSessionHookTests
{
    [Theory]
    [InlineData("claude.exe")]
    [InlineData("node.exe")]
    public void Claude_parent_without_desktop_entrypoint_is_daemon_hosted(string parent)
        => Assert.True(hooksasm::Halo.Hooks.Program.IsDaemonHosted(parent, null));

    [Theory]
    [InlineData("claude.exe")]
    [InlineData("Claude.exe")]
    public void Desktop_entrypoint_is_never_daemon_hosted(string parent)
        => Assert.False(hooksasm::Halo.Hooks.Program.IsDaemonHosted(parent, "claude-desktop"));

    [Theory]
    [InlineData("pwsh.exe", null)]
    [InlineData("bash.exe", "cli")]
    public void Shell_parent_is_a_normal_session(string parent, string? entrypoint)
        => Assert.False(hooksasm::Halo.Hooks.Program.IsDaemonHosted(parent, entrypoint));

    [Theory]
    [InlineData("claude-desktop", true)]
    [InlineData("CLAUDE-DESKTOP", true)]
    [InlineData("cli", false)]
    [InlineData(null, false)]
    public void Desktop_entrypoint_detection(string? entrypoint, bool expected)
        => Assert.Equal(expected, hooksasm::Halo.Hooks.Program.IsDesktopEntrypoint(entrypoint));
}
