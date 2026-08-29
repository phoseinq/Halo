using Halo.Launcher;

namespace Halo.Tests;

public sealed class HotKeyChordTests
{
    [Fact]
    public void Default_IsAltSpace()
    {
        Assert.Equal("Alt+Space", HotKeyChord.Default.Format());
    }

    [Theory]
    [InlineData("Alt+Space")]
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("Ctrl+Shift+P")]
    [InlineData("Win+K")]
    [InlineData("Alt+F4")]
    [InlineData("Ctrl+Alt+Shift+Win+Space")]
    public void ParseThenFormat_RoundTrips(string text)
    {
        Assert.True(HotKeyChord.TryParse(text, out var chord));
        Assert.Equal(text, chord.Format());
    }

    [Fact]
    public void ModifierOrderAndCase_AreNormalised()
    {
        // whatever the user typed into settings.json by hand, the panel should read one spelling back
        Assert.True(HotKeyChord.TryParse("  space + ALT  ", out var chord));
        Assert.Equal("Alt+Space", chord.Format());
    }

    [Fact]
    public void ControlSpelledEitherWay_Parses()
    {
        Assert.True(HotKeyChord.TryParse("Control+Space", out var a));
        Assert.True(HotKeyChord.TryParse("Ctrl+Space", out var b));
        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Alt")]              // modifier with no key
    [InlineData("Alt+")]
    [InlineData("Alt+Nonsense")]
    [InlineData("Space")]            // a bare key is not a global hotkey
    [InlineData("Alt+Ctrl")]         // two modifiers, still no key
    public void Junk_DoesNotParse(string? text)
    {
        Assert.False(HotKeyChord.TryParse(text, out _));
    }

    [Fact]
    public void AltSpace_CarriesTheWin32Bits()
    {
        Assert.True(HotKeyChord.TryParse("Alt+Space", out var chord));
        Assert.Equal(0x0001u, chord.Mods);   // MOD_ALT
        Assert.Equal(0x20u, chord.Vk);       // VK_SPACE
    }
}
