using Halo.Launcher;

namespace Halo.Tests;

public sealed class LauncherExtrasTests
{
    [Theory]
    [InlineData("2+2", "4")]
    [InlineData("2 + 3 * 4", "14")]
    [InlineData("(2+3)*4", "20")]
    [InlineData("2^3^2", "512")]
    [InlineData("3x4", "12")]
    [InlineData("200*10%", "20")]
    [InlineData("1,000+5", "1,005")]
    [InlineData("0.1+0.2", "0.3")]
    [InlineData("sqrt(16)", "4")]
    [InlineData("-2*3", "-6")]
    [InlineData("\u06F2+\u06F3", "5")]
    [InlineData("6\u00F72", "3")]
    public void Sums_AreAnswered(string line, string shown)
        => Assert.Equal(shown, LauncherCalc.Try(line)?.Shown);

    [Theory]
    [InlineData("10 km to mi", "6.2137119224 mi")]
    [InlineData("100 C to F", "212 F")]
    [InlineData("32 f to c", "0 C")]
    [InlineData("1 GiB in MB", "1,073.741824 MB")]
    [InlineData("2 hours to min", "120 min")]
    [InlineData("5 in to cm", "12.7 cm")]
    public void Conversions_AreAnswered(string line, string shown)
        => Assert.Equal(shown, LauncherCalc.Try(line)?.Shown);

    [Theory]
    [InlineData("")]
    [InlineData("telegram")]
    [InlineData("2026")]
    [InlineData("-5")]
    [InlineData("2+")]
    [InlineData("1/0")]
    [InlineData("foo(2)")]
    [InlineData("10 km to kg")]
    [InlineData("box2x")]
    public void AnythingElse_IsNotASum(string line)
        => Assert.Null(LauncherCalc.Try(line));

    [Fact]
    public void AVeryDeepLine_FailsInsteadOfOverflowingTheStack()
        => Assert.Null(LauncherCalc.Try(new string('(', 5000) + "1" + new string(')', 5000)));

    [Fact]
    public void TheCopiedValue_HasNoGrouping()
        => Assert.Equal("1234567", LauncherCalc.Try("1234567*1")?.Copy);

    [Fact]
    public void ACommandLine_ReplacesEverythingElse()
    {
        var set = LauncherExtras.For("> ipconfig /all");
        Assert.True(set.Exclusive);
        Assert.Equal(LauncherExtras.RunPrefix + "ipconfig /all", Assert.Single(set.Above).Id);
        Assert.Empty(set.Below);
    }

    [Fact]
    public void TheWeb_IsOfferedBelowAndAnswersGoAbove()
    {
        var set = LauncherExtras.For("2+2");
        Assert.StartsWith(LauncherExtras.CopyPrefix, Assert.Single(set.Above).Id);
        Assert.StartsWith(LauncherExtras.WebPrefix, Assert.Single(set.Below).Id);
    }

    [Fact]
    public void WebUrl_EscapesTheLine()
        => Assert.Equal("https://www.google.com/search?q=a%26b%20c", LauncherExtras.WebUrl("a&b c"));

    [Theory]
    [InlineData("it's'; DROP", "\"its*\" AND \"DROP*\"")]
    [InlineData("report.pdf", "\"report.pdf*\"")]
    public void FileSearch_LeavesNothingThatCanEscapeTheLiteral(string line, string match)
    {
        string sql = FileSearch.BuildSql(line, 3)!;
        Assert.Contains($"CONTAINS(System.FileName, '{match}')", sql);
    }

    [Fact]
    public void FileSearch_RefusesALineOfOnlySymbols()
        => Assert.Null(FileSearch.BuildSql("'\"%;", 3));

    [Fact]
    public void FileSearch_SplitsPersianAtTheZeroWidthJoiner()
    {
        string sql = FileSearch.BuildSql("\u0645\u06CC\u200C\u062E\u0648\u0627\u0647\u0645", 3)!;
        Assert.Contains("\"\u0645\u06CC*\" AND \"\u062E\u0648\u0627\u0647\u0645*\"", sql);
    }

    [Fact]
    public void FileSearch_CanAskForFoldersAlone()
    {
        Assert.Contains("System.ItemType = 'Directory'", FileSearch.BuildSql("down", 3, foldersOnly: true));
        Assert.DoesNotContain("System.ItemType", FileSearch.BuildSql("down", 3));
    }

    [Theory]
    [InlineData(@"C:\Users", true)]
    [InlineData("C:/Users", true)]
    [InlineData(@"\\server\share", true)]
    [InlineData(@"~\Documents", true)]
    [InlineData("%TEMP%", true)]
    [InlineData("downloads", false)]
    [InlineData("a:b", false)]
    public void IsPath_TellsALocationFromAName(string line, bool expected)
        => Assert.Equal(expected, FileSearch.IsPath(line));

    [Fact]
    public void Browse_ListsFoldersFirst_AndNarrowsByWhatIsTyped()
    {
        string root = Path.Combine(Path.GetTempPath(), "halo-browse-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "beta"));
            Directory.CreateDirectory(Path.Combine(root, "other"));
            File.WriteAllText(Path.Combine(root, "bfile.txt"), "x");

            var all = FileSearch.Browse(root + @"\", 10);
            Assert.Equal(3, all.Count);
            Assert.True(Directory.Exists(all[0]) && Directory.Exists(all[1]));

            var some = FileSearch.Browse(root + @"\b", 10);
            Assert.Equal([Path.Combine(root, "beta"), Path.Combine(root, "bfile.txt")], some);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Tab_WalksIntoAFolder()
    {
        string folder = @"C:\Users\you\Documents";
        var s = new LauncherState(() => [], () => true, new LaunchStats(), () => DateTimeOffset.UnixEpoch)
        {
            FileHits = _ => [new LauncherRow("Documents", null, true, LauncherRowKind.Action,
                                             LauncherExtras.FilePrefix + folder, null, FileSearch.FolderGlyph)],
        };
        foreach (char c in "docu") s.Type(c);
        s.Tab();
        Assert.Equal(folder + @"\", s.Query);
    }
}
