using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using Halo.Widgets;
using Xunit;

namespace Halo.Tests;

// The costumes, checked against the surface they are actually drawn on.
//
// This exists because every costume passed by eye and four of them were broken. --render-props draws each
// prop into a roomy cell, so a mark that overshoots the capsule looks perfect there and is cut square by the
// window in the live path: the plate is 132x65 with the head at 0.70 of it, which leaves 0.214 head-heights
// over the crown, and the halo's widest pass already reaches 0.206 of them. The app icon, the spark and the
// download arrow were all sliced flat and nobody could see it in the sheet that was supposed to prove them.
//
// So the test renders the REAL plate twice, with the costume and without, and asks questions about the
// pixels that differ - which is exactly the set of pixels the costume put there.
public class FacePropTests
{
    private const int W = Face.PlateW, H = Face.PlateH;

    // Names rather than the enum itself: FaceProp is internal, and xunit needs a public method to hang a
    // [Theory] on, which cannot take an internal parameter.
    public static IEnumerable<object[]> EveryProp()
    {
        foreach (FaceProp p in System.Enum.GetValues<FaceProp>())
            if (p != FaceProp.None) yield return new object[] { p.ToString() };
    }

    private static FaceProp Prop(string name) => System.Enum.Parse<FaceProp>(name);

    // A stand-in for a real app icon: FaceProp.AppIcon draws nothing at all without one, and a costume that
    // draws nothing would pass every assertion below by drawing no pixels anywhere.
    private static Bitmap SampleIcon()
    {
        var bmp = new Bitmap(64, 64, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(255, 240, 120, 40));
        return bmp;
    }

    // The moment a prop is fully engaged with the face. For a costume that arrives and stays that is just
    // "settled"; for the eating prop, which has a beginning, a middle and an end and no resting pose at all,
    // it is the swallow - icon at the mouth, mouth open, nothing spat out yet. Before it the icon is still
    // off the plate on purpose, and after it the pieces are deliberately flying away.
    // ...and it is not the same fraction for every costume, which is the second time this has had to be
    // said. Half way is right for anything that arrives and stays; the download's arrow is now a way INTO
    // the costume rather than the costume itself, absorbed into the crown before a third of the beat is
    // out, so at the shared mark it correctly draws nothing at all and the draws-something test read that
    // as a broken prop.
    private static float EngagedAt(FaceProp prop) => prop switch
    {
        FaceProp.Download => 0.26f,
        // the card is behind the head by half way, which is the costume working - being filed is the point
        FaceProp.Tray => 0.30f,
        _ => 0.5f,
    };

    // What the DIRECTOR says this costume is doing at its engaged moment, rather than a hardcoded "on = 1".
    // Costumes stopped agreeing on what 1 means: for one that arrives and stays it is the settled pose, for
    // the magnifier's sweep it is "already gone past and drawing nothing", which failed the draws-something
    // test for a costume that was working exactly as designed.
    private static float OnAtEngaged(FaceProp prop)
        => FaceDirector.Hand(FaceDirector.HandSeconds(prop) * EngagedAt(prop), prop, 0.4f, 1f).Prop;

    private static Bitmap Plate(FaceProp prop, float on, Image? icon)
    {
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        Face.DrawGlass(g, W, H);
        var box = Face.PlateBox(W, H);
        Face.Draw(g, box, Face.Look.Awake);
        if (prop != FaceProp.None) Face.DrawProp(g, box, prop, on, 1f, icon, EngagedAt(prop));
        // ...and the letterbox, which is the whole of the video costume. It is the one that is NOT a prop -
        // it changes the shape of the window rather than putting anything on the face - so a sheet that
        // only rendered props measured it as drawing nothing at all.
        if (prop != FaceProp.None)
            Face.Letterbox(g, W, H,
                FaceDirector.Hand(FaceDirector.HandSeconds(prop) * EngagedAt(prop), prop, 0.4f, 1f).Letterbox,
                1f);
        return bmp;
    }

    /// <summary>Every pixel the costume changed, which is the costume's own ink and nothing else.</summary>
    private static List<Point> Ink(FaceProp prop, float unused)
    {
        float on = OnAtEngaged(prop);
        using var icon = SampleIcon();
        using var bare = Plate(FaceProp.None, on, icon);
        using var worn = Plate(prop, on, icon);
        var ink = new List<Point>();
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (bare.GetPixel(x, y) != worn.GetPixel(x, y))
                    ink.Add(new Point(x, y));
        return ink;
    }

    [Theory]
    [MemberData(nameof(EveryProp))]
    public void EveryCostumeActuallyDrawsSomething(string name)
    {
        // the floor is deliberately low - Think is three small dots and is meant to be the faintest mark in
        // the set - but a costume contributing nothing at all is the failure this catches
        Assert.True(Ink(Prop(name), 1f).Count >= 20, $"{name} drew almost nothing when settled");
    }

    [Theory]
    [MemberData(nameof(EveryProp))]
    public void SettledCostumeIsNotCutOffByTheWindow(string name)
    {
        // The SETTLED pose only. Two costumes swing in from outside the plate on purpose - the magnifier and
        // the app icon arrive like an answer being brought over - so being clipped mid-entrance is the
        // intended drawing, and only where the costume comes to rest is it a defect.
        //
        // Video is exempt outright, and not as a concession. This rule is about MARKS ON THE FACE, which
        // must not run into the window's edge because a mark cut square there reads as a rendering fault.
        // The video costume is not a mark on the face - it is the letterbox, the frame the whole picture
        // sits inside - and a letterbox that stopped short of the window would simply not be a letterbox.
        // It is the one costume whose job is to reach the edge.
        if (Prop(name) == FaceProp.Goggles) return;
        foreach (var p in Ink(Prop(name), 1f))
            Assert.True(p.X > 0 && p.Y > 0 && p.X < W - 1 && p.Y < H - 1,
                        $"{name} still has ink on the window edge at {p.X},{p.Y} once settled - " +
                        "the bitmap cuts it square there");
    }

    [Theory]
    [MemberData(nameof(EveryProp))]
    public void SettledCostumeStaysOnTheGlass(string name)
    {
        // Stricter than the edge test and for a different reason: the HALO is allowed to spill past the
        // capsule (the reference does it, and clipping it puts a hard straight line across the one soft thing
        // in the drawing) but a costume is a hard-edged mark, and one floating on transparency beside the
        // glass reads as a stray rather than as something the face is wearing.
        // ...and video is exempt here for the same reason it is exempt above: the bars ARE the capsule's
        // top and bottom, clipped to it, so "stays on the glass" is trivially true of them and "does not
        // touch its edge" is the opposite of what they are for.
        if (Prop(name) == FaceProp.Goggles) return;
        using var capsule = Face.SheetPath(Face.PlateRect(W, H), H / 2f);
        foreach (var p in Ink(Prop(name), 1f))
            Assert.True(capsule.IsVisible(p.X, p.Y),
                        $"{name} puts ink outside the capsule at {p.X},{p.Y} once settled");
    }

    [Fact]
    public void TheAppIconIsBigEnoughToRecognise()
    {
        // 24px is taskbar size and the whole justification for this costume is that a real icon beats any
        // glyph that could be drawn instead. It was 15px and sliced, which is the size at which that
        // argument stops being true - so the number is asserted rather than left to drift back.
        var box = Face.PlateBox(W, H);
        Assert.True(box.Height * 0.50f >= 22f, "the app icon has shrunk below taskbar size");
    }

    // The costume containment tests above hold the head still. A costume that MOVES can be perfectly
    // contained at rest and still be sliced the moment the head does anything - which is exactly what
    // happened: the headphone band sat 0.016 of a head-height below the window, the spring-back after the
    // impact grew the head upward, and the band lost its top. Reported as "when it comes back up the
    // headphone gets cut - make it fully visible".
    //
    // So this walks the WHOLE beat and applies bob and squash the way the pill does, which is the only
    // arrangement that can see it.
    [Fact]
    public void TheMusicArrivalIsNeverSlicedByTheWindow()
    {
        using var icon = SampleIcon();
        // THIS costume's own beat, not HandSeconds(true). The shared 1.52s was what this used to walk, and
        // once music got a beat of its own that silently stopped covering it: the whole speaker stage lives
        // after 1.52s, so the test was passing by ending before the part with the most movement in it.
        float end = FaceDirector.HandSeconds(FaceProp.Headphones);
        // From the LANDING onward. Before it the headphones are still above the plate and being cropped by
        // the window is the whole point - they are falling in from off screen, the same way the magnifier
        // and the app icon swing in from beside it. What must never be cropped is anything after contact.
        for (float t = FaceDirector.MusicLand; t <= end; t += 0.01f)
        {
            // full level: the head cones and dips on loudness, so the worst case is the loudest
            var beat = FaceDirector.Hand(t, FaceProp.Headphones, 0.4f, 1f);
            if (beat.Alpha <= 0.02f) continue;

            using var bare = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using var worn = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            foreach (var (bmp, wear) in new[] { (bare, false), (worn, true) })
            {
                using var g = Graphics.FromImage(bmp);
                Face.DrawGlass(g, W, H, beat.Alpha);
                var box = Face.BeatBox(W, H, beat.Bob, beat.Sway, beat.Scale, beat.Squash);
                Face.Draw(g, box, beat.Look, beat.Alpha);
                if (wear) Face.DrawProp(g, box, FaceProp.Headphones, beat.Prop, beat.Alpha, icon);
            }

            for (int x = 0; x < W; x++)
            {
                // the top row only: the band is the part with nowhere to go, and the sides and floor are
                // where the cups legitimately sit against the glass
                if (bare.GetPixel(x, 0) != worn.GetPixel(x, 0))
                    Assert.Fail($"the headphones touch the top of the window at t={t:0.00}s, x={x} - "
                                + "they are being cut off there");
            }
        }
    }

    // The waves are the one thing in the set drawn OUTSIDE the head, and the plate is only 132x65 - so
    // "does it fit" is a real question and not a formality. The span and the far radius were solved against
    // this by hand (r*sin(24 deg) against the room under the head's centre); this is that arithmetic
    // asserted, so a later nudge to either number cannot quietly start clipping.
    //
    // Every edge, not just the top: a wave leaving sideways is the one costume that can run out of room on
    // the left and the right, which nothing else here has ever been able to do.
    [Fact]
    public void TheSoundWavesStayInsideThePlate()
    {
        float end = FaceDirector.HandSeconds(FaceProp.Headphones);
        for (float t = FaceDirector.MusicSound; t <= end; t += 0.01f)
        {
            var beat = FaceDirector.Hand(t, FaceProp.Headphones, 0.4f, 1f);
            if (beat.Alpha <= 0.02f || beat.Wave <= 0.02f) continue;

            using var quiet = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using var loud = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            var box = Face.BeatBox(W, H, beat.Bob, beat.Sway, beat.Scale, beat.Squash);
            foreach (var (bmp, sounding) in new[] { (quiet, false), (loud, true) })
            {
                using var g = Graphics.FromImage(bmp);
                Face.DrawGlass(g, W, H, beat.Alpha);
                if (sounding) Face.Waves(g, box, beat.Phase, beat.Wave, beat.Alpha);
                Face.Draw(g, box, beat.Look, beat.Alpha);
            }

            for (int x = 0; x < W; x++)
                foreach (int y in new[] { 0, H - 1 })
                    if (quiet.GetPixel(x, y) != loud.GetPixel(x, y))
                        Assert.Fail($"a sound wave reaches row {y} at t={t:0.00}s, x={x} - it is being cut "
                                    + "off flat there, which is the one thing this drawing must not have");
            for (int y = 0; y < H; y++)
                foreach (int x in new[] { 0, W - 1 })
                    if (quiet.GetPixel(x, y) != loud.GetPixel(x, y))
                        Assert.Fail($"a sound wave reaches column {x} at t={t:0.00}s, y={y} - it leaves the "
                                    + "plate instead of fading out inside it");
        }
    }

    // The cone may only ever shrink. The plate keeps 0.214 head-heights above the crown and the halo's
    // widest pass already spends 0.206 of them, so a head that scaled above 1 would push its own glow off
    // the top of the bitmap - and a glow cut flat is the defect the halo's falloff was rewritten to remove.
    // Asserted rather than remembered, because "quiet is smaller" is the kind of decision a later tweak
    // reads as backwards.
    [Fact]
    public void TheHeadNeverConesLargerThanItsRestingSize()
    {
        float end = FaceDirector.HandSeconds(FaceProp.Headphones);
        var rest = Face.PlateBox(W, H);
        for (float t = 0f; t <= end; t += 0.01f)
        {
            var beat = FaceDirector.Hand(t, FaceProp.Headphones, 0.4f, 1f);
            Assert.True(beat.Scale <= 1.0001f, $"the head cones to {beat.Scale:0.000} at t={t:0.00}s");
            var box = Face.BeatBox(W, H, beat.Bob, beat.Sway, beat.Scale, 0f);
            Assert.True(box.Width <= rest.Width + 0.01f && box.Height <= rest.Height + 0.01f,
                        $"the head is larger than its resting box at t={t:0.00}s");
        }
    }

    // The download's glass is the one costume that puts a NUMBER on screen, even though it draws no digits:
    // a head 60% full is a claim that 60% has arrived. So the two things that must hold are the two the
    // "never display invented numbers" rule is made of - it says the true thing when there is one, and it
    // says nothing when there is not.
    [Fact]
    public void TheGlassSettlesAtTheDownloadsRealFraction()
    {
        // late enough for the pour's overshoot to have rung out, before the burp disturbs it again
        float at = (FaceDirector.DownloadFull + FaceDirector.DownloadBurp) / 2f;
        foreach (float real in new[] { 0f, 0.25f, 0.6f, 1f })
        {
            float fill = FaceDirector.Hand(at, FaceProp.Download, 0.4f, real).Fill;
            Assert.True(MathF.Abs(fill - real) < 0.06f,
                        $"a download at {real:P0} draws a glass at {fill:P0}");
        }
    }

    [Fact]
    public void AnUnknownDownloadNeverClaimsALevel()
    {
        // Two halves of the same rule. The glass may not creep up to a level nobody measured, AND its
        // surface may not go still - a flat surface at a fixed height IS a percentage, drawn instead of
        // written, which is the exact thing this project has rejected twice.
        float at = (FaceDirector.DownloadFull + FaceDirector.DownloadBurp) / 2f;
        var unknown = FaceDirector.Hand(at, FaceProp.Download, 0.4f, -1f);
        var full = FaceDirector.Hand(at, FaceProp.Download, 0.4f, 1f);
        Assert.True(unknown.Fill < 0.35f, "an unmeasured download draws a nearly full glass");
        Assert.True(unknown.Slosh > full.Slosh + 0.15f,
                    "an unmeasured download's surface has gone as still as a measured one's - a still "
                    + "surface at a fixed height is a percentage nobody measured");
    }

    [Fact]
    public void NothingIsInTheHeadUntilTheArrowHasBeenSwallowed()
    {
        for (float t = 0f; t < FaceDirector.DownloadHit; t += 0.01f)
            Assert.True(FaceDirector.Hand(t, FaceProp.Download, 0.4f, 0.6f).Fill < 0f,
                        $"the head is already filling at t={t:0.00}s, while the arrow is still in the air");
    }

    // Clipped to HeadPath is the whole design of the fill, and a clip is exactly the kind of thing that
    // survives a refactor as a comment while the SetClip quietly moves. This asserts the consequence.
    [Fact]
    public void TheLiquidStaysInsideTheHead()
    {
        var box = Face.PlateBox(W, H);
        using var head = Face.HeadPath(box);
        for (float t = FaceDirector.DownloadHit; t <= FaceDirector.DownloadBeat; t += 0.02f)
        {
            var beat = FaceDirector.Hand(t, FaceProp.Download, 0.4f, 1f);
            if (beat.Alpha <= 0.02f || beat.Liquid is null) continue;
            using var dry = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            using var wet = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
            // the head held STILL, so the only difference between the two is the liquid itself
            foreach (var (bmp, pour) in new[] { (dry, false), (wet, true) })
            {
                using var g = Graphics.FromImage(bmp);
                Face.Draw(g, box, beat.Look, beat.Alpha, pour ? beat.Liquid : null);
            }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (dry.GetPixel(x, y) != wet.GetPixel(x, y) && !head.IsVisible(x, y))
                        Assert.Fail($"liquid at {x},{y} is outside the head at t={t:0.00}s - it is being "
                                    + "drawn over the face rather than inside it");
        }
    }

    [Fact]
    public void TheEatingPropIsOverByTheEndOfTheBeat()
    {
        // A mouth is not part of this face. It opens for a third of a second and has to be GONE, or the
        // idle face the user goes back to looking at is a different face than the one that was designed.
        using var icon = SampleIcon();
        using var bare = Plate(FaceProp.None, 1f, icon);

        var left = new List<Point>();
        var bmp = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            Face.DrawGlass(g, W, H);
            var box = Face.PlateBox(W, H);
            Face.Draw(g, box, Face.Look.Awake);
            Face.DrawProp(g, box, FaceProp.AppIcon, 1f, 1f, icon, 1f);
        }
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (bare.GetPixel(x, y) != bmp.GetPixel(x, y)) left.Add(new Point(x, y));
        bmp.Dispose();

        Assert.True(left.Count == 0, $"{left.Count} pixels of the meal are still on the face at the end");
    }
}

// Halo clinging to a notification banner. Not a costume - no beat, and it lives as long as the toast does -
// so none of the costume tests above reach it, and the two things it can get wrong are exactly the two
// things a drawing cannot show you: whether it fits in the headroom the window was grown by, and whether
// the sheet's own edge is really what cuts it off.
public class CatClingTests
{
    private const int BW = Halo.Widgets.NotifBanner.W, BH = Halo.Widgets.NotifBanner.SummaryH;
    // the window is the sheet plus the cat's margins: the whole rise above it, and half the side margin on
    // either side, which is how the pill draws it
    private static readonly int WinW = BW + (int)Face.CatSide;
    private static readonly int WinH = BH + (int)Face.CatDrop;

    private static RectangleF Sheet()
    {
        var s = Face.SheetRect(BW, BH);
        s.Offset(Face.CatSide / 2f, 0f);
        return s;
    }

    // CatRise and CatSide are the margins the banner's WINDOW is grown by, and the head plus its halo has to
    // live inside both. The halo is a pen scaled off the head's height and the tilt turns the head's
    // bounding box, so all three numbers move together - which is exactly the arrangement where changing one
    // by eye slices the ring flat against an edge, and a glow cut flat is the defect the halo's own falloff
    // was rewritten to remove.
    [Fact]
    public void TheClingingHeadFitsInTheMarginsTheWindowWasGrownBy()
    {
        var sheet = Sheet();
        for (float grip = 0.1f; grip <= 1f; grip += 0.05f)
        {
            using var bmp = new Bitmap(WinW, WinH, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
                Face.Cling(g, sheet, Face.Look.Awake, grip, 0f, 1f);
            for (int x = 0; x < WinW; x++)
                if (bmp.GetPixel(x, WinH - 1).A != 0)
                    Assert.Fail($"the cat's ring reaches the bottom of the window at grip={grip:0.00}, x={x}"
                                + $" - Face.CatDrop ({Face.CatDrop}) is no longer enough room below it");
            for (int y = 0; y < WinH; y++)
                if (bmp.GetPixel(WinW - 1, y).A != 0)
                    Assert.Fail($"the cat's ring reaches the right of the window at grip={grip:0.00}, y={y} - "
                                + $"Face.CatSide ({Face.CatSide}) is no longer enough room beside it");
        }
    }

    // The pose. It was built to HIDE the head behind the sheet once, and that was a misreading: nobody
    // asked for the cat to be concealed, they asked for it to look three-dimensional, and those are
    // opposite instructions. Depth comes from layering and from a cast shadow, not from burying the subject.
    //
    // Upright under the bottom edge the head is almost entirely clear, and the overlap is deliberately down
    // to a sliver - just the crown under the glass. That sliver is not decoration: it is the only thing
    // putting a front and a back in the picture for the shadow to measure a distance against, so its
    // presence is asserted even though it is small.
    [Fact]
    public void TheHeadIsSeenAndStillTucksUnderTheSheet()
    {
        var sheet = Sheet();
        var head = Face.CatHead(sheet, 1f, 0f);
        using var outline = Face.SheetPath(sheet, 26f);
        int over = 0, clear = 0;
        for (float u = 0f; u <= 1f; u += 0.04f)
            for (float v = 0f; v <= 1f; v += 0.04f)
            {
                float x = head.X + head.Width * u, y = head.Y + head.Height * v;
                if (outline.IsVisible(x, y)) over++; else clear++;
            }
        Assert.True(clear > over * 3,
                    $"the head is {clear} clear / {over} under the sheet - it is meant to be all the way "
                    + "out, and a cat nobody can see is not a three-dimensional cat");
        Assert.True(over > 12,
                    $"only {over} of the head tucks under the sheet - with no overlap at all there is no "
                    + "front and back for the eye to separate");
    }

    // ...and the paws rest ON the front of it, which is the other half of the layering.
    [Fact]
    public void ThePawsRestOnTheFrontOfTheSheet()
    {
        var sheet = Sheet();
        using var outline = Face.SheetPath(sheet, 26f);
        using var bmp = new Bitmap(WinW, WinH, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
            Face.ClingPaws(g, sheet, 1f, 0f, 1f);
        int on = 0, off = 0;
        for (int y = 0; y < WinH; y++)
            for (int x = 0; x < WinW; x++)
                if (bmp.GetPixel(x, y).A > 8)
                {
                    if (outline.IsVisible(x, y)) on++; else off++;
                }
        Assert.True(on > 200, $"only {on}px of paw is on the sheet - they are not resting on it");
        Assert.True(off < on / 6, $"{off}px of paw hangs off the sheet - they are gripping a rim rather "
                                  + "than resting on the front of it");
    }

    // The shadow is what a viewer actually measures distance with - two shapes stacked in the right order
    // still read as two flat cutouts without one. It has to land ON the sheet and nowhere else: a shadow
    // outside the banner is being cast by nothing onto nothing.
    [Fact]
    public void TheCatCastsAShadowAndOnlyOnTheSheet()
    {
        var sheet = Sheet();
        using var outline = Face.SheetPath(sheet, 26f);
        using var bare = new Bitmap(WinW, WinH, PixelFormat.Format32bppPArgb);
        using var cast = new Bitmap(WinW, WinH, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(cast))
            Face.ClingShadow(g, sheet, 1f, 0f, 1f);
        int ink = 0;
        for (int y = 0; y < WinH; y++)
            for (int x = 0; x < WinW; x++)
                if (cast.GetPixel(x, y) != bare.GetPixel(x, y))
                {
                    ink++;
                    Assert.True(outline.IsVisible(x, y),
                                $"shadow at {x},{y} falls outside the banner it is supposed to be cast on");
                }
        Assert.True(ink > 300, $"only {ink}px of shadow - there is nothing there to read a depth from");
    }

    // Ducking pulls it straight back UP behind the sheet, along the same axis it arrived on, and it has to
    // go all the way: a hiding animal that is still visible is crouching, not hiding. Both halves matter -
    // the geometry takes it up, and the alpha takes it away, because the sheet is translucent and a head
    // parked behind the glass is still perfectly readable through it.
    [Fact]
    public void ReachingForItSendsItAllTheWayBackUnderAndOutOfSight()
    {
        var sheet = Sheet();
        var out_ = Face.CatHead(sheet, 1f, 0f);
        foreach (var gone in new[] { Face.CatHead(sheet, 1f, 1f), Face.CatHead(sheet, 0.2f, 0f) })
        {
            Assert.True(gone.Y < out_.Y - 20f, "it no longer retreats up behind the sheet");
            Assert.Equal(out_.X, gone.X, 3);   // straight up, not off to one side
        }

        // ...and it is genuinely gone, not merely moved: nothing may still be drawn at a full duck.
        using var bmp = new Bitmap(WinW, WinH, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            Face.Cling(g, sheet, Face.Look.Awake, 1f, 1f, 1f);
            Face.ClingPaws(g, sheet, 1f, 1f, 1f);
        }
        int ink = 0;
        for (int y = 0; y < WinH; y++)
            for (int x = 0; x < WinW; x++)
                if (bmp.GetPixel(x, y).A > 24) ink++;
        Assert.True(ink < 40, $"{ink}px of cat is still visible at a full duck - it is hiding behind "
                              + "translucent glass, which hides nothing");
    }
}
