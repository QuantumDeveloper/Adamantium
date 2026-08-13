using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

// The four mechanisms of a TileBrush, as rectangle arithmetic: viewbox picks the slice of the source, viewport places
// and sizes one tile, stretch + alignment fit the content in it, tile mode repeats it. Tested without a device.
[TestFixture]
public class ImageTilingTests
{
    // 64x32 - deliberately NOT square, so an aspect-preserving mode has something to preserve.
    private sealed class Source : BitmapSource
    {
        public Source() : base(64, 32, 1, 1, Adamantium.Imaging.SurfaceFormat.B8G8R8A8.UNorm, new byte[64 * 32 * 4]) { }
    }

    private static ImageBrush Brush() => new(new Source());

    private static TileLayout Layout(ImageBrush brush, Rect bounds) => ImageTiling.Layout(brush, bounds, 1.0, 1.0);

    [Test]
    public void FillTakesTheWholeShapeAndTheWholeSource()
    {
        var layout = Layout(Brush(), new Rect(0, 0, 300, 200));

        Assert.That(layout.UvRect, Is.EqualTo(new Vector4F(0, 0, 1, 1)));
        Assert.That(layout.Drawn, Is.EqualTo(new Vector4F(0, 0, 1, 1)), "the content fills its tile");
        Assert.That(layout.Tile.X, Is.EqualTo(1f), "one tile across");
        Assert.That(layout.Tile.Y, Is.EqualTo(1f));
        Assert.That(layout.Repeats, Is.False);
    }

    // --- Viewport: where one tile sits, and therefore how big it is -------------------------------------------------

    // An ABSOLUTE viewport states the tile in logical px, so the count is however many fit - fractional, so the last
    // one is cut by the shape's edge, as a tiled surface must be.
    [Test]
    public void AnAbsoluteViewportSizesTheTileInPixels()
    {
        var brush = Brush();
        brush.TileMode = TileMode.Tile;
        brush.ViewportUnits = BrushMappingMode.Absolute;
        brush.Viewport = new Rect(0, 0, 32, 32);

        var layout = Layout(brush, new Rect(0, 0, 320, 128));

        Assert.That(layout.Tile.X, Is.EqualTo(10f).Within(1e-4), "320 / 32");
        Assert.That(layout.Tile.Y, Is.EqualTo(4f).Within(1e-4), "128 / 32");
        Assert.That(layout.Repeats, Is.True);
    }

    // A RELATIVE viewport is a fraction of the shape, so the tile COUNT is fixed however the shape resizes - the
    // difference from absolute that makes a brush reusable.
    [Test]
    public void ARelativeViewportKeepsTheTileCountAcrossSizes()
    {
        var brush = Brush();
        brush.TileMode = TileMode.Tile;
        brush.Viewport = new Rect(0, 0, 0.25, 0.5);

        var small = Layout(brush, new Rect(0, 0, 100, 100));
        var large = Layout(brush, new Rect(0, 0, 900, 400));

        Assert.That(small.Tile.X, Is.EqualTo(4f).Within(1e-4));
        Assert.That(small.Tile.Y, Is.EqualTo(2f).Within(1e-4));
        Assert.That(large.Tile.X, Is.EqualTo(4f).Within(1e-4), "a resize must not change the count");
        Assert.That(large.Tile.Y, Is.EqualTo(2f).Within(1e-4));
    }

    // The viewport's ORIGIN shifts the whole grid, in tiles - what lets a tiled brush be offset without moving the shape.
    [Test]
    public void TheViewportOriginShiftsTheTileGrid()
    {
        var brush = Brush();
        brush.TileMode = TileMode.Tile;
        brush.ViewportUnits = BrushMappingMode.Absolute;
        brush.Viewport = new Rect(16, 8, 32, 32);

        var layout = Layout(brush, new Rect(0, 0, 320, 128));

        Assert.That(layout.Tile.Z, Is.EqualTo(0.5f).Within(1e-4), "16px into a 32px tile");
        Assert.That(layout.Tile.W, Is.EqualTo(0.25f).Within(1e-4), "8px into a 32px tile");
    }

    // --- Viewbox: which part of the source a tile shows -------------------------------------------------------------

    [Test]
    public void ARelativeViewboxIsTheSampledRectAsItStands()
    {
        var brush = Brush();
        brush.Viewbox = new Rect(0.25, 0, 0.5, 1);

        var layout = Layout(brush, new Rect(0, 0, 200, 100));

        Assert.That(layout.UvRect, Is.EqualTo(new Vector4F(0.25f, 0f, 0.5f, 1f)));
    }

    // An ABSOLUTE viewbox is stated in the source's own units - texels for a picture - so a sprite is cut out by the
    // coordinates the artist reads off the sheet, and the shader still only ever sees 0..1.
    [Test]
    public void AnAbsoluteViewboxIsStatedInTheSourcesOwnUnits()
    {
        var brush = Brush();
        brush.ViewboxUnits = BrushMappingMode.Absolute;
        brush.Viewbox = new Rect(16, 8, 32, 16);   // the source is 64x32

        var layout = Layout(brush, new Rect(0, 0, 200, 100));

        Assert.That(layout.UvRect.X, Is.EqualTo(0.25f).Within(1e-4));
        Assert.That(layout.UvRect.Y, Is.EqualTo(0.25f).Within(1e-4));
        Assert.That(layout.UvRect.Z, Is.EqualTo(0.5f).Within(1e-4));
        Assert.That(layout.UvRect.W, Is.EqualTo(0.5f).Within(1e-4));
    }

    // --- Stretch, measured against the TILE (not the shape) ---------------------------------------------------------

    // Uniform keeps the aspect, so the content occupies part of its tile and the rest stays clear.
    [Test]
    public void UniformFitsTheContentInsideItsTile()
    {
        var brush = Brush();
        brush.Stretch = Stretch.Uniform;

        var layout = Layout(brush, new Rect(0, 0, 200, 200));   // square shape, 2:1 source

        Assert.That(layout.UvRect, Is.EqualTo(new Vector4F(0, 0, 1, 1)), "nothing is cropped");
        Assert.That(layout.Drawn.Z, Is.EqualTo(1f).Within(1e-4), "the wide axis fills the tile");
        Assert.That(layout.Drawn.W, Is.EqualTo(0.5f).Within(1e-4), "the other keeps the 2:1 ratio");
        Assert.That(layout.Drawn.Y, Is.EqualTo(0.25f).Within(1e-4), "centred in what is left");
    }

    // A TILED Uniform brush letterboxes EVERY copy, not the lot: the fit is measured against one tile.
    [Test]
    public void UniformLetterboxesEveryTileNotTheWholeShape()
    {
        var brush = Brush();
        brush.Stretch = Stretch.Uniform;
        brush.TileMode = TileMode.Tile;
        brush.ViewportUnits = BrushMappingMode.Absolute;
        brush.Viewport = new Rect(0, 0, 100, 100);

        var layout = Layout(brush, new Rect(0, 0, 400, 400));

        Assert.That(layout.Tile.X, Is.EqualTo(4f).Within(1e-4));
        Assert.That(layout.Drawn.W, Is.EqualTo(0.5f).Within(1e-4), "each 100x100 tile holds the 2:1 source at half height");
        Assert.That(layout.Drawn.Y, Is.EqualTo(0.25f).Within(1e-4));
    }

    // UniformToFill does the opposite: the tile is filled edge to edge and the SOURCE is cropped.
    [Test]
    public void UniformToFillCropsTheSourceRatherThanTheTile()
    {
        var brush = Brush();
        brush.Stretch = Stretch.UniformToFill;

        var layout = Layout(brush, new Rect(0, 0, 200, 200));

        Assert.That(layout.Drawn, Is.EqualTo(new Vector4F(0, 0, 1, 1)), "the tile is filled edge to edge");
        Assert.That(layout.UvRect.Z, Is.EqualTo(0.5f).Within(1e-4), "half the source's width is sampled");
        Assert.That(layout.UvRect.X, Is.EqualTo(0.25f).Within(1e-4), "and it is the CENTRED half");
        Assert.That(layout.UvRect.W, Is.EqualTo(1f), "its full height");
    }

    [Test]
    public void NoneDrawsTheSourceAtItsOwnSize()
    {
        var brush = Brush();
        brush.Stretch = Stretch.None;

        var layout = Layout(brush, new Rect(0, 0, 200, 200));

        Assert.That(layout.Drawn.Z, Is.EqualTo(0.32f).Within(1e-4), "64 of 200");
        Assert.That(layout.Drawn.W, Is.EqualTo(0.16f).Within(1e-4), "32 of 200");
        Assert.That(layout.Drawn.X, Is.EqualTo(0.34f).Within(1e-4), "centred");
    }

    // --- Alignment: which part survives when Stretch leaves room -----------------------------------------------------

    [TestCase(AlignmentX.Left, 0f)]
    [TestCase(AlignmentX.Center, 0.25f)]
    [TestCase(AlignmentX.Right, 0.5f)]
    public void AlignmentPlacesAUniformFitInsideItsTile(AlignmentX alignment, float expected)
    {
        var brush = Brush();
        brush.Stretch = Stretch.Uniform;
        brush.AlignmentX = alignment;

        // A 2:1 source in a 4:1 tile fits by HEIGHT, so it fills half the width and the rest is room to move in.
        var layout = Layout(brush, new Rect(0, 0, 400, 100));

        Assert.That(layout.Drawn.Z, Is.EqualTo(0.5f).Within(1e-4));
        Assert.That(layout.Drawn.X, Is.EqualTo(expected).Within(1e-4));
    }

    [TestCase(AlignmentX.Left, 0f)]
    [TestCase(AlignmentX.Center, 0.375f)]
    [TestCase(AlignmentX.Right, 0.75f)]
    public void AlignmentPicksWhichPartUniformToFillCrops(AlignmentX alignment, float expected)
    {
        var brush = Brush();
        brush.Stretch = Stretch.UniformToFill;
        brush.AlignmentX = alignment;

        // A 2:1 source covering a 1:2 tile overflows HORIZONTALLY, so the crop runs across the source's width.
        var layout = Layout(brush, new Rect(0, 0, 100, 200));

        Assert.That(layout.UvRect.Z, Is.EqualTo(0.25f).Within(1e-4));
        Assert.That(layout.UvRect.X, Is.EqualTo(expected).Within(1e-4));
    }

    // --- Tile mode ----------------------------------------------------------------------------------------------------

    [Test]
    public void TilingRepeatsTheSourceAtItsOwnPixelSize()
    {
        var brush = Brush();
        brush.TileMode = TileMode.Tile;
        brush.ViewportUnits = BrushMappingMode.Absolute;
        brush.Viewport = new Rect(0, 0, 64, 32);

        var layout = Layout(brush, new Rect(0, 0, 320, 128));

        Assert.That(layout.UvRect, Is.EqualTo(new Vector4F(0, 0, 1, 1)), "each tile samples the whole picture");
        Assert.That(layout.Tile.X, Is.EqualTo(5f).Within(1e-4), "320 / 64");
        Assert.That(layout.Tile.Y, Is.EqualTo(4f).Within(1e-4), "128 / 32");
    }

    [TestCase(TileMode.None, 0f, false)]
    [TestCase(TileMode.Tile, 0f, true)]
    [TestCase(TileMode.FlipX, 1f, true)]
    [TestCase(TileMode.FlipY, 2f, true)]
    [TestCase(TileMode.FlipXY, 3f, true)]
    public void TheTileModeStatesMirroringAndWhetherItRepeats(TileMode mode, float mirror, bool repeats)
    {
        var brush = Brush();
        brush.TileMode = mode;

        var layout = Layout(brush, new Rect(0, 0, 320, 128));

        Assert.That(layout.Mirror, Is.EqualTo(mirror));
        Assert.That(layout.Repeats, Is.EqualTo(repeats));
    }

    [Test]
    public void ABrushWithNoSourceStillLaysOutTheWholeShape()
    {
        var layout = Layout(new ImageBrush(), new Rect(0, 0, 100, 50));

        Assert.That(layout.UvRect, Is.EqualTo(new Vector4F(0, 0, 1, 1)));
        Assert.That(layout.Drawn, Is.EqualTo(new Vector4F(0, 0, 1, 1)));
        Assert.That(layout.Tile.X, Is.EqualTo(1f));
    }
}
