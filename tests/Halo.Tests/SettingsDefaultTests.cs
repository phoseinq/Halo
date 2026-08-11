extern alias settingsasm;
using Xunit;

namespace Halo.Tests;

// The settings panel and the pill are separate processes reading the same settings.json, and each carries its
// own idea of what a missing key means. When those two disagree the panel reports the opposite of what is
// running, which is worse than either default being wrong: the panel is what you open to find out why the
// native banner is quiet.
//
// This is not hypothetical. `70be733` put the app's default for notifications.silence back to on after a
// regression, and the panel's row went on declaring off - so on any machine whose settings.json had never
// carried the key, the panel said the feature was off while the pill was silencing every app it mirrored.
public class SettingsDefaultTests
{
    private static string PanelFallback(string key)
    {
        foreach (var page in settingsasm::Halo.Settings.Catalog.Pages)
            foreach (var section in page.Sections)
                foreach (var row in section.Rows)
                    if (row.Key == key) return row.Fallback;
        return "";
    }

    // Both places the pill reads it - NotchController's startup latch and its live re-read - pass `true`.
    [Fact]
    public void The_panel_agrees_with_the_pill_about_silencing_native_banners()
        => Assert.Equal("on", PanelFallback("notifications.silence"));

    // The feature switch above it, for the same reason: a mirrored toast the pill is drawing while the panel
    // claims the feature is off is the same class of lie.
    [Fact]
    public void The_panel_agrees_with_the_pill_about_mirroring_notifications_at_all()
        => Assert.Equal("on", PanelFallback("feature.notifications"));
}
