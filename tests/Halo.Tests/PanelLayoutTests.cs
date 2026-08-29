using System.Drawing;
using System.Text.Json.Nodes;
using Halo.Panels;
using Xunit;

namespace Halo.Tests;

public class PanelLayoutTests
{
    private static PanelSpec Spec(params string[] rows)
        => PanelSpec.Parse(JsonNode.Parse($$"""{"title":"T","rows":[{{string.Join(",", rows)}}]}""") as JsonObject)!;

    private const string Slider = """{"type":"slider","id":"w","label":"Width","min":0,"max":100,"value":25}""";
    private const string Toggle = """{"type":"toggle","id":"g","label":"Glass","value":true}""";
    private const string Text = """{"type":"text","label":"Status","text":"Building"}""";
    private const string Segments = """{"type":"buttons","id":"h","options":["a","b","c"],"value":1}""";

    // The panel is a widget, so it gets the expanded pill's slot and nothing more. The first version of
    // this laid rows out at 52px in a 470-wide panel and came to 426 tall - it would have drawn its last
    // two rows off the bottom of a 220px slot, and looked like the pill cutting the panel off rather than
    // the layout being wrong. A full panel has to fit exactly.
    [Fact]
    public void AFullPanelFitsTheExpandedPillExactly()
    {
        var rows = Enumerable.Repeat(Slider, PanelSpec.MaxRows);
        float h = PanelLayout.Height(PanelSpec.Parse(
            JsonNode.Parse($$"""{"title":"T","rows":[{{string.Join(",", rows)}}]}""") as JsonObject)!);
        Assert.True(h <= PanelLayout.SlotHeight, $"a full panel is {h}px against a {PanelLayout.SlotHeight}px slot");
        Assert.True(h > PanelLayout.SlotHeight - PanelLayout.RowH,
            "the slot has room for another row, so MaxRows is leaving space unused");
    }

    [Fact]
    public void EveryRowOfAFullPanelIsInsideTheSlot()
    {
        var rows = Enumerable.Repeat(Toggle, PanelSpec.MaxRows);
        var spec = PanelSpec.Parse(
            JsonNode.Parse($$"""{"title":"T","rows":[{{string.Join(",", rows)}}]}""") as JsonObject)!;
        foreach (var slot in PanelLayout.Slots(spec, PanelLayout.Width))
            Assert.True(slot.Row.Bottom <= PanelLayout.SlotHeight, "a row hangs off the bottom of the pill");
    }

    [Fact]
    public void HeightGrowsOneRowAtATime()
    {
        float one = PanelLayout.Height(Spec(Text));
        float two = PanelLayout.Height(Spec(Text, Text));
        Assert.Equal(PanelLayout.RowH + PanelLayout.RowGap, two - one, 3);
    }

    [Fact]
    public void RowsDoNotOverlapAndStayInsideTheHeight()
    {
        var spec = Spec(Text, Slider, Toggle, Segments);
        var slots = PanelLayout.Slots(spec, PanelLayout.Width);
        for (int i = 1; i < slots.Count; i++)
            Assert.True(slots[i].Row.Top >= slots[i - 1].Row.Bottom, $"row {i} overlaps row {i - 1}");
        Assert.True(slots[^1].Row.Bottom <= PanelLayout.Height(spec));
    }

    [Fact]
    public void ControlStaysInsideItsRow()
    {
        var slots = PanelLayout.Slots(Spec(Slider, Toggle), PanelLayout.Width);
        foreach (var slot in slots)
        {
            Assert.True(slot.Control.Right <= slot.Row.Right);
            Assert.True(slot.Control.Left >= slot.Row.Left);
        }
    }

    // a narrower pill must shrink the label, not push the control off the edge
    [Theory]
    [InlineData(470)]
    [InlineData(360)]
    [InlineData(280)]
    public void ControlSurvivesANarrowerPill(int w)
    {
        var slot = PanelLayout.Slots(Spec(Slider), w)[0];
        Assert.True(slot.Control.Right <= w - PanelLayout.Pad);
        Assert.True(slot.Control.Width > 0);
    }

    // the pair has to be exact inverses or the slider jumps the instant you touch it
    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    public void ClickingTheThumbLeavesTheValueWhereItWas(double value)
    {
        var spec = Spec($$"""{"type":"slider","id":"w","min":0,"max":100,"value":{{value}}}""");
        var slot = PanelLayout.Slots(spec, PanelLayout.Width)[0];
        var row = spec.Rows[0];
        var thumb = PanelLayout.ThumbCentre(row, slot.Control);
        Assert.Equal(value, PanelLayout.ValueAt(row, slot.Control, thumb.X), 3);
    }

    [Fact]
    public void DraggingPastEitherEndStopsAtTheStops()
    {
        var spec = Spec(Slider);
        var slot = PanelLayout.Slots(spec, PanelLayout.Width)[0];
        var row = spec.Rows[0];
        Assert.Equal(row.Min, PanelLayout.ValueAt(row, slot.Control, -9999f), 3);
        Assert.Equal(row.Max, PanelLayout.ValueAt(row, slot.Control, 9999f), 3);
    }

    // the thumb's centre must reach both stops without any of it leaving the row
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void ThumbNeverHangsOffTheRow(double value)
    {
        var spec = Spec($$"""{"type":"slider","id":"w","min":0,"max":100,"value":{{value}}}""");
        var slot = PanelLayout.Slots(spec, PanelLayout.Width)[0];
        var thumb = PanelLayout.ThumbCentre(spec.Rows[0], slot.Control);
        Assert.True(thumb.X - PanelLayout.ThumbD / 2f >= slot.Control.Left - 0.01f);
        Assert.True(thumb.X + PanelLayout.ThumbD / 2f <= slot.Control.Right + 0.01f);
    }

    [Fact]
    public void SegmentsAreEqualWidthSoTheyDoNotMoveUnderTheCursor()
    {
        var slot = PanelLayout.Slots(Spec(Segments), PanelLayout.Width)[0];
        var a = PanelLayout.SegmentRect(slot.Control, 0, 3);
        var b = PanelLayout.SegmentRect(slot.Control, 1, 3);
        var c = PanelLayout.SegmentRect(slot.Control, 2, 3);
        Assert.Equal(a.Width, b.Width, 3);
        Assert.Equal(b.Width, c.Width, 3);
        Assert.True(c.Right <= slot.Control.Right + 0.01f);
    }

    [Fact]
    public void ClickingASegmentPicksThatSegment()
    {
        var slot = PanelLayout.Slots(Spec(Segments), PanelLayout.Width)[0];
        for (int i = 0; i < 3; i++)
        {
            var r = PanelLayout.SegmentRect(slot.Control, i, 3);
            var centre = new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f);
            Assert.Equal(i, PanelLayout.SegmentAt(slot.Control, 3, centre));
        }
    }

    // the gaps belong to no button, and treating them as if they did makes one cell answer for its neighbour
    [Fact]
    public void TheGapBetweenSegmentsBelongsToNobody()
    {
        var slot = PanelLayout.Slots(Spec(Segments), PanelLayout.Width)[0];
        var first = PanelLayout.SegmentRect(slot.Control, 0, 3);
        var second = PanelLayout.SegmentRect(slot.Control, 1, 3);
        var inGap = new PointF((first.Right + second.Left) / 2f, first.Y + first.Height / 2f);
        Assert.Equal(-1, PanelLayout.SegmentAt(slot.Control, 3, inGap));
    }

    [Fact]
    public void MissingTheControlEntirelyPicksNothing()
    {
        var slot = PanelLayout.Slots(Spec(Segments), PanelLayout.Width)[0];
        Assert.Equal(-1, PanelLayout.SegmentAt(slot.Control, 3, new PointF(0, 0)));
    }

    [Fact]
    public void FractionSpansTheWholeRange()
    {
        var lo = PanelSpec.Parse(JsonNode.Parse("""{"rows":[{"type":"slider","id":"a","min":10,"max":20,"value":10}]}""") as JsonObject)!.Rows[0];
        var hi = PanelSpec.Parse(JsonNode.Parse("""{"rows":[{"type":"slider","id":"a","min":10,"max":20,"value":20}]}""") as JsonObject)!.Rows[0];
        Assert.Equal(0f, PanelLayout.Fraction(lo), 3);
        Assert.Equal(1f, PanelLayout.Fraction(hi), 3);
    }
}
