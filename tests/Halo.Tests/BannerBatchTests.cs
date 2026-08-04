using Halo.Notifications;
using Xunit;

namespace Halo.Tests;

// The format crosses a process boundary, so anything it cannot express is a silent data loss rather than
// a compile error. AUMIDs are the awkward input: they are arbitrary strings from other vendors and really
// do contain spaces, dots and braces.
public class BannerBatchTests
{
    [Fact]
    public void A_set_and_a_delete_round_trip()
    {
        var edits = new[]
        {
            new BannerEdit(@"Microsoft.WindowsStore_8wekyb3d8bbwe!App", "ShowBanner", 0),
            new BannerEdit(@"Microsoft.WindowsStore_8wekyb3d8bbwe!App", "Sound", null),
        };

        var back = BannerBatch.Parse(BannerBatch.Serialize(edits).Split('\n'));

        Assert.Equal(2, back.Count);
        Assert.Equal(edits[0], back[0]);
        Assert.Equal(edits[1], back[1]);
    }

    // a subkey with a space in it is not exotic - "Chrome Beta" style AUMIDs are common
    [Fact]
    public void A_subkey_containing_spaces_survives()
    {
        var one = new BannerEdit(@"{6D809377-6AF0-444B-8957-A3773F02200E}\My App\notifier", "ShowBanner", 0);
        var back = BannerBatch.Parse(BannerBatch.Serialize([one]).Split('\n'));
        Assert.Equal(one, Assert.Single(back));
    }

    [Fact]
    public void Blank_and_malformed_lines_are_skipped_rather_than_throwing()
    {
        var back = BannerBatch.Parse(["", "   ", "onlyonefield", "a\tb\tnotanumber"]);
        Assert.Empty(back);
    }

    // The settings root is addressed by an empty subkey, so that field being blank is data, not damage.
    // "." was tried for the root first and creates a literal key named "." - the write succeeds and lands
    // where Windows never looks.
    [Fact]
    public void An_empty_subkey_means_the_root_and_survives()
    {
        var root = new BannerEdit("", "NOC_GLOBAL_SETTING_ALLOW_NOTIFICATION_SOUND", 0);
        Assert.Equal(root, Assert.Single(BannerBatch.Parse(BannerBatch.Serialize([root]).Split('\n'))));
    }

    [Fact]
    public void An_empty_batch_serializes_to_nothing()
        => Assert.Equal("", BannerBatch.Serialize([]));
}
