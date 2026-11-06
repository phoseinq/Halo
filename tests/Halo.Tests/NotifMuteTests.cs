using Halo.Notifications;

namespace Halo.Tests;

// Muting from the history sheet: everything, or one app. A muted toast opens no banner but is still kept.
public class NotifMuteTests
{
    private static readonly HashSet<string> None = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Nothing_muted_lets_everything_through()
        => Assert.False(NotifMute.Silences(false, None, "Telegram.TelegramDesktop"));

    [Fact]
    public void Muting_all_holds_every_banner()
        => Assert.True(NotifMute.Silences(true, None, "anything"));

    [Fact]
    public void Muting_one_app_holds_only_that_app()
    {
        var apps = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Telegram.TelegramDesktop" };
        Assert.True(NotifMute.Silences(false, apps, "telegram.telegramdesktop"));   // case-insensitive
        Assert.False(NotifMute.Silences(false, apps, "Microsoft.Outlook"));
        Assert.False(NotifMute.Silences(false, apps, ""));                          // no key, nothing to match
    }

    [Fact]
    public void An_app_is_its_aumid_and_its_name_only_without_one()
    {
        Assert.Equal("Telegram.TelegramDesktop", NotifMute.Key(new NotifItem { Aumid = "Telegram.TelegramDesktop", App = "Telegram" }));
        Assert.Equal("Battery", NotifMute.Key(new NotifItem { App = "Battery" }));
    }

    [Fact]
    public void The_file_round_trips()
    {
        var (all, apps) = NotifMute.Deserialize(NotifMute.Serialize(true, ["b", "a"]));
        Assert.True(all);
        Assert.Equal(["a", "b"], apps);
        var (none, empty) = NotifMute.Deserialize("{}");
        Assert.False(none);
        Assert.Empty(empty);
    }
}
