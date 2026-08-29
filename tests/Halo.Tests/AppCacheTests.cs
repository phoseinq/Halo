using Halo.Launcher;

namespace Halo.Tests;

public sealed class AppCacheTests
{
    [Fact]
    public void RoundTrip_KeepsNameAndAumid()
    {
        IReadOnlyList<AppEntry> apps =
        [
            new("Telegram Desktop", "TelegramDesktop.TelegramDesktop"),
            new("Windows Terminal", "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App"),
        ];

        var back = AppCache.FromJson(AppCache.ToJson(apps));

        Assert.Equal(apps, back);
    }

    [Fact]
    public void Dedupe_KeepsOnePerAumid()
    {
        // AppsFolder lists a packaged app once per install context, so the same AUMID arrives twice
        // with the same name. Two identical rows in the launcher looks like a bug in the matcher.
        IReadOnlyList<AppEntry> raw =
        [
            new("Notepad", "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"),
            new("Notepad", "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"),
            new("Paint", "Microsoft.Paint_8wekyb3d8bbwe!App"),
        ];

        var clean = AppCache.Dedupe(raw);

        Assert.Equal(2, clean.Count);
        Assert.Contains(clean, a => a.Name == "Paint");
    }

    [Fact]
    public void Dedupe_KeepsOnePerDisplayName()
    {
        // --probe-apps on a real machine found five names carried twice with different targets. Two rows
        // reading "Counter-Strike Global Offensive" in a six-row list is a row the user cannot choose
        // between.
        IReadOnlyList<AppEntry> raw =
        [
            new("Counter-Strike Global Offensive", @"D:\csgo-2\csgo.exe"),
            new("Counter-Strike Global Offensive", @"D:\csgo-3\Run_CSGO.exe"),
        ];

        Assert.Single(AppCache.Dedupe(raw));
    }

    [Fact]
    public void Dedupe_KeepsVariantsThatAreNamedDifferently()
    {
        // the flip side: Outlook ships two entries on purpose and the names say which is which
        IReadOnlyList<AppEntry> raw =
        [
            new("Outlook (classic)", "outlook.classic"),
            new("Outlook (new)", "outlook.new"),
        ];

        Assert.Equal(2, AppCache.Dedupe(raw).Count);
    }

    [Fact]
    public void Dedupe_DropsBlankNamesAndAumids()
    {
        IReadOnlyList<AppEntry> raw =
        [
            new("", "Something.Else"),
            new("Real App", ""),
            new("   ", "   "),
            new("Keeper", "Keeper.Id"),
        ];

        var clean = AppCache.Dedupe(raw);

        Assert.Single(clean);
        Assert.Equal("Keeper", clean[0].Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{\"apps\":\"a string, not an array\"}")]
    public void CorruptJson_ReadsAsEmpty(string? json)
    {
        // Same contract as SettingsFile.FromJson: this is read at startup on the render process, and a
        // half-written file must cost an empty list, not a launch.
        Assert.Empty(AppCache.FromJson(json));
    }

    [Fact]
    public void Read_OnMissingFile_IsEmpty()
    {
        var path = Path.Combine(Path.GetTempPath(), "halo-apps-" + Guid.NewGuid().ToString("N") + ".json");
        Assert.Empty(AppCache.Read(path));
    }

    [Fact]
    public void SaveThenRead_SurvivesTheDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), "halo-apps-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            IReadOnlyList<AppEntry> apps = [new("Halo", "Halo.App")];
            Assert.True(AppCache.Save(path, apps));
            Assert.Equal(apps, AppCache.Read(path));
        }
        finally { try { File.Delete(path); } catch { } }
    }
}
