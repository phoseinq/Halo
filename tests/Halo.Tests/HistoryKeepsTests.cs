using Halo.Notifications;

namespace Halo.Tests;

// The history keeps messages, not Halo's own status glances.
public class HistoryKeepsTests
{
    [Theory]
    [InlineData("")]                 // a real app toast, or a screenshot
    [InlineData("api")]              // another tool, through the API
    [InlineData("reminder-42")]      // a reminder the user set
    public void Messages_are_kept(string kind)
        => Assert.True(NotifSource.Keeps(new NotifItem { Kind = kind }));

    [Theory]
    [InlineData("language")]
    [InlineData("hourly")]
    [InlineData("weather")]
    [InlineData("battery")]
    [InlineData("net")]
    [InlineData("ctx-7")]
    [InlineData("hooks")]
    [InlineData("settings-missing")]
    [InlineData("reminder-help")]
    [InlineData("ask-1234")]
    public void Status_glances_are_not(string kind)
        => Assert.False(NotifSource.Keeps(new NotifItem { Kind = kind }));
}
