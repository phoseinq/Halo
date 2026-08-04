using Halo.Notifications;
using Microsoft.Win32;
using Xunit;

namespace Halo.Tests;

// Touches the real HKCU, so it runs in the banner-registry collection, which owns the redirect for the
// whole run. It must never be pointed at the live notification settings: managing the redirect per class
// let a parallel class clear it mid-test and put "app.three" into the real settings of this machine.
[Collection("banner-registry")]
public class BannerApplyTests
{
    private const string Scratch = BannerRootFixture.Scratch;

    [Fact]
    public void Applies_a_value_and_reads_it_back()
    {
        int n = BannerApply.Apply([new BannerEdit("some.app", "ShowBanner", 0)]);

        Assert.Equal(1, n);
        Assert.Equal(0, BannerApply.Read("some.app", "ShowBanner"));
    }

    [Fact]
    public void A_null_value_deletes()
    {
        BannerApply.Apply([new BannerEdit("some.app", "Sound", 0)]);
        Assert.Equal(0, BannerApply.Read("some.app", "Sound"));

        BannerApply.Apply([new BannerEdit("some.app", "Sound", null)]);
        Assert.Null(BannerApply.Read("some.app", "Sound"));
    }

    [Fact]
    public void Reading_a_key_that_was_never_written_is_null_rather_than_a_throw()
        => Assert.Null(BannerApply.Read("never.seen", "ShowBanner"));

    // one bad edit must not cost the rest of the batch
    [Fact]
    public void A_batch_survives_an_entry_it_cannot_apply()
    {
        int n = BannerApply.Apply([
            new BannerEdit("first.app", "ShowBanner", 0),
            new BannerEdit("third.app", "", 0),
            new BannerEdit("third.app", "ShowBanner", 0),
        ]);

        Assert.Equal(2, n);
        Assert.Equal(0, BannerApply.Read("first.app", "ShowBanner"));
        Assert.Equal(0, BannerApply.Read("third.app", "ShowBanner"));
    }

    // The global sound switch lives on the settings key itself, not under an app. "." was tried for this
    // and creates a literal subkey named "." - the write succeeds and lands where Windows never looks.
    [Fact]
    public void An_empty_subkey_addresses_the_root_itself()
    {
        BannerApply.Apply([new BannerEdit("", "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", 0)]);

        Assert.Equal(0, BannerApply.Read("", "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"));
        using var root = Registry.CurrentUser.OpenSubKey(Scratch);
        Assert.NotNull(root!.GetValue("NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND"));
        Assert.DoesNotContain(".", root.GetSubKeyNames());
    }
}
