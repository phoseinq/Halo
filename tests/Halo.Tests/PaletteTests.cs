using System.Drawing;
using Halo.Widgets;

namespace Halo.Tests;

// The cover's colours for the music bar: one per distinct hue that holds a real share of the cover.
public class PaletteTests
{
    private static Bitmap Split(params Color[] bands)
    {
        var bmp = new Bitmap(60, 60);
        using var g = Graphics.FromImage(bmp);
        for (int i = 0; i < bands.Length; i++)
            using (var b = new SolidBrush(bands[i])) g.FillRectangle(b, i * 60 / bands.Length, 0, 60 / bands.Length + 1, 60);
        return bmp;
    }

    [Fact]
    public void A_two_colour_cover_gives_two_colours()
    {
        using var art = Split(Color.FromArgb(220, 40, 40), Color.FromArgb(40, 80, 230));
        Assert.Equal(2, Fx.Palette(art).Length);
    }

    [Fact]
    public void A_single_hue_cover_gives_one_and_grey_gives_none()
    {
        using var red = Split(Color.FromArgb(220, 40, 40), Color.FromArgb(160, 30, 30));
        Assert.Single(Fx.Palette(red));
        using var grey = Split(Color.FromArgb(120, 120, 120), Color.FromArgb(30, 30, 30));
        Assert.Empty(Fx.Palette(grey));
    }

    [Fact]
    public void Never_more_than_asked()
    {
        using var art = Split(Color.Red, Color.Yellow, Color.Lime, Color.Blue, Color.Magenta);
        Assert.Equal(3, Fx.Palette(art).Length);
    }

    // a one-colour cover still gets a run for the bar and the glow: its hue, turned a little each way
    [Fact]
    public void A_single_hue_cover_is_widened_to_three_and_grey_stays_empty()
    {
        using var red = Split(Color.FromArgb(220, 40, 40), Color.FromArgb(20, 20, 20));
        var sp = Fx.Spectrum(red);
        Assert.Equal(3, sp.Length);
        Assert.Equal(Fx.Palette(red)[0], sp[1]);
        using var grey = Split(Color.FromArgb(120, 120, 120), Color.FromArgb(30, 30, 30));
        Assert.Empty(Fx.Spectrum(grey));
    }
}
