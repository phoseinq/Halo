extern alias hooksasm;
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using hooksasm::Halo.Hooks;
using Xunit;

namespace Halo.Tests;

// This writes into the user's real Claude Code config, so the rules it follows are not cosmetic: a mistake
// here breaks the agent, not just the pill. What is pinned is exactly what hooks/install-hooks.ps1 used to
// guarantee, because that script is what these replace.
public class ClaudeHookInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "halo-claude-" + Guid.NewGuid().ToString("n"));
    // A real file rather than a plausible-looking path. IsInstalled now asks whether the exe a handler
    // names is still on disk, so a fictional one reads as "not installed" and every test below would be
    // measuring that instead of the thing it claims to measure.
    private readonly string _exe;
    private string Settings => Path.Combine(_root, "settings.json");

    public ClaudeHookInstallerTests()
    {
        Directory.CreateDirectory(_root);
        _exe = Path.Combine(_root, "Halo.Hooks.exe");
        File.WriteAllText(_exe, "");
    }
    // clears ReadOnly first: the read-only regression test deliberately leaves one behind, and
    // Directory.Delete throws on it - into the bare catch, leaking a temp folder on every run
    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        }
        catch { }
        try { Directory.Delete(_root, true); } catch { }
    }

    private JsonObject Read() => (JsonObject)JsonNode.Parse(File.ReadAllText(Settings))!;
    private static string[] Commands(JsonObject hooks, string evt) =>
        [.. (hooks[evt] as JsonArray ?? []).SelectMany(e =>
            ((e as JsonObject)?["hooks"] as JsonArray ?? []).Select(h => (string?)(h as JsonObject)?["command"] ?? ""))];

    [Fact]
    public void Installs_all_nine_events_into_a_file_that_did_not_exist()
    {
        ClaudeHookInstaller.Install(Settings, _exe);

        var hooks = (JsonObject)Read()["hooks"]!;
        foreach (var evt in new[] { "SessionStart", "UserPromptSubmit", "PreToolUse", "PostToolUse",
                                    "Notification", "PreCompact", "PostCompact", "Stop", "SessionEnd" })
            Assert.Contains(Commands(hooks, evt), c => c.Contains(_exe));
    }

    // the exact shape the agent parses: a quoted absolute path, then the bare event word
    [Fact]
    public void Command_is_the_quoted_path_then_the_event_word()
    {
        ClaudeHookInstaller.Install(Settings, _exe);
        Assert.Equal($"\"{_exe}\" session-start", Commands((JsonObject)Read()["hooks"]!, "SessionStart").Single());
    }

    [Fact]
    public void A_relative_path_is_refused_rather_than_written()
    {
        // the command runs later, from a working directory nobody controls
        Assert.Throws<ArgumentException>(() => ClaudeHookInstaller.Install(Settings, "Halo.Hooks.exe"));
        Assert.False(File.Exists(Settings));
    }

    // the bug the Store alias very nearly shipped: re-running appended instead of replacing, so every event
    // ended up with one handler per install
    [Fact]
    public void Reinstalling_replaces_rather_than_appends()
    {
        ClaudeHookInstaller.Install(Settings, _exe);
        ClaudeHookInstaller.Install(Settings, _exe);
        ClaudeHookInstaller.Install(Settings, _exe);

        Assert.Single(Commands((JsonObject)Read()["hooks"]!, "SessionStart"));
    }

    [Fact]
    public void Somebody_elses_handlers_are_left_alone()
    {
        File.WriteAllText(Settings, """
        {
          "hooks": {
            "SessionStart": [ { "hooks": [ { "type": "command", "command": "\"C:\\tools\\mine.exe\" go" } ] } ]
          },
          "model": "opus"
        }
        """);

        ClaudeHookInstaller.Install(Settings, _exe);
        var root = Read();
        Assert.Contains(Commands((JsonObject)root["hooks"]!, "SessionStart"), c => c.Contains("mine.exe"));
        Assert.Equal("opus", (string?)root["model"]);
    }

    // both agents can share a machine, and Codex's handlers live in files of the same shape - a Claude
    // install that swept them out would silently disconnect the other agent
    [Fact]
    public void Codex_handlers_are_not_treated_as_ours()
    {
        File.WriteAllText(Settings, $$"""
        { "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "\"{{_exe.Replace("\\", "\\\\")}}\" codex stop" } ] } ] } }
        """);

        ClaudeHookInstaller.Install(Settings, _exe);
        var stop = Commands((JsonObject)Read()["hooks"]!, "Stop");
        Assert.Contains(stop, c => c.Contains("codex stop"));
        Assert.Contains(stop, c => c.EndsWith("\" stop"));
    }

    [Fact]
    public void Uninstall_removes_ours_and_keeps_the_rest()
    {
        File.WriteAllText(Settings, """
        { "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "\"C:\\tools\\mine.exe\" go" } ] } ] } }
        """);
        ClaudeHookInstaller.Install(Settings, _exe);
        ClaudeHookInstaller.Uninstall(Settings);

        var stop = Commands((JsonObject)Read()["hooks"]!, "Stop");
        Assert.Single(stop);
        Assert.Contains("mine.exe", stop[0]);
    }

    [Fact]
    public void A_backup_is_left_where_the_docs_have_always_said()
    {
        File.WriteAllText(Settings, """{ "model": "opus" }""");
        ClaudeHookInstaller.Install(Settings, _exe);
        Assert.True(File.Exists(Settings + ".halo-bak"));
    }

    // IsInstalled decides whether the app offers to install, so "half a set" has to read as "not installed"
    // or the offer never appears on a machine that needs it
    [Fact]
    public void IsInstalled_is_false_until_every_event_is_present()
    {
        Assert.False(ClaudeHookInstaller.IsInstalled(Settings));

        ClaudeHookInstaller.Install(Settings, _exe);
        Assert.True(ClaudeHookInstaller.IsInstalled(Settings));

        var root = Read();
        ((JsonObject)root["hooks"]!).Remove("SessionEnd");
        File.WriteAllText(Settings, root.ToJsonString());
        Assert.False(ClaudeHookInstaller.IsInstalled(Settings));
    }

    [Fact]
    public void A_corrupt_settings_file_reads_as_not_installed_rather_than_throwing()
    {
        File.WriteAllText(Settings, "{ this is not json");
        Assert.False(ClaudeHookInstaller.IsInstalled(Settings));
    }

    // The bug that made auto-connect fail forever on a live machine, and it took a debugging session to
    // find because running the helper by hand is what clears the flag. BOTH files: File.Copy and File.Move
    // each refuse a read-only destination, so clearing it on the backup alone just moved the failure down
    // a line. Neither is a file the user ever touches - .halo-bak inherits the attribute from a
    // settings.json that a sync client or a policy marked read-only.
    [Fact]
    public void A_read_only_backup_or_settings_file_does_not_stop_the_install()
    {
        ClaudeHookInstaller.Install(Settings, _exe);
        string backup = Settings + ".halo-bak";
        File.WriteAllText(backup, "{}");
        File.SetAttributes(backup, File.GetAttributes(backup) | FileAttributes.ReadOnly);
        File.SetAttributes(Settings, File.GetAttributes(Settings) | FileAttributes.ReadOnly);

        ClaudeHookInstaller.Install(Settings, _exe);   // must not throw
        Assert.True(ClaudeHookInstaller.IsInstalled(Settings));

        // The attribute is PUT BACK on settings.json. A policy or a sync client that marked it read-only
        // meant it, and Halo dropping that protection for every other tool on the machine - unrecorded and
        // with no way back - is the opposite of what BannerGate does with ShowBanner.
        Assert.True(File.GetAttributes(Settings).HasFlag(FileAttributes.ReadOnly));
        // but NOT on the backup, which File.Copy would otherwise inherit onto it: that is the file the
        // banner tells the user their old settings are saved in, so it has to be openable
        Assert.False(File.GetAttributes(backup).HasFlag(FileAttributes.ReadOnly));

        // and again, because restoring the attribute means the next install meets the same wall it just
        // cleared - the fix only holds if the clearing happens on every pass rather than once
        ClaudeHookInstaller.Install(Settings, _exe);
        Assert.True(ClaudeHookInstaller.IsInstalled(Settings));
    }

    // This one happened for real. Uninstalling the ordinary build deleted
    // %LOCALAPPDATA%\Programs\Halo\Halo.Hooks.exe, all nine handlers went on naming it, and
    // query-claude-hooks still answered "installed" - so the app never repaired the wiring and the agent
    // panels stayed empty with nothing anywhere saying why.
    [Fact]
    public void IsInstalled_is_false_once_the_hook_exe_is_gone()
    {
        ClaudeHookInstaller.Install(Settings, _exe);
        Assert.True(ClaudeHookInstaller.IsInstalled(Settings));

        File.Delete(_exe);
        Assert.False(ClaudeHookInstaller.IsInstalled(Settings));
    }

    // A different Halo.Hooks.exe still runs, so it is not broken - but when this build's own stub resolves,
    // the wiring belongs on it. That is the migration which produced the bug above: an installer build's
    // hooks left in place while the Store build was the one running, each talking to the other's binary.
    [Fact]
    public void IsInstalled_is_false_when_the_hooks_name_another_build()
    {
        string other = Path.Combine(_root, "other", "Halo.Hooks.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(other)!);
        File.WriteAllText(other, "");

        ClaudeHookInstaller.Install(Settings, _exe);

        Assert.True(ClaudeHookInstaller.IsInstalled(Settings, _exe));
        Assert.False(ClaudeHookInstaller.IsInstalled(Settings, other));
    }

    // Ownership has to stay path-agnostic. RemoveManagedHandlers strips Halo's entries whatever path they
    // carry, including a dead one, or an uninstall would leave nine handlers behind pointing at nothing.
    [Fact]
    public void Uninstall_still_strips_handlers_that_name_a_deleted_exe()
    {
        ClaudeHookInstaller.Install(Settings, _exe);
        File.Delete(_exe);

        ClaudeHookInstaller.Uninstall(Settings);

        Assert.DoesNotContain("Halo.Hooks.exe", File.ReadAllText(Settings));
    }
}
