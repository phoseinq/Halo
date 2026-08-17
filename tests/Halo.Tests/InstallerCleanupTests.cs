using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Halo.Tests;

// Uninstalling has to leave the machine as it was found, and the way that promise breaks is never a wrong
// teardown - it is a teardown nobody wired up. Halo.Hooks grew `uninstall-claude-hooks` and the settings
// panel called it, but [UninstallRun] was never told, so removing Halo left nine hook handlers in
// ~/.claude/settings.json pointing at an exe that no longer existed. Every tool call in every later Claude
// Code session then spawned a process that could not start. Found live: the hooks kept firing at a deleted
// %LOCALAPPDATA%\Programs\Halo\Halo.Hooks.exe for two and a half minutes until a fresh install rewrote them.
//
// Asserting on the script rather than on a C# helper is the point. The verb existed and worked; the defect
// was in the one file no test looked at, and the codex twin beside it was correct the whole time - exactly
// the asymmetry that keeps costing this repo (see the agents note in CLAUDE.md).
public class InstallerCleanupTests
{
    private static string? Script
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Halo.sln"))) dir = dir.Parent;
            if (dir is null) return null;
            string path = Path.Combine(dir.FullName, "installer", "Halo.iss");
            // the published tree is an allowlist of src/tests/Halo.sln/Directory.Build.props and carries no
            // installer/, so there this has nothing to check rather than something to fail on
            return File.Exists(path) ? path : null;
        }
    }

    // A section ends at the next line that STARTS with '[', not at the next '[' anywhere. Scanning for the
    // bare character read a section header out of the middle of a comment - the first version of this test
    // truncated [UninstallRun] at a comment that mentioned [Run], and then happily reported the four steps
    // below the cut as missing. Inno's own grammar is the line-anchored one.
    private static string UninstallRun(string path)
    {
        var lines = File.ReadAllLines(path);
        int start = Array.FindIndex(lines, l => l.Trim() == "[UninstallRun]");
        Assert.True(start >= 0, "Halo.iss must have an [UninstallRun] section");
        int end = Array.FindIndex(lines, start + 1, l => l.TrimStart().StartsWith('['));
        if (end < 0) end = lines.Length;
        return string.Join('\n', lines[start..end]);
    }

    // Everything Halo writes outside its own install folder, and therefore everything that outlives it if
    // the uninstaller stays quiet. Each entry is a change to somebody else's config or to the OS.
    public static IEnumerable<object[]> Teardowns() => new[]
    {
        new object[] { "uninstall-claude-hooks" },   // ~/.claude/settings.json
        new object[] { "uninstall-codex-hooks" },    // ~/.codex/config.toml
        new object[] { "uninstall-autostart" },      // the logon scheduled task
        new object[] { "--restore-notifications" },  // every app's ShowBanner
        new object[] { "--report-clear" },           // stored bug reports
    };

    [Theory]
    [MemberData(nameof(Teardowns))]
    public void The_uninstaller_undoes_everything_Halo_wrote_outside_itself(string parameter)
    {
        string? path = Script;
        if (path is null) return;
        Assert.Contains(parameter, UninstallRun(path), StringComparison.Ordinal);
    }

    // The state folder is the other half of "as it was found", and for four releases the privacy statement
    // promised its deletion while no [UninstallDelete] existed at all. Named here because the promise is
    // public: an accidental deletion of this section would put the documentation back into a lie silently.
    [Fact]
    public void Uninstalling_takes_the_state_folder_with_it()
    {
        string? path = Script;
        if (path is null) return;
        string text = File.ReadAllText(path);
        Assert.Contains("[UninstallDelete]", text, StringComparison.Ordinal);
        Assert.Contains("{localappdata}\\Halo", text, StringComparison.Ordinal);
    }

    // --restore-notifications reads banner-orig.tsv out of the folder [UninstallDelete] removes, so the run
    // steps have to come first in the file. They do for a second reason too - they execute {app}\Halo.App.exe,
    // which has to still be on disk - but the ordering is load-bearing enough to pin, since reversing the two
    // sections would strand every app the gate ever silenced.
    [Fact]
    public void The_run_steps_come_before_the_folder_they_read_from_is_deleted()
    {
        string? path = Script;
        if (path is null) return;
        string text = File.ReadAllText(path);
        Assert.True(text.IndexOf("[UninstallRun]", StringComparison.Ordinal)
                  < text.IndexOf("[UninstallDelete]", StringComparison.Ordinal));
    }

    // A RunOnceId is what makes an [UninstallRun] step survive being upgraded over: without one, Inno drops
    // the entry from the old uninstaller's log and the step silently stops running on the machines that have
    // been upgrading the longest - the ones most likely to have a full ledger to put back.
    [Fact]
    public void Every_uninstall_step_carries_a_RunOnceId()
    {
        string? path = Script;
        if (path is null) return;
        foreach (var line in UninstallRun(path).Split('\n'))
        {
            if (!line.TrimStart().StartsWith("Filename:", StringComparison.Ordinal)) continue;
            Assert.Contains("RunOnceId:", line, StringComparison.Ordinal);
        }
    }
}
