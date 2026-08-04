using Halo.ClaudeCode;
using Xunit;

namespace Halo.Tests;

// This decides whether Halo edits a file belonging to another program, unprompted. Every one of these is a
// way that could go wrong quietly, so the gate is a test rather than something read off Frame() later.
public class HookConnectTests
{
    // The probes are the real thing's cost, so they are functions here too - there is no plain-bool
    // overload to test against any more, deliberately: the one that existed was what a new caller would
    // have copied, and copying it is how the eager probing came back.
    private static HookConnect.Step Next(bool seen = true, bool installed = false,
        bool tried = false, bool undone = false, bool busy = false)
        => HookConnect.Next(busy, tried, undone, () => seen, () => installed);

    [Fact]
    public void Installs_when_the_agent_turns_up_unhooked()
        => Assert.Equal(HookConnect.Step.Install, Next());

    [Fact]
    public void Does_nothing_before_the_agent_is_actually_there()
        => Assert.Equal(HookConnect.Step.Wait, Next(seen: false));

    [Fact]
    public void Does_nothing_when_the_hooks_are_already_in_place()
        => Assert.Equal(HookConnect.Step.Wait, Next(installed: true));

    // once per agent, not once per frame - Frame() runs at up to 240fps and this writes a file
    [Fact]
    public void Fires_once_rather_than_every_frame()
        => Assert.Equal(HookConnect.Step.Wait, Next(tried: true));

    // the one that matters: a user who undid the connection has answered, and re-applying it on the next
    // launch would be the app overruling them
    [Fact]
    public void Never_reinstalls_what_the_user_undid()
    {
        Assert.Equal(HookConnect.Step.Wait, Next(undone: true));
        Assert.Equal(HookConnect.Step.Wait, Next(undone: true, tried: false));
    }

    [Fact]
    public void Undone_outranks_a_missing_hook_set()
        => Assert.Equal(HookConnect.Step.Wait, Next(installed: false, undone: true));

    // A connection that did not happen must not be recorded as one: the mark is what stops Halo offering
    // again, so "done" for a failure is a permanent silence on the machine that most needed the offer.
    [Fact]
    public void A_failed_attempt_writes_no_mark()
        => Assert.Null(HookConnect.MarkFor(installed: false));

    // The literal rather than HookMarks.Done, and on purpose: this is a wire format. hooks-connect.txt
    // outlives the build that wrote it, so a rename would orphan every existing file rather than migrate
    // it, and a test that reads the constant would agree with the rename and notice nothing.
    [Fact]
    public void A_successful_attempt_is_recorded_as_done()
        => Assert.Equal("done", HookConnect.MarkFor(installed: true));

    // an install already in flight must not be started a second time - two writers on one settings.json is
    // how a half-written file happens, and a half-written settings.json is a broken agent
    [Fact]
    public void Does_not_start_a_second_install_over_a_running_one()
        => Assert.Equal(HookConnect.Step.Wait, Next(busy: true));

    // The cost of the decision, pinned. AgentSeen enumerates every process and HooksInstalled launches a
    // child process, and both used to be computed before the gates that make them irrelevant - two process
    // launches every five seconds, for the life of the app, to be told what a mark on disk already said.
    // Counting the calls is the only way this stays fixed: nothing about the returned Step would change.
    [Fact]
    public void A_decided_agent_is_not_probed_at_all()
    {
        foreach (var (tried, undone, busy) in new[] { (true, false, false), (false, true, false), (false, false, true) })
        {
            int seen = 0, installed = 0;
            var step = HookConnect.Next(busy, tried, undone,
                agentSeen: () => { seen++; return true; },
                hooksInstalled: () => { installed++; return false; });

            Assert.Equal(HookConnect.Step.Wait, step);
            Assert.Equal(0, seen);
            Assert.Equal(0, installed);
        }
    }

    // and the child process specifically: asking whether the hooks are installed costs 50ms, and an agent
    // that is not even running cannot need them
    [Fact]
    public void An_absent_agent_is_never_asked_about_its_hooks()
    {
        int installed = 0;
        var step = HookConnect.Next(busy: false, alreadyTried: false, undone: false,
            agentSeen: () => false,
            hooksInstalled: () => { installed++; return false; });

        Assert.Equal(HookConnect.Step.Wait, step);
        Assert.Equal(0, installed);
    }

    // the probes still run - and in this order - when the answer genuinely needs them
    [Fact]
    public void An_agent_that_turns_up_unhooked_is_probed_once_each()
    {
        int seen = 0, installed = 0;
        var step = HookConnect.Next(busy: false, alreadyTried: false, undone: false,
            agentSeen: () => { seen++; return true; },
            hooksInstalled: () => { installed++; return false; });

        Assert.Equal(HookConnect.Step.Install, step);
        Assert.Equal(1, seen);
        Assert.Equal(1, installed);
    }

    // The body is trimmed to the banner's width, so its length is not cosmetic: the first wording was cut
    // mid-sentence and the backup - the answer to "can I undo this" - never appeared on screen at all.
    [Fact]
    public void The_notice_names_the_file_and_the_backup_and_stays_short()
    {
        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        var (app, title, body) = HookConnect.Notice("Claude Code",
            System.IO.Path.Combine(home, ".claude", "settings.json"));

        Assert.Equal("Claude Code", app);
        Assert.Equal("Claude Code connected", title);
        Assert.Contains(".claude", body);
        Assert.Contains(".halo-bak", body);
        Assert.DoesNotContain(home, body);          // the absolute path is what overflowed
        Assert.True(body.Length <= 90, $"body is {body.Length} chars and will be trimmed: {body}");
    }

    // a path outside the home directory has nothing to shorten and must be left alone rather than mangled
    [Fact]
    public void A_path_outside_home_is_left_as_it_is()
        => Assert.Equal(@"D:\shared\settings.json", HookConnect.Short(@"D:\shared\settings.json"));

    // a failure has to say nothing changed, or the user is left believing their config was edited
    [Fact]
    public void A_failure_says_nothing_was_changed()
    {
        var (_, title, body) = HookConnect.Failed("Codex", "access denied");
        Assert.Contains("Could not connect Codex", title);
        Assert.Contains("access denied", body);
        Assert.Contains("Nothing was changed", body);

        var (_, _, bare) = HookConnect.Failed("Codex", "");
        Assert.Contains("Nothing was changed", bare);
    }
}
