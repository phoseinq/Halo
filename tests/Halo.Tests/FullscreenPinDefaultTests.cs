extern alias settingsasm;

using System.Linq;
using Halo.Launcher;
using Halo.Settings;
using Xunit;

// Both assemblies declare a Halo.Settings namespace - SettingsKeys is the pill's, Catalog is the panel's -
// so the panel side has to come through its alias. That split is exactly why the two defaults could drift
// apart without anyone noticing: they are not in the same file, the same class, or the same DLL.
using Panel = settingsasm::Halo.Settings;

namespace Halo.Tests;

// Issue #1 asked for "an option to make the notch stay on top of full-screen applications". That option
// had shipped months earlier - and for anyone upgrading it did not work, because its default was decided
// in two places that disagreed.
//
// The settings panel fell back to the catalog row ("on"). The pill fell back to whether the legacy
// `pinned` file existed, which is 0 both for "turned it off on purpose" and for "never touched it", so it
// could never answer the question it was being asked. The toggle read ON while the pill hid over
// fullscreen anyway, which is indistinguishable from the feature not existing.
//
// These pin the two together. A default that lives in two files is not a default, it is a coincidence.
public class FullscreenPinDefaultTests
{
    private static Panel.Row RowFor(string key)
        => Panel.Catalog.Pages
            .SelectMany(p => p.Sections)
            .SelectMany(s => s.Rows)
            .Single(r => r.Key == key);

    [Fact]
    public void ThePanelAndThePillAgreeOnWhetherHaloStaysOverFullscreen()
    {
        var row = RowFor(SettingsKeys.OverFullscreen);
        bool panelDefault = row.Fallback == "on";
        Assert.Equal(SettingsKeys.OverFullscreenDefault, panelDefault);
    }

    // ...and it is ON. Not because on is nicer, but because that is what the panel has been telling
    // people for months, and the fix for a disagreement is to make the visible half true.
    [Fact]
    public void StayingOverFullscreenIsTheDefault()
        => Assert.True(SettingsKeys.OverFullscreenDefault);

    // The other half of the issue: a chord to hide and show it from anywhere. The row's default has to be
    // something RegisterHotKey will actually take, or the feature ships switched off for everyone who
    // never opens Settings - which is the same failure mode the issue was reported for.
    [Fact]
    public void TheHideShortcutsDefaultIsAChordThatParses()
    {
        var row = RowFor(SettingsKeys.HideHotkey);
        Assert.True(HotKeyChord.TryParse(row.Fallback, out var chord),
                    $"the default hide chord '{row.Fallback}' does not parse");
        Assert.NotEqual(0u, chord.Mods);   // a bare key would swallow that key everywhere
        Assert.NotEqual(HotKeyChord.Default, chord);   // and it must not be the launcher's own
    }

    // Empty is a legal value and means "no shortcut". The text box that can be cleared IS the off switch,
    // so nothing may treat a blank as a parse failure worth substituting a default for.
    [Fact]
    public void AnEmptyHideShortcutMeansNoneRatherThanTheDefault()
        => Assert.False(HotKeyChord.TryParse("", out _));
}
