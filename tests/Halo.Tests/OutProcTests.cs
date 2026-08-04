using Halo.Interop;
using Xunit;

namespace Halo.Tests;

// Only the refresh decision is unit-tested. Whether a child process escapes package identity cannot be
// asserted from a test run - it needs a signed package installed - and --probe-banner is what answers it.
public class OutProcTests
{
    [Fact]
    public void A_missing_copy_needs_refreshing()
        => Assert.True(OutProc.NeedsRefresh("3.6.0.0", null));

    [Fact]
    public void A_stale_copy_needs_refreshing()
        => Assert.True(OutProc.NeedsRefresh("3.6.0.0", "3.5.0.0"));

    // copying ~250 files on every batch would cost more than the writes in it
    [Fact]
    public void A_matching_copy_is_left_alone()
        => Assert.False(OutProc.NeedsRefresh("3.6.0.0", "3.6.0.0"));

    // the stamp is a file, so it arrives with whatever line ending was written to it
    [Fact]
    public void Version_comparison_ignores_case_and_surrounding_whitespace()
    {
        Assert.False(OutProc.NeedsRefresh("3.6.0.0", " 3.6.0.0 "));
        Assert.False(OutProc.NeedsRefresh("3.6.0.0", "3.6.0.0\r\n"));
    }

    // an unreadable stamp must mean "copy again", never "assume it is fine"
    [Fact]
    public void An_empty_stamp_needs_refreshing()
        => Assert.True(OutProc.NeedsRefresh("3.6.0.0", ""));
}
