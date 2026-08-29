extern alias settingsasm;

using System;
using System.Linq;
using Halo.Launcher;
using Halo.Settings;
using Xunit;

namespace Halo.Tests;

public sealed class LauncherSettingsTests
{
    [Fact]
    public void DefaultsMatchWhatThePillReads()
    {
        var file = SettingsFile.Empty;

        Assert.True(file.Bool(SettingsKeys.LauncherEnabled, true));
        Assert.Equal("Alt+Space", file.Text(SettingsKeys.LauncherHotkey, HotKeyChord.Default.Format()));
    }

    [Fact]
    public void AStoredChord_IsWhatGetsRegistered()
    {
        var file = SettingsFile.Empty.With(SettingsKeys.LauncherHotkey, "Ctrl+Alt+Space");

        Assert.True(HotKeyChord.TryParse(
            file.Text(SettingsKeys.LauncherHotkey, HotKeyChord.Default.Format()), out var chord));
        Assert.Equal("Ctrl+Alt+Space", chord.Format());
    }

    [Fact]
    public void AGarbageChord_FallsBackToTheDefaultRatherThanNoHotkey()
    {
        // a hand-edited settings.json must not silently cost the whole feature
        var file = SettingsFile.Empty.With(SettingsKeys.LauncherHotkey, "Alt+Nonsense");
        string stored = file.Text(SettingsKeys.LauncherHotkey, HotKeyChord.Default.Format());

        var chord = HotKeyChord.TryParse(stored, out var parsed) ? parsed : HotKeyChord.Default;

        Assert.Equal(HotKeyChord.Default, chord);
    }

    [Fact]
    public void ThePanelsRowDefault_IsTheSameStringTheChordFormatsTo()
    {
        // the panel and the pill duplicate this shape rather than sharing it, so a divergence here is a
        // field showing "Alt+Space" over a pill that registered something else. Same trap
        // SettingsContractTests already pins for the other keys.
        var row = settingsasm::Halo.Settings.Catalog.Pages
            .SelectMany(p => p.Sections).SelectMany(s => s.Rows)
            .Single(r => r.Key == SettingsKeys.LauncherHotkey);

        Assert.Equal(HotKeyChord.Default.Format(), row.Fallback);
    }

    [Fact]
    public void BothRowsExistInThePanelCatalog()
    {
        var keys = settingsasm::Halo.Settings.Catalog.Pages
            .SelectMany(p => p.Sections).SelectMany(s => s.Rows).Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains(SettingsKeys.LauncherEnabled, keys);
        Assert.Contains(SettingsKeys.LauncherHotkey, keys);
    }
}
