using System.Drawing;
using Halo.Notifications;

namespace Halo.Tests;

// The pull-down history survives restarts, sealed to the user: it is the only copy once a toast has been
// mirrored, because the Action Center original is removed so Windows does not show it twice.
public class HistoryStoreTests
{
    [Fact]
    public void Text_fields_and_the_icon_round_trip()
    {
        using var icon = new Bitmap(96, 96);
        using (var g = Graphics.FromImage(icon)) g.Clear(Color.FromArgb(255, 40, 150, 235));
        var t = new DateTime(2026, 11, 6, 20, 0, 0, DateTimeKind.Utc);
        var item = new NotifItem
        {
            Id = 42, Time = t.ToLocalTime(), App = "Telegram", Title = "\u0633\u0644\u0627\u0645",
            Body = "code 482913", Aumid = "Telegram.TelegramDesktop", LaunchPath = "tg://x", Kind = "k",
            Code = "482913", Stacked = 2, Icon = icon,
        };
        var back = HistoryStore.Deserialize(HistoryStore.Serialize([item]), t.AddMinutes(5));
        var b = Assert.Single(back);
        Assert.Equal((42u, "Telegram", "\u0633\u0644\u0627\u0645", "code 482913", "Telegram.TelegramDesktop"),
                     (b.Id, b.App, b.Title, b.Body, b.Aumid));
        Assert.Equal(("tg://x", "k", "482913", 2), (b.LaunchPath, b.Kind, b.Code, b.Stacked));
        Assert.Equal(t, b.Time.ToUniversalTime());
        Assert.NotNull(b.Icon);
        Assert.Equal(64, b.Icon!.Width);   // shrunk on the way to disk
    }

    [Fact]
    public void Entries_older_than_a_week_are_dropped_on_load()
    {
        var now = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
        var old = new NotifItem { Id = 1, Time = now.AddDays(-8).ToLocalTime(), Title = "old" };
        var fresh = new NotifItem { Id = 2, Time = now.AddDays(-1).ToLocalTime(), Title = "fresh" };
        var back = HistoryStore.Deserialize(HistoryStore.Serialize([old, fresh]), now);
        Assert.Equal("fresh", Assert.Single(back).Title);
    }

    [Fact]
    public void Sealed_bytes_are_not_the_text_and_open_again()
    {
        var plain = System.Text.Encoding.UTF8.GetBytes("Your verification code is 482913");
        var sealedBytes = HistoryStore.Protect(plain);
        Assert.NotNull(sealedBytes);
        Assert.DoesNotContain("482913", System.Text.Encoding.UTF8.GetString(sealedBytes!));
        Assert.Equal(plain, HistoryStore.Unprotect(sealedBytes!));
    }

    [Fact]
    public void Garbage_on_disk_is_an_empty_history_not_a_crash()
        => Assert.Null(HistoryStore.Unprotect([1, 2, 3, 4]));
}
