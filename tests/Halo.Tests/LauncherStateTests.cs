using Halo.Launcher;

namespace Halo.Tests;

public sealed class LauncherStateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-23T12:00:00Z");

    private static readonly IReadOnlyList<AppEntry> Apps =
    [
        new("Telegram Desktop", "telegram"),
        new("Windows Terminal", "terminal"),
        new("TeamViewer", "teamviewer"),
    ];

    private static LauncherState Make(IReadOnlyList<AppEntry>? apps = null, bool ready = true)
        => new(() => apps ?? Apps, () => ready, new LaunchStats(), () => Now);

    private static void TypeAll(LauncherState s, string text)
    {
        foreach (char c in text) s.Type(c);
    }

    [Fact]
    public void EmptyQuery_ShowsTheSixMenuRows()
    {
        var s = Make();

        Assert.Equal(6, s.Rows.Count);
        Assert.Equal(["Quick Actions", "System Info", "Clipboard History", "Translate", "Reminders", "Settings"],
            s.Rows.Select(r => r.Label));
    }

    [Fact]
    public void EveryMenuRowIsLiveNow()
    {
        // The five placeholders were "deliberate and temporary" - the promise that this surface would
        // grow. All five have since been built, Translate last because it is the only one needing a
        // service behind it. Nothing on this menu is a placeholder any more.
        var s = Make();

        Assert.Equal(6, s.Rows.Count);
        Assert.All(s.Rows, r => Assert.True(r.Enabled, r.Label + " is not live"));
    }

    [Fact]
    public void SelectionStartsOnTheFirstEnabledMenuRow()
    {
        // was "the only enabled row" back when five of the six were inert placeholders. Four of them now
        // open pages, so the first enabled row is the first row.
        var s = Make();

        Assert.Equal("Quick Actions", s.Rows[s.Selected].Label);
    }

    [Fact]
    public void Typing_SwapsMenuRowsForApps()
    {
        var s = Make();
        TypeAll(s, "te");

        Assert.All(s.Rows, r => Assert.Equal(LauncherRowKind.App, r.Kind));
        // TeamViewer and Telegram both begin with "te", so the tier does not separate them and the
        // shorter name leads. Nothing here has been launched yet; once it has, the learned score
        // decides this long before the length does.
        Assert.Equal("TeamViewer", s.Rows[0].Label);
        Assert.Equal(0, s.Selected);
    }

    [Fact]
    public void ClearingTheQuery_BringsTheMenuBack()
    {
        var s = Make();
        TypeAll(s, "te");
        s.Backspace();
        s.Backspace();

        Assert.Equal(6, s.Rows.Count);
        Assert.Equal("Quick Actions", s.Rows[s.Selected].Label);
    }

    [Fact]
    public void BackspaceOnEmpty_IsHarmless()
    {
        var s = Make();
        s.Backspace();

        Assert.Equal("", s.Query);
    }

    [Fact]
    public void Move_ClampsAtBothEnds()
    {
        var s = Make();
        TypeAll(s, "te");

        s.Move(-1);
        Assert.Equal(0, s.Selected);

        for (int i = 0; i < 20; i++) s.Move(1);
        Assert.Equal(s.Rows.Count - 1, s.Selected);
    }

    [Fact]
    public void Move_WalksTheMenuOneRowAtATime()
    {
        // this used to assert that arrowing JUMPED the inert Translate row. There are no inert rows left,
        // so the property worth keeping is the plain one: every row is reachable and none is skipped.
        var s = Make();
        s.SelectAt(0);

        string[] expected = ["System Info", "Clipboard History", "Translate", "Reminders", "Settings"];
        foreach (var label in expected)
        {
            s.Move(1);
            Assert.Equal(label, s.Rows[s.Selected].Label);
        }

        s.Move(1);   // and it stops at the end rather than wrapping
        Assert.Equal("Settings", s.Rows[s.Selected].Label);
    }

    [Fact]
    public void Enter_OnAnApp_ReturnsIt()
    {
        var s = Make();
        TypeAll(s, "tele");   // long enough that only one app matches - this test is about Enter, not order

        var row = s.Enter();

        Assert.NotNull(row);
        Assert.Equal("telegram", row!.Aumid);
        Assert.Equal(LauncherRowKind.App, row.Kind);
    }

    [Fact]
    public void Enter_OnTheMenu_ReturnsTheSelectedMenuRow()
    {
        var row = Make().Enter();

        Assert.NotNull(row);
        Assert.Equal(LauncherRowKind.Page, row!.Kind);
        Assert.Equal(LauncherState.PageQuick, row.Id);
    }

    [Fact]
    public void Enter_OnSettings_StillReturnsTheSettingsRow()
    {
        var s = Make();
        s.SelectAt(5);

        var row = s.Enter();
        Assert.NotNull(row);
        Assert.Equal(LauncherRowKind.Settings, row!.Kind);
    }

    [Fact]
    public void Enter_WithNoMatch_DoesNothing()
    {
        // no web search, no run-as-command. Not asked for, not built.
        var s = Make();
        TypeAll(s, "zzzz");

        Assert.Empty(s.Rows);
        Assert.Null(s.Enter());
    }

    [Fact]
    public void ColdIndex_SaysSoInsteadOfShowingNothing()
    {
        var s = Make(apps: [], ready: false);
        TypeAll(s, "te");

        Assert.Single(s.Rows);
        Assert.Equal(LauncherState.IndexingLabel, s.Rows[0].Label);
        Assert.Equal(LauncherRowKind.Notice, s.Rows[0].Kind);
        Assert.False(s.Rows[0].Enabled);
        Assert.Null(s.Enter());
    }

    [Fact]
    public void Reset_ClearsQueryAndSelection()
    {
        var s = Make();
        TypeAll(s, "te");
        s.Reset();

        Assert.Equal("", s.Query);
        Assert.Equal("Quick Actions", s.Rows[s.Selected].Label);
    }

    [Fact]
    public void ControlCharacters_AreNotTyped()
    {
        var s = Make();
        s.Type('\t');
        s.Type('\r');
        s.Type('\b');

        Assert.Equal("", s.Query);
    }

    [Fact]
    public void LeadingSpace_IsIgnoredButInteriorSpacesWork()
    {
        // Alt+Space is the hotkey. A space arriving before anything has been typed is almost always
        // that chord leaking through rather than a query, and a query cannot usefully start with one
        // anyway. Once there IS a query, "windows te" needs its space.
        var s = Make();
        s.Type(' ');
        Assert.Equal("", s.Query);

        TypeAll(s, "windows te");
        Assert.Equal("windows te", s.Query);
        Assert.Equal("Windows Terminal", s.Rows[0].Label);
    }

    [Fact]
    public void QueryLength_IsBounded()
    {
        // a search field is not a text editor - the same reason the ask banner caps at 400
        var s = Make();
        TypeAll(s, new string('a', 200));

        Assert.True(s.Query.Length <= 64);
    }

    // ---- Tab -----------------------------------------------------------------------------------------

    [Fact]
    public void Tab_CompletesTheQueryToTheHighlightedApp()
    {
        var s = Make();
        TypeAll(s, "tele");
        Assert.Equal(LauncherState.TabResult.Changed, s.Tab());
        Assert.Equal("Telegram Desktop", s.Query);
        // and the list has narrowed to it, so Enter now launches without another keystroke
        Assert.Single(s.Rows);
    }

    [Fact]
    public void Tab_OnTheMenuMovesTheHighlight_AndNeverTypesAPageNameIntoTheAppSearch()
    {
        // "System Info" is a page name, not an app. Completing it into a field that searches APPS would
        // match nothing and look like Tab had broken the box.
        var s = Make();
        int first = s.Selected;
        Assert.Equal(LauncherState.TabResult.Changed, s.Tab());
        Assert.Equal("", s.Query);
        Assert.NotEqual(first, s.Selected);
    }

    [Fact]
    public void Tab_DoesNothingWhenTheQueryAlreadyIsTheHighlightedRow()
    {
        var s = Make();
        TypeAll(s, "tele");
        s.Tab();
        Assert.Equal(LauncherState.TabResult.Ignored, s.Tab());
        Assert.Equal("Telegram Desktop", s.Query);
    }

    [Fact]
    public void Tab_OnRemindersCyclesTheSyntaxScaffolds()
    {
        var s = Make();
        s.PageRows = (_, _) => [];
        s.GoTo(LauncherState.PageReminders);

        Assert.Equal(LauncherState.TabResult.Changed, s.Tab());
        string first = s.Query;
        Assert.StartsWith("in ", first);

        var seen = new List<string> { first };
        for (int i = 0; i < 3; i++) { s.Tab(); seen.Add(s.Query); }
        Assert.Equal(4, seen.Distinct().Count());   // four distinct scaffolds...
        s.Tab();
        Assert.Equal(first, s.Query);               // ...and then it wraps
    }

    [Fact]
    public void Tab_OnTranslateAsksForTheDirectionInstead()
    {
        // there is nothing to complete a line of prose against, so the page's own next thing is offered
        var s = Make();
        s.PageRows = (_, _) => [];
        s.GoTo(LauncherState.PageTranslate);
        TypeAll(s, "hello there");

        Assert.Equal(LauncherState.TabResult.CycleTranslatePair, s.Tab());
        Assert.Equal("hello there", s.Query);   // and it does NOT eat what was typed
    }

    [Fact]
    public void Tab_OnAFilteredPageCompletesToTheRowLabel()
    {
        var s = Make();
        s.PageRows = (_, _) =>
        [
            new LauncherRow("Mute", null, true, LauncherRowKind.Action, "act:mute"),
            new LauncherRow("Lock the screen", null, true, LauncherRowKind.Action, "act:lock"),
        ];
        s.GoTo(LauncherState.PageQuick);
        TypeAll(s, "loc");

        Assert.Equal(LauncherState.TabResult.Changed, s.Tab());
        Assert.Equal("Lock the screen", s.Query);
    }

    // ---- the inline ghost -------------------------------------------------------------------------
    //
    // Tab has completed to the selected row since the launcher shipped and nothing on screen said so.
    // Reported as wanting the rest of the name written faintly: "when I type tel, because I just opened
    // Telegram, write telegram faded and let Tab finish it."

    [Fact]
    public void APrefixOffersTheRestOfTheName()
    {
        var s = Make();

        TypeAll(s, "tel");

        Assert.Equal("egram Desktop", s.Completion);
    }

    [Fact]
    public void TakingTheOfferTypesTheWordThatWasGhosted()
    {
        // The pairing that matters: the LETTERS the ghost showed are the letters Tab commits, or the hint is
        // a lie the first time somebody presses it. Not the capitals - you type "tel" and get "Telegram
        // Desktop", because the app has a name and the field was only ever a way of finding it. Every URL bar
        // does this, and the alternative is committing "telegram Desktop", which is nobody's app.
        var s = Make();
        TypeAll(s, "tel");
        string promised = s.Query + s.Completion;

        s.Tab();

        Assert.Equal(promised, s.Query, ignoreCase: true);
        Assert.Equal("Telegram Desktop", s.Query);
        Assert.Equal("", s.Completion);   // nothing left to finish once it is finished
    }

    [Fact]
    public void AMatchInTheMiddleOfTheNameIsNotGhosted()
    {
        // "term" finds Windows Terminal, and a ghost is a promise about the letters that come NEXT - drawing
        // "term" followed by a faded "Windows Terminal"'s tail spells nothing. The row is still highlighted
        // and Tab still completes to it; there is just no honest way to draw that inline.
        var s = Make();

        TypeAll(s, "term");

        Assert.Contains(s.Rows, r => r.Label == "Windows Terminal");
        Assert.Equal("", s.Completion);
    }

    [Fact]
    public void CaseIsNotWhatDecidesIt()
    {
        var s = Make();

        TypeAll(s, "TELE");

        Assert.Equal("gram Desktop", s.Completion);
    }

    [Fact]
    public void AnEmptyFieldHasNothingToFinish()
    {
        // the menu is showing and its rows are page names; ghosting "Quick Actions" into the app field would
        // offer a completion that matches no app at all
        var s = Make();

        Assert.Equal("", s.Completion);
    }

    [Fact]
    public void APageThatTakesTextIsNeverGhosted()
    {
        // on Reminders the field is the reminder's own words, not a filter - there is nothing it could be
        // completing towards
        var s = Make();
        s.GoTo(LauncherState.PageReminders);

        TypeAll(s, "buy");

        Assert.Equal("", s.Completion);
    }
}
