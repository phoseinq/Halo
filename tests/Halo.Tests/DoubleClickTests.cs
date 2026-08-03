using Halo.Shell;

namespace Halo.Tests;

// The pill sits on top of everything at the top of the screen, so the cost of getting this wrong is not
// a missed gesture - it is yanking the user out of whatever they were doing, which is the one thing here
// that cannot be undone. Hence: the SECOND press commits, and only if it was soon enough and near enough.
public class DoubleClickTests
{
    private const int Window = 500;

    [Fact]
    public void One_press_does_nothing()
    {
        var gesture = new DoubleClick();
        Assert.False(gesture.Step(true, 1000, 100, 20, Window));
    }

    [Fact]
    public void Two_presses_in_the_same_spot_fire_on_the_second()
    {
        var gesture = new DoubleClick();
        Assert.False(gesture.Step(true, 1000, 100, 20, Window));
        Assert.False(gesture.Step(false, 1050, 100, 20, Window));
        Assert.True(gesture.Step(true, 1200, 100, 20, Window));
    }

    // Held down across frames is one press. Without the edge detection the poll would read "down" every
    // 8ms and the first click would fire the gesture by itself.
    [Fact]
    public void Holding_the_button_is_not_a_second_press()
    {
        var gesture = new DoubleClick();
        Assert.False(gesture.Step(true, 1000, 100, 20, Window));
        for (long t = 1008; t < 1400; t += 8)
            Assert.False(gesture.Step(true, t, 100, 20, Window));
    }

    [Fact]
    public void Too_slow_is_two_separate_clicks()
    {
        var gesture = new DoubleClick();
        Assert.False(gesture.Step(true, 1000, 100, 20, Window));
        Assert.False(gesture.Step(false, 1050, 100, 20, Window));
        Assert.False(gesture.Step(true, 1000 + Window + 1, 100, 20, Window));
    }

    // A mouse is not a clamp: a pixel or two of drift between two real clicks is normal.
    [Fact]
    public void A_little_drift_still_counts()
    {
        var gesture = new DoubleClick();
        Assert.False(gesture.Step(true, 1000, 100, 20, Window));
        Assert.False(gesture.Step(false, 1050, 100, 20, Window));
        Assert.True(gesture.Step(true, 1200, 100 + DoubleClick.SlopPx, 20, Window));
    }

    [Fact]
    public void Two_clicks_in_different_places_are_two_clicks()
    {
        var gesture = new DoubleClick();
        Assert.False(gesture.Step(true, 1000, 100, 20, Window));
        Assert.False(gesture.Step(false, 1050, 100, 20, Window));
        Assert.False(gesture.Step(true, 1200, 100 + DoubleClick.SlopPx + 1, 20, Window));
    }

    // Consuming the pair is what stops a nervous hand turning into repeated focus changes: after firing,
    // the next press starts a fresh pair rather than pairing with the one that already fired.
    [Fact]
    public void A_third_press_starts_over_instead_of_firing_again()
    {
        var gesture = new DoubleClick();
        gesture.Step(true, 1000, 100, 20, Window);
        gesture.Step(false, 1050, 100, 20, Window);
        Assert.True(gesture.Step(true, 1200, 100, 20, Window));
        Assert.False(gesture.Step(false, 1250, 100, 20, Window));
        Assert.False(gesture.Step(true, 1300, 100, 20, Window));
    }

    // Moving off the pill reads as a release, so the pair cannot be completed somewhere else.
    [Fact]
    public void Leaving_the_pill_between_presses_breaks_the_pair()
    {
        var gesture = new DoubleClick();
        Assert.False(gesture.Step(true, 1000, 100, 20, Window));
        Assert.False(gesture.Step(false, 1050, 900, 600, Window));
        Assert.False(gesture.Step(true, 1100, 900, 600, Window));
    }
}
