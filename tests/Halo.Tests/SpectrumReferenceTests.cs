using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The equalizer's normalisation, which has now been wrong twice in the same direction and both times the
// bug survived because the reasoning lived in a comment nobody re-derived. First the divisor was this
// frame's loudest bar, which pinned that bar to 1.0 forty times a second - a fixed picture breathing.
// Then the comment on the fix claimed "a steady loud band settles below the ceiling", which is also not
// true: the band that IS the peak is at the ceiling on every frame, because the ratio is clamped at 1.
// These pin what the mechanism really does, so the next reading of it does not have to be taken on faith.
public class SpectrumReferenceTests
{
    // The production formula, not a copy of it. A private duplicate here would keep agreeing with itself
    // after the real one moved, which is a test that has stopped testing anything.
    private static float Ceiling(float reference) => AudioSpectrum.Ceiling(reference);

    private static float Settle(float reference, float peak, int frames)
    {
        for (int i = 0; i < frames; i++) reference = AudioSpectrum.TrackReference(reference, peak);
        return reference;
    }

    // The correction. A signal that never varies puts its loudest band at the top and keeps it there -
    // and that is right, because a steady tone should look steady. What must not happen is the top being
    // the SAME height regardless of level, which is what the old divisor did.
    [Fact]
    public void A_band_that_never_varies_sits_at_the_ceiling_and_the_ceiling_tracks_its_level()
    {
        float quiet = Settle(0f, 0.2f, 40);
        float loud = Settle(0f, 0.8f, 40);

        Assert.Equal(Ceiling(quiet), AudioSpectrum.Normalize(0.2f, quiet), 3);
        Assert.Equal(Ceiling(loud), AudioSpectrum.Normalize(0.8f, loud), 3);
        // the point of the moving ceiling: the same "full" bar is visibly shorter when the room is quiet
        Assert.True(Ceiling(loud) - Ceiling(quiet) > 0.35f);
    }

    // The property the fix actually delivers, and the one worth having: for a while after something loud,
    // a band that is merely loud does NOT reach the top. Ten frames is a quarter second at the FFT rate.
    [Fact]
    public void After_a_transient_a_merely_loud_band_stays_far_below_the_ceiling()
    {
        float reference = Settle(1f, 0.5f, 10);
        float value = AudioSpectrum.Normalize(0.5f, reference);

        Assert.True(value < 0.45f, $"expected well below the top, got {value}");
        Assert.True(Ceiling(reference) - value > 0.5f, $"expected a wide gap, got {Ceiling(reference) - value}");
    }

    // Fast up, slow down. If these were symmetric the reference would be an average and the ceiling would
    // sag through every quiet moment, which is the "it takes a while to catch up" complaint in another form.
    [Fact]
    public void The_reference_leaps_at_a_transient_and_leaks_back_down()
    {
        Assert.True(AudioSpectrum.TrackReference(0f, 1f) > 0.35f);        // one frame gets most of the way
        Assert.True(AudioSpectrum.TrackReference(1f, 0f) > 0.95f);        // one frame gives up almost nothing
        Assert.True(Settle(1f, 0f, 40) < 0.35f);                          // but a second of quiet does
    }

    // Nothing may be drawn taller than the top. A transient can be louder than the envelope chasing it -
    // reaching the ceiling is what that is supposed to look like, not overshooting it.
    [Fact]
    public void A_transient_louder_than_the_reference_reaches_the_ceiling_and_no_further()
        => Assert.Equal(Ceiling(0.5f), AudioSpectrum.Normalize(2f, 0.5f), 3);

    // Silence must stay silent. Dividing by a reference this small would blow the noise floor up to full
    // height in an empty room, which is the widget inventing a reading of something it cannot hear.
    // The floor comes from production, so this stays a boundary test wherever the floor moves to.
    [Fact]
    public void Below_the_floor_nothing_is_drawn()
    {
        Assert.Equal(0f, AudioSpectrum.Normalize(0.5f, 0f));
        Assert.Equal(0f, AudioSpectrum.Normalize(0.5f, AudioSpectrum.RefFloor));
        Assert.True(AudioSpectrum.Normalize(0.5f, AudioSpectrum.RefFloor * 1.5f) > 0f);
    }
}
