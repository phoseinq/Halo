using System;
using System.Drawing;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The download pill's colour following its speed: the rate is derived from the byte count rather than invented,
// the level drifts rather than jumps, and the colour deepens toward violet without ever losing the app it
// belongs to. Each of those three is a rule this project has broken before somewhere else.
public class DlTintTests
{
    // Nothing hands over a per-download rate, so it comes from differencing the same bytes the percentage does.
    [Fact]
    public void The_rate_is_measured_across_the_gap_between_two_readings()
    {
        var rate = new DlRate();
        Assert.Equal(0f, rate.Sample(1_000_000, 0));          // first reading is an anchor, not a speed
        Assert.Equal(2_000_000f, rate.Sample(3_000_000, 1000), 1f);   // 2 MB in one second
        Assert.Equal(4_000_000f, rate.Sample(5_000_000, 1500), 1f);   // 2 MB in half of one
    }

    // The scanner polls far slower than the pill draws, so most frames see the same count. Holding the last
    // measurement is what stops the colour strobing between a burst and nothing.
    [Fact]
    public void An_unchanged_reading_holds_the_last_rate_until_it_is_a_stall()
    {
        var rate = new DlRate();
        rate.Sample(0, 0);
        Assert.Equal(1_000_000f, rate.Sample(1_000_000, 1000), 1f);
        Assert.Equal(1_000_000f, rate.Sample(1_000_000, 1000 + DlRate.StallMs), 1f);
        Assert.Equal(0f, rate.Sample(1_000_000, 1001 + DlRate.StallMs));
    }

    // The pill follows whichever download the scanner ranks first, and the new one's byte count has nothing to
    // do with the old one's - a smaller number is a different subject, not negative progress.
    [Fact]
    public void Bytes_going_backwards_re_anchor_instead_of_reading_as_a_rate()
    {
        var rate = new DlRate();
        rate.Sample(9_000_000, 0);
        rate.Sample(9_500_000, 1000);
        Assert.Equal(0f, rate.Sample(40_000, 1500));
        Assert.Equal(120_000f, rate.Sample(100_000, 2000), 1f);
    }

    [Fact]
    public void The_level_drifts_toward_its_target_and_back_down_again()
    {
        var drift = new Drift();
        drift.Step(1f, 1.6f, 1000);                     // first step seeds the clock, one 16ms step of travel
        float early = drift.Level;
        Assert.True(early is > 0f and < 0.05f, $"one frame should barely move: {early}");

        long t = 1000;
        for (int i = 0; i < 200; i++) drift.Step(1f, 1.6f, t += 16);
        Assert.True(drift.Level > 0.85f, $"~3.2s should be most of the way: {drift.Level}");

        for (int i = 0; i < 200; i++) drift.Step(0f, 1.6f, t += 16);
        Assert.True(drift.Level < 0.15f, $"and it has to come back down: {drift.Level}");
    }

    // The bug this guard was written for: during a morph the collapsed layer and the panel are drawn in the
    // same frame, and a drift stepped twice per frame runs at double speed.
    [Fact]
    public void A_second_step_inside_the_same_tick_does_not_advance_it()
    {
        var drift = new Drift();
        drift.Step(1f, 1.6f, 5000);
        long t = 5000;
        for (int i = 0; i < 50; i++) drift.Step(1f, 1.6f, t += 16);
        float once = drift.Level;

        var twice = new Drift();
        twice.Step(1f, 1.6f, 5000);
        t = 5000;
        for (int i = 0; i < 50; i++) { twice.Step(1f, 1.6f, t += 16); twice.Step(1f, 1.6f, t); }
        Assert.Equal(once, twice.Level, 0.0001f);
    }

    [Fact]
    public void Seeding_lands_on_the_level_with_no_travel()
    {
        var drift = new Drift();
        drift.Seed(0.7f, 4242);
        Assert.Equal(0.7f, drift.Level);
        Assert.Equal(0.7f, drift.Step(0f, 1.6f, 4242));   // same tick: a hook's one frame must not drift off it
    }

    // Recolouring the BAR was the first attempt, and --render-dlspeed threw it out: an app accent and a violet
    // are near-complements, so every smooth path between them either greys out in the middle or detours through
    // crimson (which is cancel here) or green (which is an agent doing fine). The RIM starts from slate, so it
    // has nothing to detour around and the pill keeps wearing the colour of the app the file comes from.
    [Fact]
    public void The_rim_runs_from_slate_to_deep_violet()
    {
        var idle = DownloadWidget.RimColor(0f);
        var fast = DownloadWidget.RimColor(1f);
        Assert.Equal(Color.FromArgb(236, 38, 50, 58), idle);
        Assert.Equal(Color.FromArgb(255, 96, 38, 176), fast);

        Fx.RgbToHsv(fast, out float h, out float s, out _);
        Assert.True(h is > 258f and < 280f, $"the end of the ramp has to actually be violet: {h}");
        Assert.True(s > 0.7f, $"and saturated enough to read as a colour rather than a grey: {s}");
    }

    [Fact]
    public void The_rim_only_ever_deepens_and_never_brightens()
    {
        // A rim reads against the DESKTOP behind the pill, so anything getting lighter glows like a strip light
        // at the top of the screen. The level has to show as the slate deepening into violet instead.
        var previous = DownloadWidget.RimColor(0f);
        for (float level = 0.05f; level <= 1.0001f; level += 0.05f)
        {
            var now = DownloadWidget.RimColor(level);
            Assert.True(now.B > previous.B, $"level {level:0.00}: blue has to keep climbing");
            Assert.True(now.G <= previous.G, $"level {level:0.00}: nothing here gets brighter");
            Assert.True(now.A >= previous.A, $"level {level:0.00}: and it stays opaque");
            previous = now;
        }
        Assert.Equal(DownloadWidget.RimColor(1f), DownloadWidget.RimColor(4f));   // clamped
    }

}
