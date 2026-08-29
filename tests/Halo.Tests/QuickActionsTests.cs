using Halo.Launcher;

namespace Halo.Tests;

public sealed class QuickActionsTests
{
    [Fact]
    public void TheThreeThatShippedFirstAreOnByDefault_AndTheRestAreOptIn()
    {
        Assert.True(QuickActions.DefaultOn(QuickActions.IdMute));
        Assert.True(QuickActions.DefaultOn(QuickActions.IdLock));
        Assert.True(QuickActions.DefaultOn(QuickActions.IdSleep));
        Assert.False(QuickActions.DefaultOn(QuickActions.IdDownloads));
        Assert.False(QuickActions.DefaultOn(QuickActions.IdRecycle));
    }

    [Fact]
    public void EveryBuiltinHasADistinctIdAndAGlyph()
    {
        // a duplicate id would silently make two rows share one settings key, and a missing glyph leaves a
        // hole in the icon gutter that the row renderer will not fill
        Assert.Equal(QuickActions.All.Length, QuickActions.All.Select(b => b.Id).Distinct().Count());
        Assert.All(QuickActions.All, b => Assert.False(string.IsNullOrEmpty(b.Glyph)));
        Assert.All(QuickActions.All, b => Assert.False(string.IsNullOrWhiteSpace(b.Label)));
    }

    [Fact]
    public void Enabled_KeepsPageOrder_AndDropsWhatIsSwitchedOff()
    {
        var kept = QuickActions.Enabled(id => id is QuickActions.IdSleep or QuickActions.IdMute);
        // the answer follows the CATALOGUE's order, not the order the predicate happened to say yes in
        Assert.Equal([QuickActions.IdMute, QuickActions.IdSleep], kept.Select(b => b.Id));
    }

    [Fact]
    public void Enabled_CanBeEmpty_WhichThePageHasToHandle()
        => Assert.Empty(QuickActions.Enabled(_ => false));

    [Theory]
    [InlineData("Notes | C:\\notes.txt", "Notes", "C:\\notes.txt")]
    [InlineData("  Notes  |  C:\\notes.txt  ", "Notes", "C:\\notes.txt")]
    public void ParseCustom_SplitsLabelFromTarget(string line, string label, string target)
    {
        var got = QuickActions.ParseCustom(line);
        Assert.NotNull(got);
        Assert.Equal(label, got!.Value.Label);
        Assert.Equal(target, got.Value.Target);
    }

    [Fact]
    public void ParseCustom_SplitsOnTheFirstPipeOnly_SoAUrlKeepsItsQuery()
    {
        var got = QuickActions.ParseCustom("Search | https://example.com/?a=1|2");
        Assert.Equal("Search", got!.Value.Label);
        Assert.Equal("https://example.com/?a=1|2", got.Value.Target);
    }

    [Fact]
    public void ParseCustom_TakesABareTargetAndNamesItAfterItself()
    {
        // pasting a path in and expecting it to work is the obvious thing to try, so it works
        var file = QuickActions.ParseCustom("C:\\tools\\ripgrep.exe");
        Assert.Equal("ripgrep.exe", file!.Value.Label);
        Assert.Equal("C:\\tools\\ripgrep.exe", file.Value.Target);

        var url = QuickActions.ParseCustom("https://news.ycombinator.com/newest");
        Assert.Equal("news.ycombinator.com", url!.Value.Label);
    }

    [Fact]
    public void ParseCustom_NamesAFolderAfterItsLastSegment_TrailingSlashOrNot()
    {
        Assert.Equal("Projects", QuickActions.ParseCustom("D:\\Projects")!.Value.Label);
        Assert.Equal("Projects", QuickActions.ParseCustom("D:\\Projects\\")!.Value.Label);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Label only |")]
    [InlineData("Label only |    ")]
    public void ParseCustom_RefusesALineWithNothingToOpen(string? line)
        => Assert.Null(QuickActions.ParseCustom(line));

    [Fact]
    public void TheKeysAreStable_BecauseTheyAreWhatIsInTheSettingsFile()
    {
        // renaming either of these silently resets every user's choices to the defaults
        Assert.Equal("quick.mute", QuickActions.EnabledKey(QuickActions.IdMute));
        Assert.Equal("quick.custom2", QuickActions.CustomKey(2));
    }
}
