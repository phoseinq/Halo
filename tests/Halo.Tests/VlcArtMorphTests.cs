using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The album art bug (MediaArtMorphTests) a second time, on the VLC panel, which never got the shared rect:
// DrawCollapsed sized its tile off the pill's height alone, so while the pill grew the cone raced past the
// panel's 132 all the way to 206 and sat at x=9, while DrawContent drew a second cone at the fixed panel
// rect - reported as the icon jumping out big and snapping back on the way open.
public class VlcArtMorphTests
{
    private const float Pill = 40f, Panel = 220f;

    // Not a copy of MediaWidget's constants: the point is that VLC's two resting rects ARE its two ends, so
    // sharing the rect changes nothing at either end and everything in between.
    [Fact]
    public void The_shared_rect_lands_on_both_of_vlcs_resting_tiles()
    {
        var pill = MediaWidget.ArtRect(Pill);
        Assert.Equal(9f, pill.X, 3);
        Assert.Equal(7f, pill.Y, 3);
        Assert.Equal(40f - 14f, pill.Width, 3);   // the old `sz = h - 14f`

        var panel = MediaWidget.ArtRect(Panel);
        Assert.Equal(26f, panel.X, 3);
        Assert.Equal(26f, panel.Y, 3);
        Assert.Equal(132f, panel.Width, 3);       // the old `const artSize`
    }

    [Fact]
    public void The_cone_keeps_the_room_it_has_at_both_ends()
    {
        Assert.Equal(2f, VlcWidget.ConeInset(Pill), 3);            // the old `x + 2, sz - 4`
        Assert.Equal(132f * 0.14f, VlcWidget.ConeInset(Panel), 3); // the old `artSize * 0.14f`
    }

    // The old bug as a property: at no height may the cone be drawn bigger than the one it is growing
    // towards, which is where the preview copy used to end up.
    [Fact]
    public void The_cone_never_grows_past_the_size_it_is_growing_towards()
    {
        float panelCone = 132f - 132f * 0.14f * 2f;
        for (int i = 0; i <= 400; i++)
        {
            float h = Pill + i;
            float cone = MediaWidget.ArtRect(h).Width - VlcWidget.ConeInset(h) * 2f;
            Assert.InRange(cone, 26f - 4f, panelCone);
        }
    }

    [Fact]
    public void It_only_ever_grows_on_the_way_out()
    {
        float last = 0f;
        for (int i = 0; i <= 180; i++)
        {
            float h = Pill + i;
            float cone = MediaWidget.ArtRect(h).Width - VlcWidget.ConeInset(h) * 2f;
            Assert.True(cone >= last, $"the cone shrank at h={h}");
            last = cone;
        }
    }

    // A step of a pixel or two per frame is a scale; the jump that was reported was 70+.
    [Fact]
    public void No_frame_of_the_morph_moves_it_more_than_a_couple_of_pixels()
    {
        for (int i = 0; i < 180; i++)
        {
            var a = MediaWidget.ArtRect(Pill + i);
            var b = MediaWidget.ArtRect(Pill + i + 1);
            Assert.InRange(b.Width - a.Width, 0f, 2f);
            Assert.InRange(b.X - a.X, 0f, 2f);
        }
    }
}
