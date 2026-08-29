using System.Drawing;
using System.Linq;
using System.Text.Json.Nodes;
using Halo.Panels;
using Xunit;

namespace Halo.Tests;

public class PanelHitTests
{
    private const int W = PanelLayout.Width;

    private static PanelSpec Spec(params string[] rows)
        => PanelSpec.Parse(JsonNode.Parse($$"""{"title":"T","rows":[{{string.Join(",", rows)}}]}""") as JsonObject)!;

    private const string Text = """{"type":"text","label":"Status","text":"Building"}""";
    private const string Slider = """{"type":"slider","id":"w","label":"Width","min":0,"max":100,"value":50}""";
    private const string Toggle = """{"type":"toggle","id":"g","label":"Glass","value":true}""";
    private const string Segments = """{"type":"buttons","id":"h","options":["a","b","c"],"value":0}""";
    private const string Meter = """{"type":"meter","label":"Used","value":0.5}""";

    private static PointF Centre(RectangleF r) => new(r.X + r.Width / 2f, r.Y + r.Height / 2f);

    [Fact]
    public void RowsWithNothingToPressGetNoTarget()
    {
        var targets = PanelHit.Targets(Spec(Text, Meter, Slider), W);
        var target = Assert.Single(targets);
        Assert.Equal(PanelRowKind.Slider, target.Kind);
    }

    // reading a toggle row with the mouse resting on its label used to flip it
    [Fact]
    public void ATogglesTargetIsTheSwitchNotTheWholeRow()
    {
        var spec = Spec(Toggle);
        var target = PanelHit.Targets(spec, W).Single();
        var slot = PanelLayout.Slots(spec, W)[0];
        Assert.True(target.Area.Width < slot.Row.Width / 2f);
        Assert.Null(PanelHit.Press(spec, W, new PointF(slot.Row.X + 20f, Centre(slot.Row).Y)));
    }

    [Fact]
    public void PressingTheSwitchFlipsIt()
    {
        var spec = Spec(Toggle);
        var target = PanelHit.Targets(spec, W).Single();
        var hit = PanelHit.Press(spec, W, Centre(target.Area));
        Assert.NotNull(hit);
        Assert.Equal("g", hit!.Value.Id);
        Assert.Equal(0, hit.Value.Value);          // it was on
    }

    [Fact]
    public void PressingASliderAtTheFarEndGivesItsMaximum()
    {
        var spec = Spec(Slider);
        var slot = PanelLayout.Slots(spec, W)[0];
        var hit = PanelHit.Press(spec, W, new PointF(slot.Control.Right - 1f, Centre(slot.Control).Y));
        Assert.Equal(100, hit!.Value.Value, 1);
    }

    [Fact]
    public void PressingASegmentPicksIt()
    {
        var spec = Spec(Segments);
        var slot = PanelLayout.Slots(spec, W)[0];
        var cell = PanelLayout.SegmentRect(slot.Control, 2, 3);
        var hit = PanelHit.Press(spec, W, Centre(cell));
        Assert.Equal(2, hit!.Value.Value);
    }

    // the gap between two segments belongs to neither, and answering with the nearer one makes the edge
    // of a cell press its neighbour
    [Fact]
    public void PressingBetweenTwoSegmentsPressesNeither()
    {
        var spec = Spec(Segments);
        var slot = PanelLayout.Slots(spec, W)[0];
        var first = PanelLayout.SegmentRect(slot.Control, 0, 3);
        var second = PanelLayout.SegmentRect(slot.Control, 1, 3);
        Assert.Null(PanelHit.Press(spec, W,
            new PointF((first.Right + second.Left) / 2f, Centre(first).Y)));
    }

    [Fact]
    public void PressingNowhereIsNothing()
        => Assert.Null(PanelHit.Press(Spec(Slider, Toggle), W, new PointF(2f, 2f)));

    // a grabbed slider has to keep following the cursor after it leaves the control, or dragging past the
    // end feels like the thumb was dropped rather than stopped
    [Fact]
    public void ADraggedSliderKeepsThePointerOutsideItsOwnControl()
    {
        var spec = Spec(Slider);
        var hit = PanelHit.Press(spec, W, new PointF(-500f, -500f), dragging: true, heldRow: 0);
        Assert.NotNull(hit);
        Assert.Equal(0, hit!.Value.Value, 1);   // clamped to the low stop, not dropped
    }

    // ...but a toggle must not flip again on every frame the button is held down
    [Fact]
    public void HoldingTheButtonDownDoesNotKeepFlippingAToggle()
    {
        var spec = Spec(Toggle);
        var target = PanelHit.Targets(spec, W).Single();
        Assert.Null(PanelHit.Press(spec, W, Centre(target.Area), dragging: true, heldRow: 0));
    }

    [Fact]
    public void HoverFindsTheRowUnderThePointerAndNothingOutside()
    {
        var spec = Spec(Text, Slider, Toggle);
        var slots = PanelLayout.Slots(spec, W);
        Assert.Equal(1, PanelHit.RowAt(spec, W, Centre(slots[1].Row)));
        Assert.Equal(-1, PanelHit.RowAt(spec, W, new PointF(2f, 2f)));
    }

    [Fact]
    public void WithReplacesOneValueAndLeavesTheOldSpecAlone()
    {
        var spec = Spec(Slider, Toggle);
        var next = PanelHit.With(spec, 0, 77);
        Assert.Equal(50, spec.Rows[0].Value);     // the published one is untouched
        Assert.Equal(77, next.Rows[0].Value);
        Assert.Equal(spec.Rows[1].Value, next.Rows[1].Value);
    }

    [Theory]
    [InlineData(-40, 0)]
    [InlineData(4000, 100)]
    public void WithClampsIntoTheRowsOwnRange(double given, double expected)
        => Assert.Equal(expected, PanelHit.With(Spec(Slider), 0, given).Rows[0].Value);

    [Fact]
    public void WithClampsASegmentToWhatExists()
        => Assert.Equal(2, PanelHit.With(Spec(Segments), 0, 99).Rows[0].Value);

    [Fact]
    public void WithIgnoresARowThatIsNotThere()
    {
        var spec = Spec(Slider);
        Assert.Same(spec, PanelHit.With(spec, 9, 1));
    }

    // the round trip a real drag makes: press, read back, and land on the same number
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void PressAndRedrawAgreeOnWhereTheThumbIs(double fraction)
    {
        var spec = Spec(Slider);
        var slot = PanelLayout.Slots(spec, W)[0];
        var track = PanelLayout.Track(slot.Control);
        float x = track.X + track.Width * (float)fraction;

        var hit = PanelHit.Press(spec, W, new PointF(x, Centre(slot.Control).Y));
        var moved = PanelHit.With(spec, 0, hit!.Value.Value);
        Assert.Equal(x, PanelLayout.ThumbCentre(moved.Rows[0], slot.Control).X, 1);
    }
}
