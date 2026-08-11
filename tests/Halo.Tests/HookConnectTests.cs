using Halo.ClaudeCode;
using Xunit;

namespace Halo.Tests;

// This decides whether Halo edits a file belonging to another program, unprompted. Every one of these is a
// way that could go wrong quietly, so the gate is a test rather than something read off Frame() later.
//
// Serialised against the other classes that touch it: one case asserts the English notice text, and
// Strings.Use switches the ACTIVE LANGUAGE for the whole process. Without the collection this class ran in
// parallel with the ones that switch to Persian and failed about one run in four, reading the Persian
// notice back where it expected "Claude Code connected".
[Collection("locale")]
public class HookConnectTests
{
    // The probes are the real thing's cost, so they are functions here too - there is no plain-bool
    // overload to test against any more, deliberately: the one that existed was what a new caller would
    // have copied, and copying it is how the eager probing came back.
    private static HookConnect.Step Next(bool? seen = true, bool? installed = false,
        bool tried = false, bool undone = false, bool busy = false)
        => HookConnect.Next(busy, tried, undone, () => seen == true, () => installed);

    // The probe could not answer. Installing on this is Halo writing nine handlers into settings.json
    // because it failed to look - unattended, on a repeating scan, which is why this matters more here
    // than in the panel where a person at least pressed something.
    [Fact]
    public void Never_installs_on_a_probe_that_did_not_answer()
        => Assert.Equal(HookConnect.Step.Wait, Next(installed: null));

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

    // One failed attempt used to settle the agent for the whole session, so the commonest cause - the agent
    // holding its own settings.json open while we read it - cost the user every session until they restarted
    // Halo. The retry has to be bounded and it has to back off, or it is the tight loop the scan interval
    // exists to prevent: each attempt is two child processes.
    [Fact]
    public void The_retry_backs_off_and_never_shortens()
    {
        int previous = 0;
        for (int attempt = 1; attempt <= HookConnect.MaxAttempts; attempt++)
        {
            int delay = HookConnect.RetryDelayMs(attempt);
            Assert.True(delay >= previous, $"attempt {attempt} waited less than attempt {attempt - 1}");
            Assert.True(delay > 0);
            previous = delay;
        }
    }

    // Four banners about one problem is worse than one, and the retry is likely to fix it - so only the
    // attempt that gives up is allowed to speak.
    [Fact]
    public void Only_the_attempt_that_gives_up_reports()
    {
        for (int attempt = 1; attempt < HookConnect.MaxAttempts; attempt++)
            Assert.False(HookConnect.ShouldReport(attempt), $"attempt {attempt} should stay quiet");
        Assert.True(HookConnect.ShouldReport(HookConnect.MaxAttempts));
    }

    // A failure must never write a mark: the mark is what records that Halo connected this agent, and the
    // whole point of retrying is that a machine which failed today can succeed tomorrow.
    [Fact]
    public void A_failure_writes_no_mark_so_the_next_launch_tries_again()
    {
        Assert.Null(HookConnect.MarkFor(installed: false));
        Assert.Equal(HookMarks.Done, HookConnect.MarkFor(installed: true));
    }

    // "Halo connected this once" must not stop it connecting again. Found on a packaged install reading a
    // mark the ordinary one had written: the handlers were gone and it refused to put them back, because
    // the mark said done. Whether they are there NOW is the probe's question, and only the user's explicit
    // Disconnect - `undone` - is allowed to be permanent.
    [Fact]
    public void A_previous_connection_does_not_block_a_missing_one_from_being_restored()
    {
        var step = HookConnect.Next(busy: false, alreadyTried: false, undone: false,
            agentSeen: () => true, hooksInstalled: () => false);
        Assert.Equal(HookConnect.Step.Install, step);

        // and the user's no still is
        Assert.Equal(HookConnect.Step.Wait, HookConnect.Next(busy: false, alreadyTried: false, undone: true,
            agentSeen: () => true, hooksInstalled: () => false));
    }

    // The banner went out with no icon at all for as long as it had existed, and the sample sheet hid it by
    // supplying its own badge. These pin the builder BOTH now use, so the preview cannot be right while the
    // thing users see is wrong.
    [Fact]
    public void Every_hook_banner_carries_a_badge()
    {
        foreach (bool ok in new[] { true, false })
        {
            var item = Halo.Shell.NotchController.HookBanner(("Claude Code", "t", "b"), ok);
            Assert.NotNull(item.Icon);
        }
    }

    // success and failure must not land on the same mark - a red badge on "connected" reads as an error
    [Fact]
    public void A_failed_connection_does_not_wear_the_connected_badge()
    {
        var good = Halo.Shell.NotchController.HookBanner(("Claude Code", "t", "b"), ok: true);
        var bad = Halo.Shell.NotchController.HookBanner(("Claude Code", "t", "b"), ok: false);
        Assert.NotEqual(Tone(good.Icon!), Tone(bad.Icon!));
    }

    // the whole sheet, not just the two entries above: an iconless local notice is the bug class, and the
    // sheet is what gets eyeballed before a release
    [Fact]
    public void No_sampled_local_notice_is_iconless()
    {
        using var shot = new System.Drawing.Bitmap(4, 4);
        foreach (var item in Halo.Shell.NotchController.SampleLocalNotices(shot))
            Assert.True(item.Icon != null || item.Preview != null, $"{item.App}/{item.Title} has nothing to draw");
    }

    // average colour, which is all that is needed to tell the green mark from the red one
    private static (int r, int g, int b) Tone(System.Drawing.Bitmap bmp)
    {
        long r = 0, g = 0, b = 0, n = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.A < 128) continue;   // the badge is mostly transparent; only the glyph counts
                r += p.R; g += p.G; b += p.B; n++;
            }
        return n == 0 ? (0, 0, 0) : ((int)(r / n), (int)(g / n), (int)(b / n));
    }
}
