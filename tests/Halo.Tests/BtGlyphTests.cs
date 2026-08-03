using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The device icon comes from the Class of Device the device advertises, not from its name. These pin the
// decode of that field, which is the whole of it - the glyph constants are referenced rather than written
// as literals so the test cannot drift from what the widget actually draws.
public class BtGlyphTests
{
    [Theory]
    [InlineData(2, 3, BtWidget.GlyphPhone)]        // phone / smartphone
    [InlineData(2, 1, BtWidget.GlyphPhone)]        // phone / cellular, minor is not consulted
    [InlineData(1, 3, BtWidget.GlyphComputer)]     // computer / laptop
    [InlineData(4, 1, BtWidget.GlyphHeadphone)]    // A/V / wearable headset
    [InlineData(4, 2, BtWidget.GlyphHeadphone)]    // A/V / hands-free
    [InlineData(4, 6, BtWidget.GlyphHeadphone)]    // A/V / headphones
    [InlineData(4, 5, BtWidget.GlyphSpeaker)]      // A/V / loudspeaker
    [InlineData(4, 8, BtWidget.GlyphSpeaker)]      // A/V / car audio
    [InlineData(4, 18, BtWidget.GlyphController)]  // A/V / gaming
    [InlineData(7, 1, BtWidget.GlyphWatch)]        // wearable / wristwatch
    public void Class_of_device_picks_the_glyph(int major, int minor, int expected)
        => Assert.Equal(expected, BtWidget.GlyphForCod(major, minor));

    // Peripheral is the awkward one: the type is a nibble, and keyboard/pointing are FLAG BITS above it,
    // so a combo device sets both and a plain gamepad sets neither.
    [Theory]
    [InlineData(0x10, BtWidget.GlyphKeyboard)]
    [InlineData(0x20, BtWidget.GlyphMouse)]
    [InlineData(0x30, BtWidget.GlyphKeyboard)]   // combo: the keyboard half wins
    [InlineData(0x02, BtWidget.GlyphController)] // gamepad
    [InlineData(0x01, BtWidget.GlyphController)] // joystick
    public void Peripheral_reads_its_flag_bits(int minor, int expected)
        => Assert.Equal(expected, BtWidget.GlyphForCod(5, minor));

    // A peripheral that is none of those must not be forced into an approximate icon - a wrong icon reads
    // as a wrong device, which is worse than admitting it is just some Bluetooth thing.
    [Theory]
    [InlineData(5, 0x06)]   // card reader
    [InlineData(7, 3)]      // wearable / jacket
    [InlineData(9, 1)]      // health
    [InlineData(0, 0)]      // miscellaneous: the device declined to say
    [InlineData(31, 0)]     // uncategorised
    public void Anything_unmapped_stays_the_bluetooth_mark(int major, int minor)
        => Assert.Equal(BtWidget.GlyphBluetooth, BtWidget.GlyphForCod(major, minor));
}
