extern alias hooksasm;
using System;
using System.IO;
using System.Text.Json.Nodes;
using hooksasm::Halo.Hooks;
using Xunit;

namespace Halo.Tests;

// The twin of ClaudeHookInstallerTests, and it exists because the Codex side was assumed healthy once on
// the strength of a hand-written fixture whose shape was wrong. Everything here builds its fixture through
// CodexHookInstaller.Install, so the file under test is the one that decided what the file looks like.
public class CodexHookInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "halo-codex-" + Guid.NewGuid().ToString("n"));
    // a real file: IsInstalled asks whether the exe a handler names is still on disk
    private readonly string _exe;
    private string Settings => Path.Combine(_root, "hooks.json");

    public CodexHookInstallerTests()
    {
        Directory.CreateDirectory(_root);
        _exe = Path.Combine(_root, "Halo.Hooks.exe");
        File.WriteAllText(_exe, "");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public void A_fresh_install_reads_as_installed()
    {
        CodexHookInstaller.Install(Settings, _exe);
        Assert.True(CodexHookInstaller.IsInstalled(Settings));
    }

    // Same defect as the Claude side, same consequence: hooks left behind by a build that has been
    // uninstalled answer "installed", so nothing repairs them and the Codex panel stays empty.
    [Fact]
    public void IsInstalled_is_false_once_the_hook_exe_is_gone()
    {
        CodexHookInstaller.Install(Settings, _exe);
        Assert.True(CodexHookInstaller.IsInstalled(Settings));

        File.Delete(_exe);
        Assert.False(CodexHookInstaller.IsInstalled(Settings));
    }

    [Fact]
    public void IsInstalled_is_false_when_the_hooks_name_another_build()
    {
        string other = Path.Combine(_root, "other", "Halo.Hooks.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(other)!);
        File.WriteAllText(other, "");

        CodexHookInstaller.Install(Settings, _exe);

        Assert.True(CodexHookInstaller.IsInstalled(Settings, _exe));
        Assert.False(CodexHookInstaller.IsInstalled(Settings, other));
    }

    [Fact]
    public void Uninstall_still_strips_handlers_that_name_a_deleted_exe()
    {
        CodexHookInstaller.Install(Settings, _exe);
        File.Delete(_exe);

        CodexHookInstaller.Uninstall(Settings);

        Assert.DoesNotContain("Halo.Hooks.exe", File.ReadAllText(Settings));
    }

    // The twin of the Claude pruning tests. Both Uninstalls emptied the event arrays and left the keys, and
    // there is no reason for the two files to disagree about what "put it back" means.
    [Fact]
    public void Uninstall_leaves_behind_no_hooks_key_it_created_itself()
    {
        CodexHookInstaller.Install(Settings, _exe);
        CodexHookInstaller.Uninstall(Settings);

        Assert.Null(((JsonObject)JsonNode.Parse(File.ReadAllText(Settings))!)["hooks"]);
    }

    [Fact]
    public void Uninstall_keeps_an_event_Halo_never_managed()
    {
        File.WriteAllText(Settings, """{ "hooks": { "SessionEnd": [] } }""");
        CodexHookInstaller.Install(Settings, _exe);
        CodexHookInstaller.Uninstall(Settings);

        var hooks = Assert.IsType<JsonObject>(((JsonObject)JsonNode.Parse(File.ReadAllText(Settings))!)["hooks"]);
        Assert.NotNull(hooks["SessionEnd"]);
        Assert.Null(hooks["Stop"]);
    }
}
