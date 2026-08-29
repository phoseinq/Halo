using System.Text.Json.Nodes;
using Halo.Panels;
using Xunit;

namespace Halo.Tests;

public class PanelSpecTests
{
    private static PanelSpec? Parse(string json)
        => PanelSpec.Parse(JsonNode.Parse(json) as JsonObject);

    [Fact]
    public void NullIsNoPanel() => Assert.Null(PanelSpec.Parse(null));

    // an empty panel would put a titled blank sheet on the pill and leave the caller thinking it worked
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"title":"Hi"}""")]
    [InlineData("""{"title":"Hi","rows":[]}""")]
    public void NoUsableRowsIsNoPanel(string json) => Assert.Null(Parse(json));

    [Fact]
    public void UnknownRowKindsAreDroppedRatherThanRejected()
    {
        var spec = Parse("""
            {"rows":[{"type":"hologram","label":"future"},{"type":"text","label":"now"}]}
            """);
        Assert.NotNull(spec);
        var row = Assert.Single(spec!.Rows);
        Assert.Equal(PanelRowKind.Text, row.Kind);
        Assert.Equal("now", row.Label);
    }

    [Fact]
    public void RowsAreCappedSoThePanelStaysOneScreen()
    {
        var rows = string.Join(",", System.Linq.Enumerable.Range(0, 40)
            .Select(i => $$"""{"type":"text","label":"row {{i}}"}"""));
        var spec = Parse($$"""{"rows":[{{rows}}]}""");
        Assert.NotNull(spec);
        Assert.Equal(PanelSpec.MaxRows, spec!.Rows.Count);
    }

    [Fact]
    public void SliderValueIsClampedIntoItsRange()
    {
        var spec = Parse("""
            {"rows":[{"type":"slider","id":"w","label":"Width","min":10,"max":50,"value":900}]}
            """);
        Assert.Equal(50, spec!.Rows[0].Value);
    }

    // an inverted range would divide by zero working out where the thumb goes
    [Fact]
    public void InvertedRangeFallsBackInsteadOfDividingByZero()
    {
        var row = Parse("""
            {"rows":[{"type":"slider","id":"w","min":90,"max":10,"value":50}]}
            """)!.Rows[0];
        Assert.True(row.Max > row.Min);
    }

    // a caller assembling the body by hand sends "42", not 42
    [Theory]
    [InlineData("\"42\"", 42)]
    [InlineData("42", 42)]
    public void NumbersArriveAsStringsToo(string literal, double expected)
    {
        var row = Parse($$"""{"rows":[{"type":"slider","id":"a","min":0,"max":100,"value":{{literal}}}]}""")!.Rows[0];
        Assert.Equal(expected, row.Value);
    }

    [Theory]
    [InlineData("true", 1)]
    [InlineData("\"on\"", 1)]
    [InlineData("false", 0)]
    [InlineData("\"nonsense\"", 0)]
    public void ToggleReadsBoolsAndTheStringsPeopleSendInstead(string literal, double expected)
    {
        var row = Parse($$"""{"rows":[{"type":"toggle","id":"g","value":{{literal}}}]}""")!.Rows[0];
        Assert.Equal(expected, row.Value);
    }

    // a segmented control with one option is a label wearing a button's clothes
    [Theory]
    [InlineData("""["only"]""")]
    [InlineData("[]")]
    public void ButtonsNeedSomethingToChooseBetween(string options)
    {
        Assert.Null(Parse($$"""{"rows":[{"type":"buttons","id":"h","options":{{options}}}]}"""));
    }

    [Fact]
    public void ButtonsSelectionIsClampedToWhatExists()
    {
        var row = Parse("""
            {"rows":[{"type":"buttons","id":"h","options":["a","b","c"],"value":9}]}
            """)!.Rows[0];
        Assert.Equal(2, row.Value);
    }

    [Fact]
    public void OptionsAreCapped()
    {
        var row = Parse("""
            {"rows":[{"type":"buttons","id":"h","options":["a","b","c","d","e","f","g","h"]}]}
            """)!.Rows[0];
        Assert.Equal(PanelSpec.MaxOptions, row.Options.Count);
    }

    // anything the user can move has to report the move under a name
    [Theory]
    [InlineData("slider")]
    [InlineData("toggle")]
    public void InteractiveRowsWithoutAnIdAreDropped(string type)
    {
        Assert.Null(Parse($$"""{"rows":[{"type":"{{type}}","label":"nameless"}]}"""));
    }

    [Fact]
    public void TextRowsNeedNoId()
    {
        var spec = Parse("""{"rows":[{"type":"text","label":"Status","text":"Building"}]}""");
        Assert.Equal("Building", spec!.Rows[0].Text);
    }

    [Fact]
    public void MeterIsAFractionAndIsClamped()
    {
        var row = Parse("""{"rows":[{"type":"meter","label":"Progress","value":4.5}]}""")!.Rows[0];
        Assert.Equal(1, row.Value);
    }

    // a 4000-character label is a layout problem on every frame for as long as the panel is up
    [Fact]
    public void LongTextIsClippedAtTheDoor()
    {
        var spec = Parse($$"""{"title":"{{new string('x', 500)}}","rows":[{"type":"text","label":"{{new string('y', 500)}}"}]}""");
        Assert.True(spec!.Title.Length <= 48);
        Assert.True(spec.Rows[0].Label.Length <= 48);
    }

    [Fact]
    public void NewlinesInLabelsBecomeSpaces()
    {
        var spec = Parse("""{"rows":[{"type":"text","label":"two\nlines"}]}""");
        Assert.DoesNotContain('\n', spec!.Rows[0].Label);
    }
}
