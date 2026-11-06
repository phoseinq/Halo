using Halo.Widgets;

namespace Halo.Tests;

// The written lines after hello are chains of cubics; their joints are made G1 so round letters stop faceting.
public class ScriptSmoothTests
{
    private static float Angle(float[] s, int j)
    {
        float ax = s[j] - s[j - 2], ay = s[j + 1] - s[j - 1];
        float bx = s[j + 2] - s[j], by = s[j + 3] - s[j + 1];
        return MathF.Acos(Math.Clamp((ax * bx + ay * by) / (MathF.Sqrt(ax * ax + ay * ay) * MathF.Sqrt(bx * bx + by * by)), -1f, 1f))
               * 180f / MathF.PI;
    }

    // two cubics meeting at (10,0): coming in heading 45 degrees down-right, leaving heading ~18 degrees up -
    // a ~63-degree kink, soft enough (under CornerDeg) to be a faceting fault rather than an intended corner
    private static readonly float[] Kinked = [0, 0, 3, -3, 7, -3, 10, 0, 13, -1, 17, 3, 20, 6];

    [Fact]
    public void A_soft_kink_becomes_a_straight_tangent()
    {
        Assert.True(Angle(Kinked, 6) > 20f);
        var sm = Script.SmoothJoints(Kinked);
        Assert.True(Angle(sm, 6) < 0.5f);
    }

    [Fact]
    public void Joint_and_ends_stay_where_they_were_drawn()
    {
        var sm = Script.SmoothJoints(Kinked);
        Assert.Equal((10f, 0f), (sm[6], sm[7]));     // the joint itself
        Assert.Equal((0f, 0f), (sm[0], sm[1]));      // stroke start
        Assert.Equal((20f, 6f), (sm[12], sm[13]));   // stroke end
    }

    [Fact]
    public void A_real_reversal_is_left_as_a_corner()
    {
        float[] cusp = [0, 0, 3, -10, 7, -10, 10, 0, 7, -10, 3, -10, 0, 0];   // straight back the way it came
        Assert.Equal(cusp, Script.SmoothJoints(cusp));
    }

    [Fact]
    public void A_closed_stroke_is_smooth_across_its_seam()
    {
        // two cubics around a loop, starting and ending at (10,0), meeting there at an angle
        float[] loop = [10, 0, 10, -8, -10, -8, -10, 0, -10, 8, 6, 8, 10, 0];
        float ax = loop[12] - loop[10], ay = loop[13] - loop[11], bx = loop[2] - loop[0], by = loop[3] - loop[1];
        Assert.True(MathF.Abs(ax * by - ay * bx) > 1f);                    // a notch before
        var sm = Script.SmoothJoints(loop);
        ax = sm[12] - sm[10]; ay = sm[13] - sm[11]; bx = sm[2] - sm[0]; by = sm[3] - sm[1];
        Assert.True(MathF.Abs(ax * by - ay * bx) < 0.01f && ax * bx + ay * by > 0f);   // one tangent after
    }

    [Fact]
    public void A_single_cubic_has_no_joint_to_touch()
    {
        float[] one = [0, 0, 1, 2, 3, 4, 5, 6];
        Assert.Equal(one, Script.SmoothJoints(one));
    }

    // the joins between letters: one cubic from where a letter lifts to where the next goes down, and into an
    // ascender it climbs on the RIGHT, never back across the letter it leaves
    [Fact]
    public void A_join_runs_from_exit_to_entry_and_loops_right_into_an_ascender()
    {
        var j = Halo.Widgets.Script.Join(new System.Drawing.PointF(27, -6), new System.Drawing.PointF(41, -78));
        Assert.Equal(8, j.Length);
        Assert.Equal((27f, -6f), (j[0], j[1]));
        Assert.Equal((41f, -78f), (j[6], j[7]));
        Assert.True(j[2] > 27f && j[4] > 41f);
    }
}
