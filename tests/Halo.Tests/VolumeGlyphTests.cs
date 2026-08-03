using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The speaker chip has to report the level, not just on/off. The thresholds are the whole content of it,
// so they are pinned here rather than left to be judged by eye on a running pill.
public class VolumeGlyphTests
{
    private const string Mute = "\uE74F";   // speaker with a cross
    private const string One = "\uE993";
    private const string Two = "\uE994";
    private const string Three = "\uE995";

    [Fact]
    public void Silence_shows_the_crossed_speaker_not_a_bare_one()
    {
        // E992 is Volume0, a speaker with no waves - it reads as "quiet", which is the one thing zero is
        // not. At zero the user has to see that nothing will come out.
        Assert.Equal(Mute, MediaWidget.VolumeGlyph(0f, muted: false));
        Assert.Equal(Mute, MediaWidget.VolumeGlyph(0.0005f, muted: false));
    }

    [Fact]
    public void Muted_shows_the_cross_at_any_level()
    {
        Assert.Equal(Mute, MediaWidget.VolumeGlyph(0.9f, muted: true));
        Assert.Equal(Mute, MediaWidget.VolumeGlyph(0.4f, muted: true));
    }

    [Theory]
    [InlineData(0.02f, One)]
    [InlineData(0.32f, One)]
    [InlineData(0.34f, Two)]
    [InlineData(0.65f, Two)]
    [InlineData(0.67f, Three)]
    [InlineData(1f, Three)]
    public void Waves_climb_with_the_level(float vol, string expected)
        => Assert.Equal(expected, MediaWidget.VolumeGlyph(vol, muted: false));

    [Fact]
    public void Thirds_are_the_boundaries()
    {
        Assert.Equal(One, MediaWidget.VolumeGlyph(1f / 3f - 0.001f, muted: false));
        Assert.Equal(Two, MediaWidget.VolumeGlyph(1f / 3f, muted: false));
        Assert.Equal(Two, MediaWidget.VolumeGlyph(2f / 3f - 0.001f, muted: false));
        Assert.Equal(Three, MediaWidget.VolumeGlyph(2f / 3f, muted: false));
    }

    // a level above 1 arrives from a drag clamped elsewhere; it must not fall off the end of the ramp
    [Fact]
    public void Over_full_still_reads_as_full()
        => Assert.Equal(Three, MediaWidget.VolumeGlyph(1.4f, muted: false));
}
