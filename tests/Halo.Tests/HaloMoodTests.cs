using System;
using System.Drawing;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The ring's colour through the day.
//
// The ask was "thirty or more, changing through the day and with conditions, so it does not get boring".
// Thirty ARBITRARY pairs would be worse than the one it had: a pile of unrelated gradients reads as the
// app not knowing what it looks like. So this is a journey rather than a list, and these are the rules that
// keep every point on it recognisably the same face in a different light.
public class HaloMoodTests
{
    private static float Hue(Color c) => c.GetHue();

    private static float Apart(float a, float b)
    {
        float d = MathF.Abs(a - b) % 360f;
        return d > 180f ? 360f - d : d;
    }

    [Fact]
    public void TheShortWayRoundTheWheel()
    {
        // 338 to 26 is 48 degrees through red, not 312 backwards through the whole spectrum. Without this,
        // dawn sweeps through green and blue on its way from rose to amber - visibly wrong, and only ever
        // visible on a sheet of the whole day rather than at any one moment.
        Assert.Equal(2f, HaloMood.LerpHue(338f, 26f, 0.5f), 1);
        Assert.Equal(180f, HaloMood.LerpHue(170f, 190f, 0.5f), 1);
        // and it stays inside the wheel rather than running off either end
        for (float t = 0f; t <= 1f; t += 0.05f)
            Assert.InRange(HaloMood.LerpHue(350f, 10f, t), 0f, 360f);
    }

    [Fact]
    public void MidnightIsNotASeam()
    {
        // The day is a LOOP: the last keyframe interpolates back to the first, so 23:59 and 00:01 are
        // neighbours. A list of moods would jump here, which is the tell that it is a list.
        var before = HaloMood.At(23.98f);
        var after = HaloMood.At(0.02f);
        Assert.True(Apart(Hue(before.Left), Hue(after.Left)) < 6f,
                    "the ring jumps at midnight - the loop is not closed");
    }

    [Fact]
    public void ConsecutiveHoursAreNeighbours()
    {
        // Variety has to arrive gradually or it is not variety, it is flicker. Nobody watching the pill
        // should ever catch it changing colour.
        for (float h = 0f; h < 24f; h += 1f)
        {
            var a = HaloMood.At(h);
            var b = HaloMood.At(h + 1f);
            float step = Apart(Hue(a.Left), Hue(b.Left));
            Assert.True(step < 70f, $"the ring moves {step:0} degrees between {h:00}:00 and the next hour");
        }
    }

    [Fact]
    public void ItIsAlwaysASweepAndNeverAFlatColour()
    {
        // Two hues, 20-70 degrees apart, is the face's signature. Wider stops reading as one light source;
        // narrower and the sweep disappears, at which point the ring may as well be a single colour.
        for (float h = 0f; h < 24f; h += 0.25f)
        {
            var (l, r) = HaloMood.At(h);
            float gap = Apart(Hue(l), Hue(r));
            Assert.InRange(gap, 20f, 70f);
        }
    }

    [Fact]
    public void ItNeverGoesGreyAndNeverGoesNeon()
    {
        // The ring is a GLOW. Fully saturated it becomes a neon outline round the head; washed out it
        // becomes the grey the net ramp's pastel stops were, which is the complaint that started all this.
        //
        // Measured in HSV, which is the model the Light table is written in. The first version of this test
        // used Color.GetSaturation, which is HSL saturation - a bright colour at 40% HSV saturation reads as
        // 100% there, so midday failed a band it was comfortably inside. Wrong instrument, not wrong design.
        for (float h = 0f; h < 24f; h += 0.25f)
        {
            var (l, r) = HaloMood.At(h);
            foreach (var c in new[] { l, r })
            {
                Fx.RgbToHsv(c, out _, out float sat, out float val);
                Assert.InRange(sat, 0.35f, 0.75f);
                Assert.InRange(val, 0.88f, 1.0f);
            }
        }
    }

    [Fact]
    public void TheWholeDayIsActuallyVaried()
    {
        // The number that was asked for. Counted as distinct 15-degree buckets of the left hue across the
        // day, which is a fair reading of "how many different colours would anyone notice".
        var seen = new System.Collections.Generic.HashSet<int>();
        for (float h = 0f; h < 24f; h += 0.25f) seen.Add((int)(Hue(HaloMood.At(h).Left) / 15f));
        Assert.True(seen.Count >= 12, $"only {seen.Count} distinguishable hues across a whole day");
    }

    // ---- the conditions half ------------------------------------------------------------------------
    //
    // A condition SHIFTS the hour's colour rather than replacing it. Replacing throws away the one thing
    // the ring already says: a face stuck on amber all evening stops being a clock and becomes an alarm
    // nobody can switch off.

    [Fact]
    public void AHealthyBatteryChangesNothing()
    {
        foreach (float pct in new[] { 1f, 0.8f, 0.4f, HaloMood.LowBattery })
            Assert.Equal(HaloMood.At(14f).Left, HaloMood.At(14f, pct, charging: false).Left);
    }

    [Fact]
    public void ChargingIsNeverAWarningNoMatterHowEmpty()
    {
        // At 8% on mains nothing is wrong - the machine is filling up. Colouring that is crying wolf, which
        // is how a signal stops being read at all.
        Assert.Equal(HaloMood.At(14f).Left, HaloMood.At(14f, 0.08f, charging: true).Left);
        Assert.Equal(HaloMood.At(14f).Left, HaloMood.At(14f, 0.02f, charging: true).Left);
    }

    [Fact]
    public void ADrainingBatteryPullsTheRingWarm()
    {
        // Measured as distance to the warning colour, not as "the red channel rises". The first version
        // asserted the latter at 14:00, where the hour's own colour is already yellow with red at 255 -
        // there was nothing left for the warning to add, so a working shift failed a test that was asking
        // the wrong question.
        float ToWarn(Color c) =>
            MathF.Sqrt(MathF.Pow(c.R - 255, 2) + MathF.Pow(c.G - 156, 2) + MathF.Pow(c.B - 48, 2));

        // 02:00, where the hour's colour is deep blue, so the pull has somewhere to travel
        float ok = ToWarn(HaloMood.At(2f, 0.30f, charging: false).Left);
        float low = ToWarn(HaloMood.At(2f, 0.15f, charging: false).Left);
        float dire = ToWarn(HaloMood.At(2f, 0.03f, charging: false).Left);

        Assert.True(low < ok, "15% is no closer to the warning than 30%");
        Assert.True(dire < low, "3% is no closer to the warning than 15%");

        var atThree = HaloMood.At(2f, 0.03f, charging: false).Left;
        Assert.True(atThree.R > atThree.B, "at 3% the ring is still not warm");
    }

    [Fact]
    public void TheWarningIsStillASweepAndStillTheHour()
    {
        // it must not collapse to one flat alarm colour, and it must not fully erase the time either
        for (float h = 0f; h < 24f; h += 3f)
        {
            var (l, r) = HaloMood.At(h, 0.05f, charging: false);
            Assert.True(Apart(Hue(l), Hue(r)) > 4f, $"the warning went flat at {h:00}:00");
        }
        // two different hours under the same dire battery are still different colours
        Assert.NotEqual(HaloMood.At(3f, 0.05f, false).Left, HaloMood.At(15f, 0.05f, false).Left);
    }

    [Fact]
    public void NoBatteryAtAllSaysNothing()
    {
        // a desktop reports no battery; -1 is that, and it must not read as "empty"
        Assert.Equal(HaloMood.At(9f).Left, HaloMood.At(9f, -1f, charging: false).Left);
    }

    [Fact]
    public void AnyClockValueIsSafe()
    {
        // negative and past-24 both wrap: the caller passes DateTime.Now's TimeOfDay and nothing should
        // ever have to think about it
        Assert.Equal(HaloMood.At(2f).Left, HaloMood.At(26f).Left);
        Assert.Equal(HaloMood.At(23f).Left, HaloMood.At(-1f).Left);
    }
}

// The conditions half of the ring. The clock half is tested above; this is the part the ask called
// "and conditions", and every one of them is a SHIFT of the hour's colour rather than a replacement -
// which is the design rule, and exactly the kind of rule that erodes into "well, red is clearer".
public class HaloMoodConditionTests
{
    private static (Color L, Color R) At(float hour, HaloMood.Conditions c) => HaloMood.At(hour, c);

    private static float Sat(Color c)
    {
        int max = Math.Max(c.R, Math.Max(c.G, c.B)), min = Math.Min(c.R, Math.Min(c.G, c.B));
        return max == 0 ? 0f : (max - min) / (float)max;
    }

    // Offline drains the colour and keeps the hour. A fixed grey would be the same dead ring at 04:00 and
    // at noon, and the hour is still true when the network is not.
    [Fact]
    public void Offline_drains_the_colour_without_replacing_the_hour()
    {
        foreach (float hour in new[] { 3f, 9f, 14f, 21f })
        {
            var live = At(hour, new HaloMood.Conditions());
            var dead = At(hour, new HaloMood.Conditions(Offline: true));
            Assert.True(Sat(dead.L) < Sat(live.L) * 0.55f,
                        $"offline barely changed the ring at {hour:00}:00");
            Assert.NotEqual(dead, At(hour + 8f, new HaloMood.Conditions(Offline: true)));
        }
    }

    // Both privacy states keep a TWO-HUE sweep, because that sweep is the face's signature - a flat ring is
    // not Halo in a different light, it is a different object.
    [Fact]
    public void Privacy_keeps_the_two_hue_sweep()
    {
        foreach (var c in new[] { new HaloMood.Conditions(Mic: true), new HaloMood.Conditions(Cam: true) })
        {
            var (l, r) = At(11f, c);
            Assert.NotEqual(l, r);
        }
    }

    // Green for the camera, orange for the mic - the convention Privacy.cs already names and the one
    // everybody has already learned from their phone. Asserted by channel rather than by exact value, so a
    // tuned shade does not break it but a swapped meaning does.
    [Fact]
    public void The_camera_is_green_and_the_mic_is_orange()
    {
        var cam = At(11f, new HaloMood.Conditions(Cam: true)).L;
        Assert.True(cam.G > cam.R + 25 && cam.G > cam.B + 25, $"the camera tint is not green: {cam}");
        var mic = At(11f, new HaloMood.Conditions(Mic: true)).L;
        Assert.True(mic.R > mic.G + 25 && mic.G > mic.B + 25, $"the mic tint is not orange: {mic}");
    }

    // A call is both at once, and of the two the camera is the one people want to be certain about.
    [Fact]
    public void The_camera_outranks_the_mic_when_both_are_live()
        => Assert.Equal(At(11f, new HaloMood.Conditions(Cam: true)),
                        At(11f, new HaloMood.Conditions(Mic: true, Cam: true)));

    // Charging at 8% is not a warning. The machine is filling up, and colouring that is crying wolf - which
    // is how a signal stops being read at all.
    [Fact]
    public void A_charging_machine_is_never_warned_about()
        => Assert.Equal(At(11f, new HaloMood.Conditions()),
                        At(11f, new HaloMood.Conditions(0.05f, Charging: true)));

    // Nothing true = the clock, untouched. The condition path must be able to do nothing, or every ring
    // everywhere has quietly been tinted by a default.
    [Fact]
    public void With_nothing_wrong_it_is_exactly_the_hour()
    {
        for (float hour = 0f; hour < 24f; hour += 0.5f)
            Assert.Equal(HaloMood.At(hour), At(hour, new HaloMood.Conditions()));
    }

    // The other half of "thirty or more moods": what the machine is DOING, not what is wrong with it. These
    // are the light in the room rather than something asking to be looked at, so they are deliberately much
    // weaker than the warnings - and the rule that keeps them safe is that a warning must always survive one.
    [Fact]
    public void An_activity_never_drowns_out_a_warning()
    {
        var flat = new HaloMood.Conditions(0.04f);
        foreach (var doing in new[] { HaloMood.Doing.Video, HaloMood.Doing.Downloading })
        {
            var busy = At(11f, flat with { Activity = doing });
            Assert.True(busy.L.R > busy.L.G + 40,
                        $"a flat battery stopped reading as a warning while {doing} was on: {busy.L}");
        }
        // ...and privacy, which is the loudest thing here and must be unmissable through anything
        var filming = At(11f, new HaloMood.Conditions(Cam: true, Activity: HaloMood.Doing.Video)).L;
        Assert.True(filming.G > filming.R + 20, $"the camera stopped being green during a film: {filming}");
    }

    // Music takes the album's OWN colour, which is the one mood in the set that differs for every record -
    // and takes NOTHING when there is no artwork, because a colour invented for "music in general" would be
    // a made-up fact about what is playing.
    [Fact]
    public void Music_wears_the_album_and_nothing_when_there_is_no_art()
    {
        var warm = At(11f, new HaloMood.Conditions(
            Activity: HaloMood.Doing.Music, Accent: Color.FromArgb(255, 232, 118, 62)));
        var cool = At(11f, new HaloMood.Conditions(
            Activity: HaloMood.Doing.Music, Accent: Color.FromArgb(255, 58, 190, 132)));
        Assert.NotEqual(warm, cool);
        Assert.True(warm.L.R > cool.L.R + 30, "the warm record did not warm the ring");

        Assert.Equal(HaloMood.At(11f),
                     At(11f, new HaloMood.Conditions(Activity: HaloMood.Doing.Music)));
    }

    // A film is watched in the dark, and this is the only mood that takes brightness AWAY - which is also
    // the thing that tells it apart from every other tint at a glance.
    [Fact]
    public void A_film_dims_the_ring()
    {
        var lit = At(14f, new HaloMood.Conditions()).L;
        var dark = At(14f, new HaloMood.Conditions(Activity: HaloMood.Doing.Video)).L;
        Assert.True(dark.R + dark.G + dark.B < (lit.R + lit.G + lit.B) * 0.85f,
                    $"the cinema mood is no darker than the hour: {dark} against {lit}");
    }

    // Offline is applied AFTER the activity on purpose: a film playing off a local disk with the network
    // down should still go grey, and draining first would let the cinema tint paint the colour back in.
    [Fact]
    public void Offline_still_drains_a_ring_that_is_busy()
    {
        var busy = At(14f, new HaloMood.Conditions(Activity: HaloMood.Doing.Music,
                                                   Accent: Color.FromArgb(255, 232, 118, 62)));
        var gone = At(14f, new HaloMood.Conditions(Offline: true, Activity: HaloMood.Doing.Music,
                                                   Accent: Color.FromArgb(255, 232, 118, 62)));
        Assert.True(Sat(gone.L) < Sat(busy.L) * 0.55f, "a busy machine stopped showing that it is offline");
    }
}
