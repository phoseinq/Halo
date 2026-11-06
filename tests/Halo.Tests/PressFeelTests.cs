using Halo.Shell;

namespace Halo.Tests;

// The pill yields a couple of percent while held and settles back when let go.
public class PressFeelTests
{
    [Fact]
    public void At_rest_it_is_full_size_and_fully_pressed_it_is_barely_smaller()
    {
        Assert.Equal(1f, PressFeel.Scale(0f));
        Assert.Equal(1f - PressFeel.Depth, PressFeel.Scale(1f), 5);
        Assert.True(PressFeel.Depth <= 0.03f);   // "very little" is a contract, not a tuning
    }

    [Fact]
    public void Presses_in_faster_than_it_lets_go()
    {
        float inFrames = 0, p = 0f;
        while (p < 1f) { p = PressFeel.Step(p, held: true, 0.008f); inFrames++; }
        float outFrames = 0;
        while (p > 0f) { p = PressFeel.Step(p, held: false, 0.008f); outFrames++; }
        Assert.True(inFrames < outFrames);
        Assert.True(inFrames * 0.008f <= 0.1f);   // the press lands within a tenth of a second
    }

    [Fact]
    public void Never_overshoots_on_the_way_back()
    {
        float p = 1f;
        for (int i = 0; i < 200; i++)
        {
            p = PressFeel.Step(p, held: false, 0.008f);
            Assert.True(PressFeel.Scale(p) <= 1f);
            Assert.True(p >= 0f);
        }
        Assert.Equal(0f, p);
    }
}
