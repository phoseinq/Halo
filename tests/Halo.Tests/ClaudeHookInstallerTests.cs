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
    private readonly string _exe = @"C:\Program Files\Halo\Halo.Hooks.exe";
    private string Settings => Path.Combine(_root, "settings.json");

    public ClaudeHookInstallerTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

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
}
