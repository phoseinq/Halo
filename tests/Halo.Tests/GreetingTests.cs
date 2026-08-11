extern alias settingsasm;
using System;
using System.Globalization;
using System.IO;
using Halo.Shell;
using Xunit;

namespace Halo.Tests;

public class GreetingGateTests
{
    private const string Version = "3.1.7.0";
    private static readonly DateOnly Today = new(2026, 8, 7);

    private static GreetingKind Decide(string? marker, bool enabled = true)
        => GreetingGate.Decide(GreetingGate.Parse(marker), Version, Today, arriving: false, enabled);

    [Fact]
    public void No_marker_means_this_build_has_never_run_here()
        => Assert.Equal(GreetingKind.Install, Decide(null));

    // the marker used to hold a boot timestamp; an upgrade reads that and must treat it as "not this build"
    [Fact]
    public void A_marker_it_cannot_recognise_is_a_new_build()
        => Assert.Equal(GreetingKind.Install, Decide("2026-07-31T17:11:05.4669755Z"));

    [Fact]
    public void An_upgrade_introduces_itself()
        => Assert.Equal(GreetingKind.Install, Decide("3.1.6.0"));

    // Every marker written before the ration existed is one line and no day, so an installed build that
    // upgrades into this one must read as "same build, has not said hello today" - one hand, not the
    // ten-second introduction and not silence.
    [Fact]
    public void A_marker_from_before_the_ration_gets_exactly_one_hand()
        => Assert.Equal(GreetingKind.Login, Decide(Version));

    [Fact]
    public void The_first_arrival_of_the_day_gets_the_hand()
        => Assert.Equal(GreetingKind.Login, Decide(Version + "\n2026-08-06"));

    // The report this whole thing exists for: a laptop set to sleep ten minutes after you stand up woke
    // to a hand every single time you came back to it.
    [Fact]
    public void A_second_arrival_the_same_day_gets_nothing()
        => Assert.Equal(GreetingKind.None, Decide(Version + "\n2026-08-07"));

    // whitespace happens to a file written by a text editor; it is not a different build
    [Fact]
    public void The_marker_is_compared_trimmed()
        => Assert.Equal(GreetingKind.Login, Decide(" 3.1.7.0\r\n"));

    // Off means off, including the one greeting that has something new to say. A user who switched the
    // hand off and then upgraded did not ask for ten seconds of signature.
    [Fact]
    public void The_switch_silences_the_introduction_too()
    {
        Assert.Equal(GreetingKind.None, Decide(null, enabled: false));
        Assert.Equal(GreetingKind.None, Decide(Version + "\n2026-08-06", enabled: false));
    }

    // A second line it cannot read must cost the ration and nothing else. Reading it as a version change
    // would turn a corrupt byte into the ten-second introduction on every launch.
    [Fact]
    public void A_day_it_cannot_read_is_a_missing_day_and_not_a_new_build()
        => Assert.Equal(GreetingKind.Login, Decide(Version + "\nnot a day"));

    // The machine this was reported on runs a Persian locale, where a culture-formatted date writes a year
    // 621 off and parses back as nothing - which reads as "no hello yet today", on every single wake.
    [Fact]
    public void The_day_is_written_and_read_in_one_calendar_whatever_the_machine_is_set_to()
    {
        var was = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fa-IR");
            string text = GreetingGate.Format(new GreetingMark(Version, Today));
            Assert.Equal(Version + "\n2026-08-07", text);
            Assert.Equal(Today, GreetingGate.Parse(text).Last);
            Assert.Equal(GreetingKind.None, Decide(text));
        }
        finally { CultureInfo.CurrentCulture = was; }
    }

    // a real version, not "0": the fallback would make every launch an install
    [Fact]
    public void The_running_build_reports_a_version()
    {
        Assert.NotEqual("0", GreetingGate.Version);
        Assert.Equal(GreetingKind.Login,
            GreetingGate.Decide(new GreetingMark(GreetingGate.Version, null), GreetingGate.Version, Today,
                                arriving: false, true));
    }

    // The rule end to end, through the file the pill actually keeps: two arrivals in a day, one hand.
    [Fact]
    public void Two_arrivals_in_one_day_leave_one_hand_between_them()
    {
        var (dir, path) = TempMarker();
        try
        {
            GreetingGate.Write(path, new GreetingMark(GreetingGate.Version, null));
            Assert.Equal(GreetingKind.Login, GreetingGate.Take(path, Today, arriving: false, true));
            Assert.Equal(GreetingKind.None, GreetingGate.Take(path, Today, arriving: false, true));
            Assert.Equal(GreetingKind.Login, GreetingGate.Take(path, Today.AddDays(1), arriving: false, true));
        }
        finally { Scrub(dir); }
    }

    // Stamping the day on every Take regardless would let a launch with the hand switched off eat the
    // day's one greeting, so switching it back on would show nothing until tomorrow.
    [Fact]
    public void A_hand_the_switch_refused_does_not_spend_the_day()
    {
        var (dir, path) = TempMarker();
        try
        {
            GreetingGate.Write(path, new GreetingMark(GreetingGate.Version, null));
            Assert.Equal(GreetingKind.None, GreetingGate.Take(path, Today, arriving: false, false));
            Assert.Equal(GreetingKind.Login, GreetingGate.Take(path, Today, arriving: false, true));
        }
        finally { Scrub(dir); }
    }

    // A state directory that cannot be written costs a greeting, not a launch - so Take has to answer even
    // when nothing it does can be persisted, and the answer has to be the safe one.
    [Fact]
    public void A_marker_that_cannot_be_written_still_answers()
    {
        string path = Path.Combine(Path.GetTempPath(), "halo-greet-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);   // a DIRECTORY where the marker file should be
        try { Assert.Equal(GreetingKind.Install, GreetingGate.Take(path, Today, arriving: false, true)); }
        finally { Scrub(path); }
    }

    // The switch is written by one executable and read by another, which is as far apart as two readers
    // get here: a typo leaves a toggle that slides, saves, and is never read by the thing it names - and
    // nothing anywhere would say so. Its default is pinned on the same grounds; a row defaulting off
    // against a reader defaulting on is a hand that disappears for everyone who never opened the panel.
    [Fact]
    public void The_panel_writes_the_greeting_key_the_pill_reads()
    {
        bool found = false;
        foreach (var page in settingsasm::Halo.Settings.Catalog.Pages)
            foreach (var section in page.Sections)
                foreach (var row in section.Rows)
                    if (row.Key == Halo.Settings.SettingsKeys.Greeting)
                    {
                        found = true;
                        Assert.Equal("on", row.Fallback);
                    }
        Assert.True(found, "no settings row writes " + Halo.Settings.SettingsKeys.Greeting);
    }

    private static (string dir, string path) TempMarker()
    {
        string dir = Path.Combine(Path.GetTempPath(), "halo-greet-" + Guid.NewGuid().ToString("n"));
        return (dir, Path.Combine(dir, "greeted"));
    }

    private static void Scrub(string dir) { try { Directory.Delete(dir, true); } catch { } }

    // Long enough that no stall, alt-tab storm or fps tier can reach it, short enough that a nap does.
    [Fact]
    public void A_wake_is_a_gap_no_running_frame_could_produce()
        => Assert.True(NotchController.WakeGap >= TimeSpan.FromSeconds(60));
}

public class ScriptTests
{
    // The failure this catches is silent by construction: a stroke authored one point short still parses,
    // still draws, and just loses its last curve. "e" shipped that way and turned "welcome" into a word
    // that read as "welcomp".
    [Fact]
    public void Every_stroke_is_a_start_point_followed_by_whole_cubics()
    {
        foreach (var (c, i, n) in Halo.Widgets.Script.Strokes())
        {
            Assert.True(n >= 8, $"'{c}' stroke {i} has no curve at all");
            Assert.True((n - 2) % 6 == 0,
                $"'{c}' stroke {i} has {n} numbers - {(n - 2) % 6} short of a whole cubic, so its tail is dropped");
        }
    }

    [Fact]
    public void The_hand_can_write_every_line_the_greeting_uses()
    {
        foreach (var line in Halo.Widgets.Greeting.Lines)
            Assert.True(Halo.Widgets.Script.Can(line), $"the hand is missing a letter of \"{line}\"");
    }

    [Fact]
    public void A_wider_word_measures_wider()
        => Assert.True(Halo.Widgets.Script.Width("welcome") > Halo.Widgets.Script.Width("i'm"));
}

public class GreetingPlanTests
{
    private static float[] Clock() =>
        [0f, 0.05f, 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 0.95f, 1f];

    // A signature has one end. If the pen ever went backwards the ink would rub itself out mid-word, and
    // an overshooting ease is exactly how that happens - so the monotonicity is pinned, not assumed.
    [Fact]
    public void The_pen_never_goes_backwards()
    {
        float last = -1f;
        foreach (float t in Clock())
        {
            float w = GreetingPlan.Install(t).Written;
            Assert.True(w >= last, $"written went backwards at t={t}");
            last = w;
        }
    }

    [Fact]
    public void The_pen_never_overshoots_the_end_of_the_path()
    {
        foreach (float t in Clock())
            Assert.InRange(GreetingPlan.Install(t).Written, 0f, 1f);
    }

    [Fact]
    public void The_install_pill_is_never_smaller_than_a_collapsed_one()
    {
        foreach (float t in Clock())
        {
            var f = GreetingPlan.Install(t);
            Assert.True(f.PillW >= GreetingPlan.CollapsedW - 0.01f, $"too narrow at t={t}");
            Assert.True(f.PillH >= GreetingPlan.CollapsedH - 0.01f, $"too short at t={t}");
        }
    }

    [Fact]
    public void The_login_greeting_never_opens_the_pill()
    {
        foreach (float t in Clock())
        {
            var f = GreetingPlan.Login(t);
            Assert.Equal(GreetingPlan.CollapsedW, f.PillW);
            Assert.Equal(GreetingPlan.CollapsedH, f.PillH);
            Assert.Equal(0f, f.LineAlpha);
        }
    }

    // Both greetings have to leave the pill exactly as they found it, or whatever was showing before is
    // stuck behind a half-faded word.
    [Fact]
    public void Both_greetings_end_with_an_empty_collapsed_pill()
    {
        var install = GreetingPlan.Install(1f);
        Assert.Equal(GreetingPlan.CollapsedW, install.PillW, 1);
        Assert.Equal(0f, install.LineAlpha, 2);
        Assert.Equal(0f, install.HelloAlpha, 2);

        var login = GreetingPlan.Login(1f);
        Assert.Equal(0f, login.HelloAlpha, 2);
    }

    // The two lines cross over rather than meeting exactly: a frame with neither on the page reads as the
    // animation having stopped. What must never happen is the FIRST line reappearing after the second.
    [Fact]
    public void The_second_line_replaces_the_first_and_never_the_other_way_round()
    {
        bool sawSecond = false;
        for (float t = 0.5f; t <= 1f; t += 0.01f)
        {
            var f = GreetingPlan.Install(t);
            if (f.LineIndex == 1 && f.LineAlpha > 0.01f) sawSecond = true;
            else if (sawSecond && f.LineIndex == 0 && f.LineAlpha > 0.01f)
                Assert.Fail($"the first line came back at t={t}");
        }
        Assert.True(sawSecond, "the second line never appeared at all");
    }

    [Fact]
    public void The_signature_is_gone_before_the_first_line_is_fully_up()
    {
        for (float t = 0f; t <= 1f; t += 0.01f)
        {
            var f = GreetingPlan.Install(t);
            Assert.True(f.HelloAlpha < 0.5f || f.LineAlpha < 0.5f,
                $"the signature and a line were both solid at t={t}");
        }
    }
}

// Signing in is exempt from the daily ration, which is the whole of the fix for "it didn't say hello after
// the boot": a development session spends the day's one hand long before the evening reboot.
public class SigninGreetingTests
{
    private static readonly DateOnly Today = new(2026, 8, 11);
    private static string Version => GreetingGate.Version;

    [Fact]
    public void Arriving_says_hello_even_though_today_already_had_one()
    {
        var spent = new GreetingMark(Version, Today);

        Assert.Equal(GreetingKind.None, GreetingGate.Decide(spent, Version, Today, false, true));
        Assert.Equal(GreetingKind.Signin, GreetingGate.Decide(spent, Version, Today, true, true));
    }

    // A build that has never run here has something to say that is not "hello", and it outranks the arrival.
    [Fact]
    public void A_new_version_still_introduces_itself_rather_than_only_waving()
        => Assert.Equal(GreetingKind.Install,
                        GreetingGate.Decide(new GreetingMark("0.0.0.1", Today), Version, Today, true, true));

    [Fact]
    public void The_switch_still_silences_an_arrival()
        => Assert.Equal(GreetingKind.None,
                        GreetingGate.Decide(new GreetingMark(Version, null), Version, Today, true, false));

    // The pill opens for this one - that is the point of it, and the request was "make it grow".
    [Fact]
    public void The_signin_pill_opens_wider_than_collapsed_and_comes_back()
    {
        Assert.Equal(GreetingPlan.CollapsedW, GreetingPlan.Signin(0f).PillW, 1);
        Assert.True(GreetingPlan.Signin(0.5f).PillW > GreetingPlan.CollapsedW + 100f,
                    $"got {GreetingPlan.Signin(0.5f).PillW}");
        Assert.Equal(GreetingPlan.CollapsedW, GreetingPlan.Signin(1f).PillW, 1);
    }

    [Fact]
    public void The_signin_pen_never_goes_backwards_and_never_shrinks_the_pill_below_collapsed()
    {
        float pen = 0f;
        for (float t = 0f; t <= 1.0001f; t += 0.01f)
        {
            var f = GreetingPlan.Signin(t);
            Assert.True(f.Written >= pen - 1e-4f, $"the pen went backwards at {t}");
            Assert.True(f.PillW >= GreetingPlan.CollapsedW - 0.5f, $"narrower than collapsed at {t}");
            Assert.True(f.PillH >= GreetingPlan.CollapsedH - 0.5f, $"shorter than collapsed at {t}");
            pen = f.Written;
        }
    }

    // It says hello and nothing else: the two lines of introduction are what makes install a ten-second
    // speech, and one of those at every sign-in is a different request from the one that was made.
    [Fact]
    public void The_signin_greeting_writes_no_introduction_lines()
    {
        for (float t = 0f; t <= 1.0001f; t += 0.02f)
        {
            Assert.Equal(0f, GreetingPlan.Signin(t).LineWritten);
            Assert.Equal(0f, GreetingPlan.Signin(t).LineAlpha);
        }
    }

    [Fact]
    public void Each_kind_gets_its_own_animation_and_length()
    {
        Assert.Equal(GreetingPlan.SigninSeconds, GreetingPlan.SecondsOf(GreetingKind.Signin));
        Assert.Equal(GreetingPlan.InstallSeconds, GreetingPlan.SecondsOf(GreetingKind.Install));
        Assert.Equal(GreetingPlan.LoginSeconds, GreetingPlan.SecondsOf(GreetingKind.Login));
        // the login hand never opens the pill, the sign-in one does - that is how they differ
        Assert.Equal(GreetingPlan.CollapsedW, GreetingPlan.Of(GreetingKind.Login, 0.5f).PillW, 1);
        Assert.True(GreetingPlan.Of(GreetingKind.Signin, 0.5f).PillW > GreetingPlan.CollapsedW);
    }
}
