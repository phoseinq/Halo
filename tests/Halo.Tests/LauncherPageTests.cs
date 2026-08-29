using Halo.Launcher;

namespace Halo.Tests;

// Page navigation and the filter-then-trim order. The order is the whole content of these tests: trimming
// first turns a page's search box into a search over the rows already on screen.
public sealed class LauncherPageTests
{
    private static LauncherState Fresh(Func<string, string, IReadOnlyList<LauncherRow>>? pages = null)
    {
        var s = new LauncherState(() => [], () => true, new LaunchStats(), () => DateTimeOffset.UnixEpoch);
        s.PageRows = pages ?? ((_, _) => []);
        return s;
    }

    private static IReadOnlyList<LauncherRow> Numbered(int n)
        => [.. Enumerable.Range(0, n).Select(i =>
               new LauncherRow("item " + i, null, true, LauncherRowKind.Action, "id" + i))];

    [Fact]
    public void GoTo_ReplacesTheMenuWithThePage()
    {
        var s = Fresh((_, _) => Numbered(3));
        s.GoTo(LauncherState.PageClipboard);

        Assert.Equal(LauncherState.PageClipboard, s.Page);
        Assert.Equal(4, s.Rows.Count);                       // Back, then the three
        Assert.Equal(LauncherRowKind.Back, s.Rows[0].Kind);
    }

    [Fact]
    public void Back_ReturnsToTheMenu_AndSaysItDidSomething()
    {
        var s = Fresh((_, _) => Numbered(3));
        s.GoTo(LauncherState.PageSystem);

        Assert.True(s.Back());
        Assert.Null(s.Page);
        Assert.Equal(6, s.Rows.Count);
        Assert.False(s.Back());     // on the menu there is nothing to go back to, so Escape may close
    }

    [Fact]
    public void GoTo_DropsTheQueryThatFoundTheRow()
    {
        var s = Fresh((_, _) => Numbered(3));
        foreach (char c in "clip") s.Type(c);
        s.GoTo(LauncherState.PageClipboard);

        Assert.Equal("", s.Query);
    }

    [Fact]
    public void ALongPage_IsTrimmedAndSaysHowMuchIsHidden()
    {
        var s = Fresh((_, _) => Numbered(20));
        s.GoTo(LauncherState.PageClipboard);

        Assert.Equal(LauncherState.MaxPageRows + 2, s.Rows.Count);   // Back + the cap + the "more" notice
        Assert.Equal(LauncherRowKind.Notice, s.Rows[^1].Kind);
        Assert.Contains($"{20 - LauncherState.MaxPageRows} more", s.Rows[^1].Label);
    }

    [Fact]
    public void TypingOnAPage_SearchesTheWholeList_NotOnlyTheRowsOnScreen()
    {
        // "item 19" is well past the 8-row cap. Trimming before filtering made it unreachable, which is
        // exactly what the "N more - keep typing" row was promising you could do.
        var s = Fresh((_, _) => Numbered(20));
        s.GoTo(LauncherState.PageClipboard);
        foreach (char c in "item 19") s.Type(c);

        Assert.Equal(2, s.Rows.Count);
        Assert.Equal("item 19", s.Rows[1].Label);
    }

    [Fact]
    public void TheFilterAlsoLooksAtTheDetailColumn()
    {
        var s = Fresh((_, _) => [new LauncherRow("Memory", null, false, LauncherRowKind.Info, null, "31.7 GB")]);
        s.GoTo(LauncherState.PageSystem);
        foreach (char c in "31.7") s.Type(c);

        Assert.Equal(2, s.Rows.Count);
        Assert.Equal("Memory", s.Rows[1].Label);
    }

    [Fact]
    public void EveryPageOpensWithAWayBack()
    {
        // Escape already went back, but nothing on screen said so and a mouse could not reach it
        var s = Fresh((_, _) => Numbered(3));
        s.GoTo(LauncherState.PageQuick);

        Assert.Equal(LauncherRowKind.Back, s.Rows[0].Kind);
        Assert.True(s.Rows[0].Enabled);
    }

    [Fact]
    public void TheSelectionSkipsBack_SoEnterDoesTheThingYouCameFor()
    {
        var s = Fresh((_, _) => Numbered(3));
        s.GoTo(LauncherState.PageQuick);

        Assert.Equal(1, s.Selected);
        Assert.Equal("item 0", s.Rows[s.Selected].Label);
    }

    [Fact]
    public void APageWithNothingInIt_StillLandsSomewhereSafe()
    {
        // only Back and a disabled notice: the selection has nowhere useful to go and must not crash
        var s = Fresh((_, _) => [new LauncherRow("nothing here", null, false, LauncherRowKind.Notice)]);
        s.GoTo(LauncherState.PageReminders);

        Assert.Equal(0, s.Selected);
        Assert.Equal(LauncherRowKind.Back, s.Rows[s.Selected].Kind);
    }

    [Fact]
    public void AFixedPageIsNeverTruncated()
    {
        // System Info is about eleven rows and is a table, not a feed. The cap exists for the clipboard;
        // it must not be tight enough to hide the end of a page that has a natural end.
        var s = Fresh((_, _) => Numbered(11));
        s.GoTo(LauncherState.PageSystem);

        Assert.Equal(12, s.Rows.Count);        // Back + all eleven
        Assert.DoesNotContain(s.Rows, r => r.Label.Contains("more - keep typing"));
    }

    [Fact]
    public void ThePlaceholderSaysWhatTypingDoesHere()
    {
        // the field is the only control on the page, and one fixed string made it lie on four of them
        Assert.Equal("Search apps...", LauncherState.PlaceholderFor(null));
        Assert.Equal("Search clipboard...", LauncherState.PlaceholderFor(LauncherState.PageClipboard));
        Assert.Equal("Filter actions...", LauncherState.PlaceholderFor(LauncherState.PageQuick));
        Assert.Equal("Filter system info...", LauncherState.PlaceholderFor(LauncherState.PageSystem));
    }

    [Fact]
    public void PagesThatTakeInput_PromptForInputRatherThanForASearch()
    {
        // on these two the query is not a filter, so "Search..." would be describing the wrong action
        foreach (var page in new[] { LauncherState.PageReminders, LauncherState.PageTranslate })
        {
            var text = LauncherState.PlaceholderFor(page);
            Assert.DoesNotContain("Search", text);
            Assert.DoesNotContain("Filter", text);
        }
    }

    [Fact]
    public void EveryPageHasItsOwnPlaceholder_AndNoneIsBlank()
    {
        string[] pages =
        [
            LauncherState.PageQuick, LauncherState.PageSystem, LauncherState.PageClipboard,
            LauncherState.PageReminders, LauncherState.PageTranslate,
        ];
        var seen = pages.Select(LauncherState.PlaceholderFor).ToList();

        Assert.All(seen, t => Assert.False(string.IsNullOrWhiteSpace(t)));
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.DoesNotContain(LauncherState.PlaceholderFor(null), seen);
    }

    [Fact]
    public void OnlyRemindersReadsTheQueryAsACommand()
    {
        Assert.True(LauncherState.PageTakesText(LauncherState.PageReminders));
        Assert.False(LauncherState.PageTakesText(LauncherState.PageClipboard));
        Assert.False(LauncherState.PageTakesText(LauncherState.PageSystem));
        Assert.False(LauncherState.PageTakesText(LauncherState.PageQuick));
    }
}
