using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

public class DownloadBadgeTests
{
    // frame-errors.txt caught this on the live pill: "Value of '-0.744' is not valid for 'emSize'",
    // thrown from DrawCountBadge on the per-frame render path. Work it back - -0.744 / 0.62 = -1.2, and
    // -1.2 / 0.60 = -2.0 - and the icon size was -2, which is h - 14 at a pill height of 12. The
    // collapsed pill passes through heights like that on every morph.
    [Theory]
    [InlineData(40f, 26f)]
    [InlineData(15f, 1f)]
    [InlineData(14f, 0f)]
    [InlineData(12f, 0f)]   // the height that produced the -0.744
    [InlineData(0f, 0f)]
    public void IconSize_never_goes_negative(float h, float expected)
        => Assert.Equal(expected, DownloadWidget.IconSize(h));
}
