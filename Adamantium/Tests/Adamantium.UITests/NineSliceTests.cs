using System;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

// Cutting a picture into nine pieces: the corners keep their size, the edges take the axis they run along, the centre
// takes what is left. Pure rectangle arithmetic, and the part of a nine-slice that is easy to get subtly wrong - so it
// is tested apart from any device.
[TestFixture]
public class NineSliceTests
{
    // A stand-in source: nine-slice reads only its pixel size.
    private sealed class Source : ImageSource
    {
        public override double Width => 100;

        public override double Height => 100;
    }

    private static NineSliceBrush Brush(double slice = 0.25) => new()
    {
        Source = new Source(),
        Slice = new Thickness(slice)
    };

    private static TexRectItem[] Bake(NineSliceBrush brush, Rect bounds) => NineSlice.Bake(brush, bounds, 1.0, 0);

    [Test]
    public void ItCutsIntoNinePieces()
    {
        var items = Bake(Brush(), new Rect(0, 0, 300, 300));

        Assert.That(items, Has.Length.EqualTo(9));
    }

    [Test]
    public void WithoutItsCentreItCutsIntoEight()
    {
        var brush = Brush();
        brush.DrawCenter = false;

        Assert.That(Bake(brush, new Rect(0, 0, 300, 300)), Has.Length.EqualTo(8));
    }

    // The whole point: a corner is drawn at the size the source gives it, whatever the shape's size.
    [Test]
    public void TheCornersKeepTheirSizeWhateverTheShape()
    {
        var small = Bake(Brush(), new Rect(0, 0, 200, 200));
        var large = Bake(Brush(), new Rect(0, 0, 900, 400));

        Assert.That(small[0].Bounds.Z, Is.EqualTo(25f), "0.25 of a 100px source = 25px");
        Assert.That(small[0].Bounds.W, Is.EqualTo(25f));
        Assert.That(large[0].Bounds.Z, Is.EqualTo(25f), "and a shape four times as wide does not stretch them");
        Assert.That(large[0].Bounds.W, Is.EqualTo(25f));
    }

    [Test]
    public void ThePiecesTileTheWholeShapeWithNoGap()
    {
        var items = Bake(Brush(), new Rect(10, 20, 300, 200));

        var left = items.Min(i => i.Bounds.X);
        var top = items.Min(i => i.Bounds.Y);
        var right = items.Max(i => i.Bounds.X + i.Bounds.Z);
        var bottom = items.Max(i => i.Bounds.Y + i.Bounds.W);

        Assert.That(left, Is.EqualTo(10f));
        Assert.That(top, Is.EqualTo(20f));
        Assert.That(right, Is.EqualTo(310f));
        Assert.That(bottom, Is.EqualTo(220f));
    }

    // Each piece samples its own ninth: the four corners take the slice fractions, the middle takes what is between.
    // Within a texel, because the sampled range is pulled in by half a one at each end - see the inset test below.
    private const double Texel = 1.0 / 100;   // the stand-in source is 100x100

    [Test]
    public void EachPieceSamplesItsOwnPartOfTheSource()
    {
        var items = Bake(Brush(), new Rect(0, 0, 300, 300));

        var topLeft = items[0];
        Assert.That(topLeft.UvRect.X, Is.EqualTo(0f).Within(Texel));
        Assert.That(topLeft.UvRect.Z, Is.EqualTo(0.25f).Within(1.5 * Texel));

        var centre = items[4];
        Assert.That(centre.UvRect.X, Is.EqualTo(0.25f).Within(Texel));
        Assert.That(centre.UvRect.Z, Is.EqualTo(0.5f).Within(1.5 * Texel), "what is left between the two 0.25 cuts");
    }

    // A linear sampler asked for a strip's very edge blends in the texel BEYOND it - the neighbouring piece's pixels.
    // On a tiled edge that happens at every wrap and draws a thin line at each seam, so the sampled range stops at the
    // texel CENTRES instead of the texel edges.
    [Test]
    public void EachPieceStopsAtTheTexelCentres()
    {
        var items = Bake(Brush(), new Rect(0, 0, 300, 300));

        var topLeft = items[0];
        Assert.That(topLeft.UvRect.X, Is.EqualTo(Texel / 2).Within(1e-6), "half a texel in from the source's edge");
        Assert.That(topLeft.UvRect.Z, Is.EqualTo(0.25 - Texel).Within(1e-6), "and half a texel short at the far end");
    }

    // Stretch is repeat=1: one copy of the strip pulled across the gap.
    [Test]
    public void StretchedEdgesTakeOneCopyOfTheirStrip()
    {
        var items = Bake(Brush(), new Rect(0, 0, 300, 300));

        Assert.That(items.All(i => i.Tile.X == 1f && i.Tile.Y == 1f), Is.True);
    }

    // Repeat tiles the strip as many times as it fits - and ONLY along the axis the strip runs.
    [Test]
    public void RepeatedEdgesTileAlongTheirOwnAxisOnly()
    {
        var brush = Brush();
        brush.EdgeMode = NineSliceEdgeMode.Repeat;

        var items = Bake(brush, new Rect(0, 0, 300, 300));

        var top = items[1];      // the top EDGE: 250px of shape over a 50px strip of source
        Assert.That(top.Tile.X, Is.EqualTo(5f).Within(1e-4));
        Assert.That(top.Tile.Y, Is.EqualTo(1f), "a top edge does not repeat downwards");

        var corner = items[0];
        Assert.That(corner.Tile.X, Is.EqualTo(1f), "a corner never repeats - that is what makes it a corner");
        Assert.That(corner.Tile.Y, Is.EqualTo(1f));
    }

    // A shape too small for its own frame: the corners must not overlap and draw each other's pixels. Shrink the border
    // proportionally instead, the way CSS border-image does.
    // Repeat cuts the last tile mid-motif; ROUND nudges the count to a whole number so the rhythm stays even - the strip
    // is drawn a little wider or narrower instead. CSS calls this border-image-repeat: round.
    [Test]
    public void RoundFitsAWholeNumberOfTiles()
    {
        var brush = Brush();
        brush.EdgeMode = NineSliceEdgeMode.Round;

        var items = Bake(brush, new Rect(0, 0, 300, 300));

        var top = items[1];
        Assert.That(top.Tile.X, Is.EqualTo(Math.Round(top.Tile.X)).Within(1e-5), "a whole number of tiles");
        Assert.That(top.Tile.X, Is.GreaterThanOrEqualTo(1f));
    }

    [Test]
    public void RepeatLeavesThePartialTile()
    {
        var brush = Brush();
        brush.EdgeMode = NineSliceEdgeMode.Repeat;

        var items = Bake(brush, new Rect(0, 0, 310, 300));

        var top = items[1];
        Assert.That(top.Tile.X, Is.Not.EqualTo(Math.Round(top.Tile.X)).Within(1e-5));
    }

    // The MIDDLE is not an edge: tiled at the edges' pitch it becomes a grid, denser the smaller the slice. It only
    // tiles when asked outright.
    [Test]
    public void TheCentreDoesNotTileWithTheEdges()
    {
        var brush = Brush();
        brush.EdgeMode = NineSliceEdgeMode.Repeat;

        var items = Bake(brush, new Rect(0, 0, 300, 300));

        var centre = items[4];
        Assert.That(centre.Tile.X, Is.EqualTo(1f), "the centre is stretched, not tiled");
        Assert.That(centre.Tile.Y, Is.EqualTo(1f));
    }

    [Test]
    public void TheCentreTilesWhenAsked()
    {
        var brush = Brush();
        brush.EdgeMode = NineSliceEdgeMode.Repeat;
        brush.TileCenter = true;

        var items = Bake(brush, new Rect(0, 0, 300, 300));

        Assert.That(items[4].Tile.X, Is.GreaterThan(1f));
    }

    [Test]
    public void AShapeTooSmallForItsFrameShrinksItRatherThanOverlapping()
    {
        var items = Bake(Brush(), new Rect(0, 0, 30, 30));

        var leftColumn = items[0].Bounds.Z;
        var rightColumn = items[2].Bounds.Z;

        Assert.That(leftColumn + rightColumn, Is.EqualTo(30f).Within(1e-4));
        Assert.That(leftColumn, Is.EqualTo(rightColumn).Within(1e-4), "the ratio the author wrote is kept");
    }

    // Slices that together claim more than the whole source would make the two corners sample each other's pixels.
    [Test]
    public void SlicesThatClaimMoreThanTheWholeAreScaledDown()
    {
        var brush = Brush();
        brush.Slice = new Thickness(0.8, 0.1, 0.8, 0.1);

        var items = Bake(brush, new Rect(0, 0, 400, 400));

        var left = items[0].UvRect.Z;
        var right = items[2].UvRect.Z;
        Assert.That(left + right, Is.EqualTo(1f).Within(2 * Texel), "the pair fills the source, bar the texel insets");
    }

    // Nothing to draw is not a piece: a zero slice would otherwise emit a null quad per row.
    [Test]
    public void AZeroSliceContributesNoPieces()
    {
        var brush = Brush();
        brush.Slice = new Thickness(0, 0.25, 0, 0.25);   // no left/right cut - the columns collapse to one

        var items = Bake(brush, new Rect(0, 0, 300, 300));

        Assert.That(items, Has.Length.EqualTo(3), "three rows, one column each");
        Assert.That(items.All(i => i.Bounds.Z > 0 && i.Bounds.W > 0), Is.True);
    }

    [Test]
    public void ABrushWithNoSourceBakesNothing()
    {
        var brush = new NineSliceBrush();

        Assert.That(Bake(brush, new Rect(0, 0, 100, 100)), Is.Null);
    }
}
