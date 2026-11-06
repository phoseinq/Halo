using Halo.Shell;

namespace Halo.Tests;

// The switch-off when Halo quits: content leaves, the pill pulls in to a circle, the circle shrinks to a dot,
// the dot goes out.
public class GoodbyeTests
{
    [Fact]
    public void Starts_as_the_pill_it_was()
    {
        var s = Goodbye.At(264, 40, 0f);
        Assert.Equal((264, 40, 20, 1f, (byte)255), s);
    }

    [Fact]
    public void Content_leaves_before_the_shape_shrinks()
    {
        Assert.True(Goodbye.At(264, 40, 0.36f).Content < 0.01f);
        Assert.Equal(40, Goodbye.At(264, 40, 0.36f).H);   // still full height while the content goes
    }

    [Fact]
    public void Pulls_in_to_a_circle_of_its_own_height()
    {
        var s = Goodbye.At(264, 40, 0.40f);
        Assert.Equal(40, s.W);
        Assert.Equal(40, s.H);
        Assert.Equal(20, s.R);
    }

    [Fact]
    public void Ends_as_an_invisible_dot()
    {
        var s = Goodbye.At(264, 40, 1f);
        Assert.Equal(Goodbye.DotPx, s.W);
        Assert.Equal(Goodbye.DotPx, s.H);
        Assert.Equal(0, s.Alpha);
    }

    [Fact]
    public void Never_grows_and_never_goes_square()
    {
        int lastW = int.MaxValue, lastH = int.MaxValue;
        for (float t = 0f; t <= 1f; t += 0.01f)
        {
            var s = Goodbye.At(264, 40, t);
            Assert.True(s.W <= lastW && s.H <= lastH, $"grew at t={t}");
            Assert.Equal(System.Math.Min(s.W, s.H) / 2, s.R);
            lastW = s.W; lastH = s.H;
        }
    }

    [Fact]
    public void Opacity_only_falls_in_the_last_stretch()
    {
        Assert.Equal((byte)255, Goodbye.At(264, 40, 0.6f).Alpha);
        Assert.True(Goodbye.At(264, 40, 0.85f).Alpha is > 0 and < 255);
    }

    [Fact]
    public void The_face_falls_asleep_first()
    {
        Assert.Equal(0f, Goodbye.EyesShut(0f));
        Assert.Equal(1f, Goodbye.EyesShut(0.3f));
    }
}
