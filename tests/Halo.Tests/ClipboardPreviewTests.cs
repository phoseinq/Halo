using Halo.Launcher;

namespace Halo.Tests;

// One row of a launcher list out of arbitrary copied text. The whitespace rule is the interesting one:
// copied code and copied tables carry long indents, and a row that is mostly gap is unreadable.
public sealed class ClipboardPreviewTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("plain", "plain")]
    public void Preview_HandlesTheEmptyCases(string? raw, string expected)
        => Assert.Equal(expected, ClipboardHistory.Preview(raw));

    [Fact]
    public void Preview_CollapsesEveryRunOfWhitespace()
    {
        Assert.Equal("a b c", ClipboardHistory.Preview("a    b\t\tc"));
        Assert.Equal("one two", ClipboardHistory.Preview("one\r\n\r\n   two"));
    }

    [Fact]
    public void Preview_TrimsBothEnds()
        => Assert.Equal("hello", ClipboardHistory.Preview("\r\n   hello   \t"));

    [Fact]
    public void Preview_KeepsIndentedCodeReadableOnOneLine()
    {
        const string code = "if (x) {\n        doThing();\n        andAnother();\n}";
        Assert.Equal("if (x) { doThing(); andAnother(); }", ClipboardHistory.Preview(code));
    }

    [Fact]
    public void Preview_CutsLongTextAndSaysSo()
    {
        var got = ClipboardHistory.Preview(new string('x', 200));
        Assert.EndsWith("...", got);
        Assert.Equal(73, got.Length);
    }

    [Fact]
    public void Preview_DoesNotCutTextThatFits()
    {
        var got = ClipboardHistory.Preview(new string('x', 70));
        Assert.DoesNotContain("...", got);
        Assert.Equal(70, got.Length);
    }
}
