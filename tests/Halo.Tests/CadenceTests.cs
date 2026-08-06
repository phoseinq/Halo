using Halo.Shell;
using Xunit;

namespace Halo.Tests;

// The expand/collapse morph used to run at whatever tier the once-a-second CPU sampler had picked, which
// while the panel is open WAS a deliberate 60 - so the animation never saw 120, and a tier change landing
// inside the ~300ms morph switched cadence mid-movement. The 60 is gone as of the glass cache; the morph
// still takes the ceiling regardless of the tier, which is what these first two pin.
public class CadenceTests
{
    [Fact]
    public void A_morph_always_gets_the_ceiling_even_on_the_watching_tier()
        => Assert.Equal(NotchController.MaxFps, NotchController.CadenceFps(true, 60));

    [Fact]
    public void A_morph_outruns_the_slammed_tier_too()
        => Assert.Equal(NotchController.MaxFps, NotchController.CadenceFps(true, 30));

    // The morph is ~300ms, so running it flat out costs a third of a second of full rate. The settled panel
    // used to be the thing that had to stay at 60; it does not any more - see the Tier tests below for the
    // measurement that changed, and the top rung now resolves through the ceiling rather than stopping at
    // MaxFps.
    [Fact]
    public void The_ceiling_is_above_the_old_120_limit()
        => Assert.True(NotchController.MaxFps > 120);

    [Fact]
    public void Once_settled_the_measured_tier_stands()
    {
        Assert.Equal(60, NotchController.CadenceFps(false, 60));
        Assert.Equal(30, NotchController.CadenceFps(false, 30));
        Assert.Equal(120, NotchController.CadenceFps(false, 120));
    }

    // ...except the top one, which is a name for "flat out" rather than a measurement. A 280Hz display was
    // being held to 240 on every settled frame while the morph went through Reach and got all 280.
    [Fact]
    public void The_top_rung_settles_at_the_ceiling_not_at_MaxFps()
    {
        Assert.Equal(280, NotchController.CadenceFps(false, NotchController.MaxFps, 280));
        Assert.Equal(NotchController.MaxFps, NotchController.CadenceFps(false, NotchController.MaxFps, 0));
        // and a ceiling BELOW it still wins, through Capped as before
        Assert.Equal(120, NotchController.Capped(NotchController.CadenceFps(false, NotchController.MaxFps, 120), 120));
    }

    [Theory]
    [InlineData(280, 3.571)]
    [InlineData(240, 4.167)]
    [InlineData(144, 6.944)]
    [InlineData(120, 8.333)]
    [InlineData(60, 16.667)]
    [InlineData(30, 33.333)]
    public void Each_rate_maps_to_its_exact_period(int fps, double ms)
        => Assert.Equal(ms, NotchController.IntervalMs(fps), 3);

    // The reason the period stopped being rounded to whole milliseconds: 240 and 280 both landed on 4ms,
    // so the two choices were the same tick and picking the higher one did nothing at all.
    [Fact]
    public void Two_neighbouring_choices_do_not_collapse_onto_one_tick()
        => Assert.True(NotchController.IntervalMs(280) < NotchController.IntervalMs(240),
            "280 must ask for a shorter period than 240");

    // Picked above MaxFps, the setting has to RAISE what a morph reaches for - capping it away would make
    // the row a control that cannot be honoured.
    [Fact]
    public void A_rate_above_the_built_in_ceiling_is_still_reached_for()
    {
        Assert.Equal(280, NotchController.Reach(280));
        Assert.Equal(280, NotchController.CadenceFps(true, 60, 280));
    }

    [Fact]
    public void With_no_setting_a_morph_reaches_for_the_built_in_ceiling()
        => Assert.Equal(NotchController.MaxFps, NotchController.Reach(0));

    // A ceiling the user picks has to be honoured even when it is above what Halo would choose on its
    // own, and a shorter interval must never come out of a lower number.
    [Fact]
    public void A_higher_tier_never_asks_for_a_longer_interval()
    {
        int[] tiers = [30, 60, 120, 144, 240];
        for (int i = 1; i < tiers.Length; i++)
            Assert.True(NotchController.IntervalMs(tiers[i]) <= NotchController.IntervalMs(tiers[i - 1]),
                $"{tiers[i]}fps asked for a longer interval than {tiers[i - 1]}fps");
    }

    // The ceiling is the user's judgement about their hardware, which a CPU sample cannot make. It is
    // applied last, so it beats the morph's 120 too - otherwise the one moment a weak machine struggles
    // most would be the one moment the setting did not apply.
    [Fact]
    public void The_ceiling_holds_a_morph_down()
        => Assert.Equal(60, NotchController.Capped(NotchController.CadenceFps(true, 60), 60));

    // Ceiling, not target: capping at 60 must not stop a slammed machine dropping to 30.
    [Fact]
    public void A_tier_below_the_ceiling_is_left_alone()
        => Assert.Equal(30, NotchController.Capped(30, 60));

    [Fact]
    public void An_unknown_ceiling_caps_nothing()
    {
        Assert.Equal(120, NotchController.Capped(120, 0));
        Assert.Equal(NotchController.MaxFps, NotchController.Capped(NotchController.CadenceFps(true, 30), 0));
    }

    // Auto used to be MaxFps flat: 180 frames a second a 60Hz panel could not show, and 40 fewer than a
    // 280Hz one could.
    [Theory]
    [InlineData(280, 280)]
    [InlineData(165, 165)]
    [InlineData(60, 60)]
    [InlineData(24, 24)]
    public void Auto_is_whatever_the_display_refreshes_at(int hz, int expected)
        => Assert.Equal(expected, NotchController.AutoCeiling(hz));

    // 0 is "could not read it" and 1 is what some drivers report for "hardware default". Believing either
    // would pin the loop to a number nobody chose, which is the invented-value fault in another costume.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(23)]
    [InlineData(1001)]
    public void A_refresh_rate_that_cannot_be_true_falls_back(int hz)
        => Assert.Equal(NotchController.MaxFps, NotchController.AutoCeiling(hz));

    // The ordering here has been both ways round and the argument is worth keeping. Slammed used to win,
    // so that a game at 95% got its cores back - but Halo hides entirely over a fullscreen window, so that
    // mostly protected a case where Halo is not on screen, while costing the panel the user was looking at:
    // a scrolling title steps 1.4px a frame at 30 and 0.7px at 60, and 1.4px reads as unevenness. Watching
    // wins now, and these two are the pair that pins it.
    [Fact]
    public void An_open_panel_wins_over_a_slammed_machine()
        => Assert.Equal(60, NotchController.Tier(0.95f, watching: true, current: NotchController.MaxFps));

    [Fact]
    public void A_slammed_machine_still_yields_when_nobody_is_looking()
        => Assert.Equal(30, NotchController.Tier(0.95f, watching: false, current: NotchController.MaxFps));

    // It used to pin 60 here, and the reason was the cost of the glass: the composite was 13.84ms of a
    // 16.76ms frame. It is cached now and the frame is 1.84ms, so sixty frames of it cost 1006ms/s against
    // the ceiling's 442ms/s - the rung was buying back less than it spent. A panel with room reaches.
    [Fact]
    public void An_open_panel_reaches_for_the_ceiling_when_there_is_room()
        => Assert.Equal(NotchController.MaxFps, NotchController.Tier(0.20f, watching: true, current: 60));

    // and the one case that does NOT follow it up: watching still beats slammed, but not by taking a
    // quarter of a core from a machine at 95%
    [Fact]
    public void An_open_panel_on_a_slammed_machine_holds_sixty()
        => Assert.Equal(60, NotchController.Tier(0.95f, watching: true, current: NotchController.MaxFps));

    [Fact]
    public void A_busy_machine_holds_sixty()
        => Assert.Equal(60, NotchController.Tier(0.60f, watching: false, current: NotchController.MaxFps));

    [Fact]
    public void Headroom_reaches_for_the_ceiling()
        => Assert.Equal(NotchController.MaxFps, NotchController.Tier(0.20f, watching: false, current: 60));

    // The dead band is the reason the tier does not flap once a second on a machine sitting at half load.
    [Theory]
    [InlineData(0.45f)]
    [InlineData(0.50f)]
    [InlineData(0.55f)]
    public void Half_load_holds_whatever_it_already_had(float busy)
    {
        Assert.Equal(144, NotchController.Tier(busy, watching: false, current: 144));
        Assert.Equal(30, NotchController.Tier(busy, watching: false, current: 30));
    }

    // Auto means MaxFps now, so a user who wants the old behaviour has to be able to ask for it.
    [Fact]
    public void A_user_can_pin_the_rate_back_down_to_the_old_limit()
        => Assert.Equal(120, NotchController.Capped(NotchController.CadenceFps(true, 60), 120));

    [Fact]
    public void A_ceiling_above_the_tier_changes_nothing()
        => Assert.Equal(60, NotchController.Capped(60, 120));

    // The dark flash people saw while the pill grew: the collapsed preview was gone by t=0.35 and the
    // expanded content did not start until t=0.45, so a tenth of the morph drew nothing at all. Swept
    // rather than spot-checked, because the hole was ten percent wide and a handful of samples walks
    // straight over it.
    [Fact]
    public void Something_is_drawn_at_every_point_of_the_morph()
    {
        for (int i = 0; i <= 1000; i++)
        {
            float t = i / 1000f;
            Assert.True(NotchController.MorphHasContent(t),
                $"nothing drawn at t={t:F3}: content={NotchController.ContentFade(t):F3} "
                + $"mini={NotchController.MiniFade(t):F3}");
        }
    }

    // The two have to overlap, not merely meet: touching at a point is one frame of near-nothing at any
    // frame rate slow enough to land on it.
    [Fact]
    public void The_preview_is_still_up_when_the_content_starts()
        => Assert.True(NotchController.ContentIn < NotchController.MiniOut,
            "content must begin before the preview has finished melting");

    [Fact]
    public void The_ends_are_still_clean()
    {
        Assert.Equal(0f, NotchController.ContentFade(0f));
        Assert.Equal(1f, NotchController.ContentFade(1f));
        Assert.Equal(1f, NotchController.MiniFade(0f));
        Assert.Equal(0f, NotchController.MiniFade(1f));
    }

    // "Set glass to Strong, then go to the desktop, and it freezes." Over an app the tints are 120 and 48
    // and 1.34x has room; on the DESKTOP they are 255 and 245, already opaque because there is no
    // wallpaper detail left to protect there, and 255 * 1.34 = 341. That reached Color.FromArgb, which
    // throws outside 0..255 - on the render path, every frame. These pin both ends of the range.
    [Theory]
    [InlineData(255, 1.34f, 255)]   // the desktop at Strong: already opaque, cannot go further
    [InlineData(245, 1.34f, 255)]
    [InlineData(120, 1.34f, 160)]   // over an app there is headroom and Strong still means something
    [InlineData(48, 1.34f, 64)]
    [InlineData(255, 0.66f, 168)]   // Light works in both places, which is the direction it is for
    [InlineData(120, 1f, 120)]
    public void A_scaled_tint_stays_inside_an_alpha_channel(int baseAlpha, float scale, int expected)
        => Assert.Equal(expected, NotchController.TintFor(baseAlpha, scale));

    // whatever the strength, the result has to be something Color.FromArgb accepts
    [Fact]
    public void No_strength_can_produce_an_alpha_that_throws()
    {
        foreach (int a in new[] { 0, 48, 120, 245, 255 })
            foreach (float s in new[] { 0.66f, 1f, 1.34f, 4f })
            {
                int v = NotchController.TintFor(a, s);
                Assert.InRange(v, 0, 255);
                System.Drawing.Color.FromArgb(v, 8, 8, 8);   // the call that was throwing
            }
    }
}
