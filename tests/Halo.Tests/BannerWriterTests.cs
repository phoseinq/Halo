using Halo.Notifications;
using Xunit;

namespace Halo.Tests;

// Unpackaged, Commit writes directly and verifies. The packaged route cannot be exercised from a test run
// - it needs a signed package installed - so what is pinned here is that verification is REAL: Commit
// reports what it could prove landed, not what it attempted. Reporting attempts is how this feature spent
// a night succeeding loudly and doing nothing.
[Collection("banner-registry")]
public class BannerWriterTests
{

    [Fact]
    public void Commit_reports_what_it_verified()
    {
        int ok = BannerWriter.Commit([
            new BannerEdit("app.one", "ShowBanner", 0),
            new BannerEdit("app.two", "ShowBanner", 0),
        ]);

        Assert.Equal(2, ok);
        Assert.Equal(0, BannerApply.Read("app.one", "ShowBanner"));
    }

    [Fact]
    public void A_deletion_verifies_as_absent()
    {
        BannerWriter.Commit([new BannerEdit("app.three", "Sound", 0)]);
        Assert.Equal(1, BannerWriter.Commit([new BannerEdit("app.three", "Sound", null)]));
        Assert.Null(BannerApply.Read("app.three", "Sound"));
    }

    [Fact]
    public void An_empty_batch_is_not_a_process_launch()
        => Assert.Equal(0, BannerWriter.Commit([]));

    [Fact]
    public void Verified_is_false_for_a_value_that_was_never_written()
        => Assert.False(BannerWriter.Verified(new BannerEdit("app.absent", "ShowBanner", 0)));

    // the count is of what LANDED, so an entry the applier skipped must not be counted
    [Fact]
    public void An_edit_that_could_not_be_applied_is_not_counted_as_verified()
    {
        int ok = BannerWriter.Commit([
            new BannerEdit("app.four", "ShowBanner", 0),
            new BannerEdit("app.four", "", 0),
        ]);

        Assert.Equal(1, ok);
    }
}
