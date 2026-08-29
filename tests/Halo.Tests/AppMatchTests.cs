using Halo.Launcher;

namespace Halo.Tests;

public sealed class AppMatchTests
{
    private static readonly IReadOnlyList<AppEntry> Apps =
    [
        new("Telegram Desktop", "telegram"),
        new("Windows Terminal", "terminal"),
        new("TeamViewer", "teamviewer"),
        new("PowerShell", "powershell"),
        new("VSCode", "vscode"),
        new("Notepad", "notepad"),
    ];

    [Fact]
    public void WholeNamePrefix_OutranksWordStart()
    {
        // "te" begins the NAME of Telegram and TeamViewer, and only the second WORD of Windows Terminal.
        // Both of the first two outrank it, whichever of the two happens to lead - that is a tie-break,
        // tested below, and asserting a winner here would be testing the wrong thing.
        var top = AppMatch.Top(Apps, "te", _ => 0).Select(a => a.Name).ToList();

        Assert.Contains("Windows Terminal", top);
        Assert.True(top.IndexOf("Telegram Desktop") < top.IndexOf("Windows Terminal"));
        Assert.True(top.IndexOf("TeamViewer") < top.IndexOf("Windows Terminal"));
    }

    [Fact]
    public void WordStart_MatchesInsideTheName()
    {
        Assert.Equal(1, AppMatch.Tier("Windows Terminal", "te"));
        Assert.Equal(2, AppMatch.Tier("Telegram Desktop", "te"));
        Assert.Equal(0, AppMatch.Tier("Notepad", "te"));
    }

    [Fact]
    public void NotASubsequence_DoesNotMatch()
    {
        // fuzzy matching was considered and rejected: "vsc" must not find "Visual Studio Code" here,
        // because once subsequences match, short queries stop being predictable
        Assert.Equal(0, AppMatch.Tier("Visual Studio Code", "vsc"));
    }

    [Fact]
    public void CamelCase_IsAWordBoundary()
    {
        Assert.Equal(["Power", "Shell"], AppMatch.Words("PowerShell"));
        Assert.Equal(["VS", "Code"], AppMatch.Words("VSCode"));
        Assert.Equal(1, AppMatch.Tier("PowerShell", "sh"));
    }

    [Fact]
    public void PunctuationAndDigits_SplitWords()
    {
        Assert.Equal(["Notepad", "7", "Zip"], AppMatch.Words("Notepad++ 7-Zip"));
    }

    [Fact]
    public void LearnedScore_BreaksTiesWithinATier()
    {
        var apps = new[] { new AppEntry("Term A", "a"), new AppEntry("Term B", "b") };

        var top = AppMatch.Top(apps, "term", id => id == "b" ? 10.0 : 0.0);

        Assert.Equal("Term B", top[0].Name);
    }

    [Fact]
    public void EqualScores_OrderByShorterNameThenOrdinal()
    {
        // without these two the order is whatever the input order happened to be, and no test of the
        // result list can be trusted
        var apps = new[]
        {
            new AppEntry("Term Zebra", "z"), new AppEntry("Term", "t"), new AppEntry("Term Alpha", "a"),
        };

        var top = AppMatch.Top(apps, "term", _ => 0);

        Assert.Equal(["Term", "Term Alpha", "Term Zebra"], top.Select(a => a.Name));
    }

    [Fact]
    public void EmptyQuery_ReturnsNothing()
    {
        // an empty field shows the menu rows, not every app on the machine
        Assert.Empty(AppMatch.Top(Apps, "", _ => 0));
        Assert.Empty(AppMatch.Top(Apps, "   ", _ => 0));
    }

    [Fact]
    public void ResultsAreCappedAtSix()
    {
        var many = Enumerable.Range(0, 40).Select(i => new AppEntry("App " + i, "id" + i)).ToArray();

        Assert.Equal(AppMatch.MaxResults, AppMatch.Top(many, "app", _ => 0).Count);
    }

    [Fact]
    public void MatchingIsCaseInsensitive()
    {
        Assert.Equal(2, AppMatch.Tier("Telegram Desktop", "TELE"));
    }
}
