using System;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The idle face has no UI harness and cannot be screenshotted while it runs, so every timing claim about it
// has to be assertable here or it is not checked at all.
public class FaceDirectorTests
{
    [Fact]
    public void At_rest_the_eyes_are_open()
        => Assert.Equal(1f, FaceDirector.Open(0.5f), 3);

    [Fact]
    public void The_first_blink_lands_where_the_schedule_says()
    {
        float first = FaceDirector.Gap(0);
        Assert.Equal(1f, FaceDirector.Open(first - 0.05f), 3);
        Assert.True(FaceDirector.Open(first + FaceDirector.BlinkSeconds / 2f) < 0.2f,
            "the middle of a blink should be all but shut");
        Assert.Equal(1f, FaceDirector.Open(first + FaceDirector.BlinkSeconds + 0.05f), 3);
    }

    // An eye with no height is an eye that is not there, and two frames of that reads as a dropped frame
    // rather than a blink.
    [Fact]
    public void A_blink_never_closes_all_the_way()
    {
        for (float t = 0f; t < 60f; t += 0.01f) Assert.True(FaceDirector.Open(t) >= 0.05f);
    }

    // A lid that jumps at the bottom of the blink is the thing the easing is there to stop, so the shape
    // has to be symmetric about the middle.
    [Fact]
    public void A_blink_closes_and_opens_at_the_same_rate()
    {
        float mid = FaceDirector.Gap(0) + FaceDirector.BlinkSeconds / 2f;
        for (float d = 0.01f; d < FaceDirector.BlinkSeconds / 2f; d += 0.01f)
            Assert.Equal(FaceDirector.Open(mid - d), FaceDirector.Open(mid + d), 3);
    }

    // The gaps have to look unplanned. A fixed interval is a metronome, which is the one thing a face
    // cannot do, so the spread is asserted rather than left to the constants happening to differ.
    [Fact]
    public void No_two_consecutive_blinks_are_the_same_distance_apart()
    {
        for (int i = 0; i < FaceDirector.BlinkCycle - 1; i++)
            Assert.True(MathF.Abs(FaceDirector.Gap(i) - FaceDirector.Gap(i + 1)) > 0.3f,
                $"gaps {i} and {i + 1} are too close to each other");
    }

    [Fact]
    public void Every_gap_is_inside_its_bounds()
    {
        for (int i = 0; i < FaceDirector.BlinkCycle; i++)
        {
            Assert.True(FaceDirector.Gap(i) >= FaceDirector.BlinkGapMin);
            Assert.True(FaceDirector.Gap(i) <= FaceDirector.BlinkGapMax);
        }
    }

    // The pill can sit on an idle desktop for hours, so the schedule has to keep working there rather than
    // walking off the end of its own table.
    // Compared to a tolerance rather than to two decimals, and the reason is worth keeping: at forty cycles
    // out the clock is a float near 2400, where consecutive representable values are 1.2e-4 apart, so the
    // two times being compared are not the same time and cannot be. The middle of a blink turns that into
    // ~0.001 of eye-open. That is the precision the pill's own accumulated clock has out there too.
    [Fact]
    public void The_schedule_repeats_rather_than_running_out()
    {
        float cycle = FaceDirector.CycleSeconds();
        for (float t = 0f; t < cycle; t += 0.05f)
            Assert.True(MathF.Abs(FaceDirector.Open(t) - FaceDirector.Open(t + cycle * 40f)) < 0.01f,
                $"the schedule drifted at {t}s");
    }

    // Hours in, and still blinking - the loop above only proves the shape repeats, not that anything
    // happens inside it.
    [Fact]
    public void It_is_still_blinking_after_three_hours()
    {
        float start = 3 * 60 * 60;
        bool shut = false;
        for (float t = start; t < start + FaceDirector.CycleSeconds(); t += 0.02f)
            if (FaceDirector.Open(t) < 0.3f) { shut = true; break; }
        Assert.True(shut);
    }

    [Fact]
    public void The_gaze_drifts_but_stays_inside_its_travel()
    {
        for (float t = 0f; t < 300f; t += 0.05f)
        {
            var (x, y) = FaceDirector.Gaze(t);
            Assert.InRange(x, -1f, 1f);
            Assert.InRange(y, -1f, 1f);
        }
    }

    // Two sines on periods that do not divide into each other, so the path never closes. If it did, the
    // face would visibly be running a loop.
    [Fact]
    public void The_gaze_does_not_return_to_where_it_started()
    {
        var (x0, y0) = FaceDirector.Gaze(0f);
        for (float t = 1f; t < 200f; t += 0.25f)
        {
            var (x, y) = FaceDirector.Gaze(t);
            Assert.False(MathF.Abs(x - x0) < 0.001f && MathF.Abs(y - y0) < 0.001f,
                $"the gaze closed its path at {t}s");
        }
    }

    // The ring is the face's legibility at pill size, so it breathes rather than pulses: dimming it far
    // enough to notice would take the face with it.
    [Fact]
    public void The_glow_breathes_without_going_dark()
    {
        float low = float.MaxValue, high = float.MinValue;
        for (float t = 0f; t < 60f; t += 0.01f)
        {
            float g = FaceDirector.Glow(t);
            low = MathF.Min(low, g);
            high = MathF.Max(high, g);
        }
        Assert.True(low > 0.9f, $"the ring dimmed to {low}");
        Assert.True(high < 1.12f);
        Assert.True(high - low > 0.1f, "a ring that does not move is not breathing");
    }

    [Fact]
    public void The_fade_runs_from_nothing_to_everything_and_is_flat_at_both_ends()
    {
        Assert.Equal(0f, FaceDirector.Alpha(0f), 3);
        Assert.Equal(1f, FaceDirector.Alpha(1f), 3);
        Assert.Equal(0f, FaceDirector.Alpha(-3f), 3);
        Assert.Equal(1f, FaceDirector.Alpha(9f), 3);
        Assert.True(FaceDirector.Alpha(0.02f) < 0.02f, "it should leave gently");
        Assert.True(FaceDirector.Alpha(0.98f) > 0.98f, "and arrive gently");
    }

    [Fact]
    public void The_fade_only_ever_goes_up()
    {
        for (float t = 0f; t < 1f; t += 0.01f)
            Assert.True(FaceDirector.Alpha(t + 0.01f) >= FaceDirector.Alpha(t));
    }

    [Fact]
    public void At_bundles_the_three_into_one_look()
    {
        var look = FaceDirector.At(12.5f);
        Assert.Equal(FaceDirector.Open(12.5f), look.Open, 4);
        Assert.Equal(FaceDirector.Glow(12.5f), look.Glow, 4);
        var (x, y) = FaceDirector.Gaze(12.5f);
        Assert.Equal(x, look.GazeX, 4);
        Assert.Equal(y, look.GazeY, 4);
    }
}

// The handover: the face notices a widget coming, puts its costume on, squints and lets go. It is over in
// 0.62s and only happens when a widget happens to wake, so nothing about it can be caught by eye.
public class FaceHandoverTests
{
    private static (Face.Look Look, float Prop, float Alpha, float Bob) At(float t, bool prop = true)
        => FaceDirector.Hand(t, prop, 0.4f);

    // The cost of the beat lands on someone waiting for their music to appear, so a widget with nothing to
    // put on must not pay it.
    [Fact]
    public void A_widget_with_no_costume_gets_out_of_the_way_almost_at_once()
    {
        Assert.Equal(FaceDirector.BareEnd, FaceDirector.HandSeconds(false), 3);
        Assert.True(FaceDirector.HandSeconds(false) < FaceDirector.HandSeconds(true) / 3f);
    }

    [Fact]
    public void The_costume_is_not_on_before_the_face_has_noticed()
        => Assert.Equal(0f, At(FaceDirector.NoticeEnd * 0.5f).Prop, 3);

    [Fact]
    public void The_costume_is_fully_on_by_the_time_the_beat_starts()
        => Assert.Equal(1f, At(FaceDirector.CostumeEnd).Prop, 2);

    // It overshoots and settles rather than sliding to a stop - that is the "arriving under its own weight"
    // the ease was chosen for - so the assertion is that it lands, not that it never turns back.
    [Fact]
    public void The_costume_overshoots_and_settles()
    {
        float peak = 0f;
        for (float t = 0f; t <= FaceDirector.CostumeEnd; t += 0.005f) peak = MathF.Max(peak, At(t).Prop);
        Assert.True(peak > 1.02f, $"nothing overshot - peaked at {peak}");
        Assert.True(peak < 1.15f, $"overshot too far - {peak}");
        Assert.Equal(1f, At(FaceDirector.CostumeEnd).Prop, 2);
        for (float t = 0f; t <= FaceDirector.BeatEnd; t += 0.005f)
            Assert.True(At(t).Prop >= -0.0001f, $"the costume went negative at {t}s");
    }

    // The head dips as the costume lands and is still again by the end - a bounce that outlives the beat
    // would hand a moving face to the widget morph.
    [Fact]
    public void The_head_dips_once_as_the_costume_lands_and_settles()
    {
        Assert.Equal(0f, At(FaceDirector.NoticeEnd).Bob, 3);
        float deepest = 0f;
        for (float t = FaceDirector.CostumeEnd; t <= FaceDirector.BeatEnd; t += 0.005f)
            deepest = MathF.Max(deepest, MathF.Abs(At(t).Bob));
        Assert.InRange(deepest, 0.01f, 0.08f);
        Assert.True(MathF.Abs(At(FaceDirector.BeatEnd).Bob) < 0.006f, "it was still moving at the end");
    }

    // A widget with no costume has nothing to land, so nothing may move.
    [Fact]
    public void A_bare_handover_never_bobs()
    {
        for (float t = 0f; t <= FaceDirector.BareEnd; t += 0.005f)
            Assert.Equal(0f, At(t, prop: false).Bob, 4);
    }

    // The face has to be gone by the end, or the widget morphs in underneath something still drawn.
    [Fact]
    public void The_face_is_fully_gone_when_the_beat_ends()
    {
        Assert.Equal(0f, At(FaceDirector.BeatEnd).Alpha, 3);
        Assert.Equal(0f, At(FaceDirector.BareEnd, prop: false).Alpha, 3);
    }

    [Fact]
    public void The_face_is_at_full_strength_while_the_costume_goes_on()
        => Assert.Equal(1f, At(FaceDirector.NoticeEnd).Alpha, 2);

    // The point of the hold: the costume has to be seen ON the face, finished, before the face leaves. The
    // first version started dissolving 0.02s after it landed and it was never once seen.
    [Fact]
    public void The_costume_is_seen_finished_before_anything_starts_to_fade()
    {
        Assert.Equal(1f, At(FaceDirector.CostumeEnd).Prop, 2);
        Assert.Equal(1f, At(FaceDirector.CostumeEnd).Alpha, 2);
        Assert.Equal(1f, At(FaceDirector.HoldEnd).Alpha, 2);
        Assert.True(FaceDirector.HoldEnd - FaceDirector.CostumeEnd > 0.15f,
            "the hold is too short to read as a pose");
    }

    // The squint has to be settled while the face fades, not still moving into one. To a tolerance rather
    // than to decimals: the landing widen is exponentially damped and still has about 1% left at HoldEnd,
    // which is a fifth of a pixel on the eye and not what this is asking about.
    [Fact]
    public void The_squint_has_landed_by_the_time_the_fade_starts()
    {
        float held = At(FaceDirector.HoldEnd).Look.Open, ended = At(FaceDirector.BeatEnd).Look.Open;
        Assert.True(MathF.Abs(held - ended) < 0.02f, $"still moving: {held} -> {ended}");
    }

    [Fact]
    public void The_fade_only_ever_goes_down()
    {
        float last = 2f;
        for (float t = 0f; t <= FaceDirector.BeatEnd; t += 0.01f)
        {
            float a = At(t).Alpha;
            Assert.True(a <= last + 0.0001f, $"the face brightened again at {t}s");
            last = a;
        }
    }

    // The eyes swing out and come back: out at the notice, home again by the time the costume has landed.
    [Fact]
    public void The_eyes_search_rather_than_swinging_once()
    {
        // This replaced "flick to one side and return to centre". The old shape was defensible - the pill
        // grows both ways from the middle, so there is no direction to point at - and it was the same mark
        // every time, which is most of why the beat read as having no motion in it.
        Assert.True(MathF.Abs(At(0f).Look.GazeX) < 0.05f, "the search starts from centre");

        // BOTH sides get looked at. One side is a glance; two is looking for something.
        float lowest = 1f, highest = -1f;
        for (float t = 0f; t <= FaceDirector.CostumeEnd; t += 0.02f)
        {
            float x = At(t).Look.GazeX;
            lowest = MathF.Min(lowest, x);
            highest = MathF.Max(highest, x);
        }
        Assert.True(lowest < -0.5f, $"never looked left (min {lowest:0.00})");
        Assert.True(highest > 0.5f, $"never looked right (max {highest:0.00})");

        // ...and it ENDS looking at the arrival rather than back at centre, because every costume that
        // comes in from the side comes from the right. Landing back at centre was the old shape and it
        // meant the search was about nothing.
        Assert.True(At(FaceDirector.CostumeEnd).Look.GazeX > 0.4f, "the search does not end on the costume");
    }

    [Fact]
    public void The_search_stays_inside_the_eyes_own_travel()
    {
        // GazeX is -1..1 OF THE EYE'S OWN TRAVEL, so a waypoint past 1 does not look further, it just
        // clamps somewhere in the drawing and costs the path its shape
        foreach (var (_, x, y) in FaceDirector.Glances)
        {
            Assert.InRange(x, -1f, 1f);
            Assert.InRange(y, -1f, 1f);
        }
    }

    [Fact]
    public void The_search_holds_still_between_glances()
    {
        // Eyes move in saccades. A path that glides evenly between its points reads as following something
        // rather than looking for it, so at least one pair of waypoints has to sit almost on top of each
        // other - that pause is the difference.
        bool held = false;
        for (int i = 1; i < FaceDirector.Glances.Length; i++)
        {
            var a = FaceDirector.Glances[i - 1];
            var b = FaceDirector.Glances[i];
            if (b.T - a.T > 0.05f && MathF.Abs(b.X - a.X) < 0.15f) held = true;
        }
        Assert.True(held, "the search has no hold in it - it is a sweep, not a set of glances");
    }

    [Fact]
    public void The_beat_is_a_squint_rather_than_a_blink()
    {
        float open = At(FaceDirector.BeatEnd).Look.Open;
        Assert.InRange(open, 0.25f, 0.6f);
    }

    // Nothing may be asked for outside the beat: the controller clamps, and so does this.
    [Fact]
    public void Before_and_after_are_the_ends_rather_than_extrapolations()
    {
        Assert.Equal(At(0f).Prop, At(-5f).Prop, 4);
        Assert.Equal(At(FaceDirector.BeatEnd).Prop, At(50f).Prop, 4);
        Assert.Equal(At(FaceDirector.BeatEnd).Alpha, At(50f).Alpha, 4);
    }

    // The idle clock carries on underneath - the face does not stop being alive because it is handing over.
    [Fact]
    public void The_blink_schedule_keeps_running_through_the_handover()
    {
        float blink = FaceDirector.Gap(0);
        float mid = blink + FaceDirector.BlinkSeconds / 2f;
        Assert.True(FaceDirector.Hand(0.1f, true, mid).Look.Open
                    < FaceDirector.Hand(0.1f, true, 0.4f).Look.Open);
    }
}

// The two agents are near-mirrored modules everywhere else in this repo, and a change to one almost always
// needs the twin. The costumes are now built that way on purpose - one design in two accents - and that is
// the kind of intent which survives as a comment while the code quietly drifts apart. So it is asserted.
public class AgentCostumeTests
{
    private static FaceDirector.Beat Claude(float t) => FaceDirector.Hand(t, FaceProp.Spark, 0.4f, 0f);
    private static FaceDirector.Beat Codex(float t) => FaceDirector.Hand(t, FaceProp.Brackets, 0.4f, 0f);

    [Fact]
    public void Both_agents_run_the_same_beat()
        => Assert.Equal(FaceDirector.HandSeconds(FaceProp.Spark),
                        FaceDirector.HandSeconds(FaceProp.Brackets), 4);

    [Fact]
    public void They_read_identically_and_only_part_at_the_finish()
    {
        // Through the reading stage they must be the same face doing the same thing - the eyes are the
        // costume there, and two agents whose eyes read differently would be two ideas rather than one in
        // two accents.
        for (float t = FaceDirector.AgentWake + 0.05f; t < FaceDirector.AgentWork; t += 0.05f)
        {
            Assert.Equal(Claude(t).Look.GazeX, Codex(t).Look.GazeX, 4);
            Assert.Equal(Claude(t).Look.GazeY, Codex(t).Look.GazeY, 4);
        }
        // ...and then they must NOT be. Claude's ring flares and Codex's head is squeezed; if either of
        // those stopped happening the two would silently collapse into one costume.
        //
        // Taken as the extreme over the finish rather than at one instant. Both accents are damped
        // oscillations, so a single sample can land on a zero crossing and report that the thing does not
        // happen - which is what the first version of this test did, at 0.12s, a twelfth of a second after
        // the squeeze had swung back through its own centre.
        float flare = 0f, squeeze = 0f;
        for (float t = FaceDirector.AgentWork; t <= FaceDirector.AgentBeat; t += 0.01f)
        {
            flare = MathF.Max(flare, Claude(t).Look.Glow - Codex(t).Look.Glow);
            squeeze = MathF.Min(squeeze, Codex(t).Squash);
        }
        Assert.True(flare > 0.5f, $"Claude's finish only lifts the ring by {flare:0.00} - it no longer flares");
        Assert.True(squeeze < -0.03f, $"Codex's finish only reaches {squeeze:0.00} - it no longer squeezes");
    }

    // The cursor blink may modulate the ring, never extinguish it. The face at pill size is a dark shape on
    // a dark pill without its halo - that is what --render-face's "no glow" column showed - so a literal
    // on/off cursor would make the head disappear twice a second.
    [Fact]
    public void The_cursor_blink_never_puts_the_ring_out()
    {
        for (float t = 0f; t <= FaceDirector.AgentBeat; t += 0.01f)
            foreach (var beat in new[] { Claude(t), Codex(t) })
                Assert.True(beat.Look.Glow >= 0.85f,
                            $"the ring falls to {beat.Look.Glow:0.00} at t={t:0.00}s");
    }

    // The light has to actually GO somewhere, and keep going. A phase that stalls is a ring with a bright
    // patch stuck on it, which is what "not interesting" looked like the first two times this costume was
    // attempted - so the thing that makes it work at all is asserted rather than eyeballed.
    [Fact]
    public void The_light_travels_all_the_way_round_and_keeps_going()
    {
        float first = -1f, last = -1f;
        for (float t = 0f; t <= FaceDirector.AgentWork; t += 0.01f)
        {
            float phase = Claude(t).Spin;
            if (phase < 0f) continue;
            if (first < 0f) first = phase;
            Assert.True(phase >= last - 0.0001f, $"the light went backwards at t={t:0.00}s");
            last = phase;
        }
        Assert.True(last - first > 1.5f,
                    $"the light only gets {last - first:0.00} of a lap round the head - it has to be seen "
                    + "going all the way round, or it is a bright patch rather than something travelling");
    }

    // The correction the whole third attempt exists for. The second version shut the eyes and filled the
    // head with lines, and for a second and a half the face was not a face - which is the one thing the
    // eating costume, the only one anybody has liked, gets right. The eyes stay open here, and that is now
    // a rule rather than a decision somebody remembers.
    [Fact]
    public void The_face_stays_a_face_while_it_works()
    {
        for (float t = FaceDirector.AgentIn; t <= FaceDirector.AgentWork; t += 0.01f)
            foreach (var beat in new[] { Claude(t), Codex(t) })
                Assert.True(beat.Look.Open > 0.35f,
                            $"the eyes are only {beat.Look.Open:0.00} open at t={t:0.00}s - the face has "
                            + "stopped being a face again");
    }

    [Fact]
    public void The_light_runs_only_while_it_is_working()
    {
        Assert.Null(Claude(0.05f).Chase);
        Assert.NotNull(Claude((FaceDirector.AgentIn + FaceDirector.AgentWork) / 2f).Chase);
        Assert.Null(Claude(FaceDirector.AgentBeat).Chase);
    }

    // One light is something thinking; two moving together is a machine running. That is the entire accent
    // between the twins besides the colour and the last half second, so if it collapses they are one costume.
    [Fact]
    public void Claude_thinks_with_one_light_and_Codex_runs_two()
    {
        float mid = (FaceDirector.AgentIn + FaceDirector.AgentWork) / 2f;
        Assert.Equal(1, Claude(mid).Chase!.Value.Count);
        Assert.Equal(2, Codex(mid).Chase!.Value.Count);
        Assert.NotEqual(Claude(mid).Chase!.Value.Ink, Codex(mid).Chase!.Value.Ink);
    }
}
