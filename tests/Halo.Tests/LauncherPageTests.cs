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
        s.GoTo(LauncherState.PageQuick);

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
        Assert.Equal("Search apps, files, math...", LauncherState.PlaceholderFor(null));
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

    [Fact]
    public void BackOrErase_ErasesFirst_ThenLeavesThePage()
    {
        var s = new LauncherState(() => [], () => true, new LaunchStats(), () => DateTimeOffset.UnixEpoch);
        s.GoTo(LauncherState.PageQuick);
        s.Type('a');
        Assert.True(s.BackOrErase());
        Assert.Equal(LauncherState.PageQuick, s.Page);
        Assert.True(s.BackOrErase());
        Assert.Null(s.Page);
        Assert.False(s.BackOrErase());
    }

    [Theory]
    [InlineData("https://example.com/a", "\uE71B")]
    [InlineData("me@example.com", "\uE715")]
    [InlineData(@"C:\Users\you\file.txt", "\uE8B7")]
    [InlineData("git status", "\uE756")]
    [InlineData("a && b", "\uE756")]
    [InlineData("just a sentence", "\uE8A5")]
    public void ClipboardRows_SayWhatTheyAre(string text, string glyph)
        => Assert.Equal(glyph, ClipboardHistory.Kind(text).Glyph);

    [Fact]
    public void BackFromAddAction_ReturnsToQuickActions()
    {
        var s = new LauncherState(() => [], () => true, new LaunchStats(), () => DateTimeOffset.UnixEpoch);
        s.GoTo(LauncherState.PageAddAction);
        Assert.True(s.Back());
        Assert.Equal(LauncherState.PageQuick, s.Page);
    }

    [Fact]
    public void FreeSlot_FindsTheFirstEmpty_AndZeroWhenFull()
    {
        Assert.Equal(2, QuickActions.FreeSlot(n => n == 1 ? "a | b" : ""));
        Assert.Equal(0, QuickActions.FreeSlot(_ => "a | b"));
    }

    private static LauncherState WithClipboard(int copies, int pinned = 0)
    {
        var history = new List<ClipboardHistory.Item>();
        for (int i = 0; i < pinned; i++) history.Add(new("p" + i, "pinned " + i, true));
        for (int i = 0; i < copies; i++) history.Add(new("c" + i, "copy " + i));
        var s = new LauncherState(() => [], () => true, new LaunchStats(), () => DateTimeOffset.UnixEpoch)
        {
            PageRows = (_, q) => LauncherPages.ClipRows(history, [], q),
        };
        s.GoTo(LauncherState.PageClipboard);
        return s;
    }

    // Windows returns newest first, so "copy 0" here is the line just copied.
    private static int LastCopy(LauncherState s)
    {
        for (int i = s.Rows.Count - 1; i >= 0; i--)
            if (s.Rows[i].Label.StartsWith("copy ", StringComparison.Ordinal)) return i;
        return -1;
    }

    [Fact]
    public void Clipboard_ReadsFromTheBottomUp_LockedThenPinnedThenNewest()
    {
        var s = WithClipboard(3, pinned: 1);
        Assert.Equal(ClipboardHistory.OpenVault, s.Rows[^1].Id);
        Assert.Equal("Pinned", s.Rows[^2].Label);
        Assert.Equal("copy 0", s.Rows[^3].Label);
        Assert.Equal("copy 2", s.Rows[1].Label);      // the oldest is at the top, under Back
        Assert.Equal(s.Rows.Count - 3, s.Selected);
    }

    [Fact]
    public void Clipboard_PinnedEntriesCannotPushANewCopyOffThePage()
    {
        var s = WithClipboard(4, pinned: 11);
        Assert.Equal("copy 0", s.Rows[LastCopy(s)].Label);
        Assert.DoesNotContain(s.Rows, r => r.Label.StartsWith("pinned ", StringComparison.Ordinal));
        Assert.Contains(s.Rows, r => r.Label == "Pinned" && r.Detail == "11");
    }

    [Fact]
    public void Clipboard_ALongHistoryDropsTheOldest_AndKeepsLocked()
    {
        var s = WithClipboard(30);
        Assert.Equal("copy 0", s.Rows[LastCopy(s)].Label);
        Assert.Equal(ClipboardHistory.OpenVault, s.Rows[^1].Id);
        Assert.Equal(LauncherRowKind.Notice, s.Rows[1].Kind);
        Assert.Contains("older", s.Rows[1].Label);
    }

    [Fact]
    public void Clipboard_SearchReachesPinnedEntries()
    {
        var s = WithClipboard(2, pinned: 2);
        foreach (char c in "pinned 1") s.Type(c);
        Assert.Contains(s.Rows, r => r.Label == "pinned 1");
    }

    [Fact]
    public void BackFromPinnedOrLocked_ReturnsToTheClipboard()
    {
        foreach (var page in new[] { LauncherState.PageClipPins, LauncherState.PageVault })
        {
            var s = new LauncherState(() => [], () => true, new LaunchStats(), () => DateTimeOffset.UnixEpoch);
            s.GoTo(page);
            Assert.True(s.Back());
            Assert.Equal(LauncherState.PageClipboard, s.Page);
        }
    }

    [Fact]
    public void EditingAReminder_OpensItsOwnPage_AndBackReturnsToTheList()
    {
        var now = DateTimeOffset.UnixEpoch;
        var s = new LauncherState(() => [], () => true, new LaunchStats(), () => now);
        s.GoTo(LauncherState.PageReminders);
        s.EditReminder(new Reminder("id1", now.AddHours(2), "buy milk"));
        Assert.Equal(LauncherState.PageReminderEdit, s.Page);
        Assert.Equal("id1", s.EditingReminder);
        Assert.Equal("buy milk", s.Query);

        s.Backspace();
        s.Reset();
        Assert.Equal("id1", s.EditingReminder);   // clearing the text does not abandon the edit

        Assert.True(s.Back());
        Assert.Equal(LauncherState.PageReminders, s.Page);
        Assert.Null(s.EditingReminder);
    }

    [Fact]
    public void Vault_SealsAndOpens_AndAWrongKeyGetsNothing()
    {
        var key = new byte[32];
        key[0] = 7;
        var box = LauncherVault.Seal(TextShelf.Pack(["secret \u0633\u0644\u0627\u0645", "line\nbreak"]), key);
        Assert.Equal(["secret \u0633\u0644\u0627\u0645", "line\nbreak"], TextShelf.Unpack(LauncherVault.Open(box, key)!));

        var other = new byte[32];
        Assert.Null(LauncherVault.Open(box, other));
        box[^1] ^= 1;
        Assert.Null(LauncherVault.Open(box, key));   // and a changed file is refused, not half read
    }

    [Fact]
    public void Dpapi_RoundTrips()
    {
        var plain = new byte[] { 1, 2, 3, 4 };
        var sealedBytes = Dpapi.Protect(plain)!;
        Assert.NotEqual(plain, sealedBytes);
        Assert.Equal(plain, Dpapi.Unprotect(sealedBytes));
    }

    [Fact]
    public void ReminderForm_EveryCellAnswersAtItsOwnCentre_AndStaysInTheBox()
    {
        var f = LauncherLayout.ComputeForm(8);       // Back, five chips, Delete, Save
        for (int i = 0; i < 8; i++)
        {
            var c = f.Cells[i];
            Assert.Equal(i, LauncherLayout.Hit(f, c.X + c.Width / 2f, c.Y + c.Height / 2f));
            Assert.True(c.Right <= LauncherView.W && c.Bottom <= f.Height);
            Assert.False(c.IntersectsWith(f.Band));
        }
    }

    [Fact]
    public void ReminderForm_ATimeInTheTextWinsOverTheChosenOne()
    {
        var now = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        var s = new LauncherState(() => [], () => true, new LaunchStats(), () => now);
        s.EditReminder(new Reminder("id1", now.AddHours(5), "buy milk"));
        Assert.Equal(now.AddHours(5), s.EffectiveWhen);
        s.SetReminderWhen(now.AddHours(1));
        Assert.Equal(now.AddHours(1), s.EffectiveWhen);
        foreach (char c in " in 20m") s.Type(c);
        Assert.Equal(now.AddMinutes(20), s.EffectiveWhen);
        Assert.Equal("buy milk", s.ReminderText);
    }

    [Fact]
    public void Clipboard_ATextPinnedInHaloLeavesTheList_AndIsCounted()
    {
        var history = new List<ClipboardHistory.Item> { new("a", "keep me"), new("b", "pin me") };
        var rows = LauncherPages.ClipRows(history, ["pin me"], "");
        Assert.DoesNotContain(rows, r => r.Label == "pin me");
        Assert.Contains(rows, r => r.Label == "keep me");
        Assert.Contains(rows, r => r.Label == "Pinned" && r.Detail == "1");
    }

    [Fact]
    public void Unpinning_IsPendingUntilLeaving_AndASecondPressTakesItBack()
    {
        const string Row = "clippin:99";
        LauncherPages.TogglePendingUnpin(Row);
        Assert.True(LauncherPages.IsPendingUnpin(Row));
        LauncherPages.TogglePendingUnpin(Row);
        Assert.False(LauncherPages.IsPendingUnpin(Row));
    }

    [Fact]
    public void ClearAll_LeavesPinnedEntriesAlone()
        => Assert.Equal(["a", "c"], ClipboardHistory.ToClear([new("a", "a"), new("b", "b", true), new("c", "c")]));

    [Fact]
    public void ClearAll_ShowsOnlyOnTheClipboardPage_AndNotWhileSearching()
    {
        var s = WithClipboard(2);
        Assert.True(s.ShowClearAll);
        s.Type('x');
        Assert.False(s.ShowClearAll);
        s.GoTo(LauncherState.PageClipPins);
        Assert.False(s.ShowClearAll);
    }
}
