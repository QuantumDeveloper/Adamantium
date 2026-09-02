using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Drawings;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A DrawingBrush is a thin producer over <see cref="TileBrush"/>: what it owes the render path is CONTENT
/// with a size, in the form the path already knows. These pin that, and that the tiling survives a freeze - the render
/// thread reads the frozen snapshot, so a property lost there is a brush that paints differently than it was written.</summary>
[TestFixture]
public class DrawingBrushTests
{
    private static Drawing Square(double x, double y, double size) =>
        new GeometryDrawing
        {
            Geometry = new RectangleGeometry { Rect = new Rect(x, y, size, size) },
            Brush = new SolidColorBrush(Colors.Red)
        };

    [Test]
    public void ItsContentIsAnImageSourceTheRenderPathAlreadyDraws()
    {
        var brush = new DrawingBrush(Square(0, 0, 10));

        Assert.That(brush.ContentSource, Is.InstanceOf<DrawingImage>());
        Assert.That(((DrawingImage)brush.ContentSource).Drawing, Is.SameAs(brush.Drawing));
    }

    // The content's SIZE is the drawing's own extent - what Stretch and an absolute Viewbox are measured against.
    [Test]
    public void ItsContentSizeIsTheDrawingsOwnExtent()
    {
        var brush = new DrawingBrush(new DrawingGroup { Children = { Square(0, 0, 10), Square(10, 0, 14) } });

        Assert.That(brush.ContentSize, Is.EqualTo(new Size(24, 14)), "the union of the two squares, not either alone");
    }

    // Swapping the drawing must not swap the IMAGE: the image is the bake key, so a new one would throw away every
    // consumer's cached texture for a change that only altered what is inside it.
    [Test]
    public void SwappingTheDrawingKeepsTheSameContentObject()
    {
        var brush = new DrawingBrush(Square(0, 0, 10));
        var before = brush.ContentSource;

        brush.Drawing = Square(0, 0, 20);

        Assert.That(brush.ContentSource, Is.SameAs(before));
        Assert.That(((DrawingImage)brush.ContentSource).Drawing, Is.SameAs(brush.Drawing));
    }

    // The bake cache is keyed by the CONTENT OBJECT, and a snapshot is rebuilt on every property change - so a clone
    // with a fresh one misses the cache for ever AND orders a new bake, with its own render target, per change. That
    // is not a slow path: it exhausts device memory and takes the app down.
    [Test]
    public void EveryFrozenCloneSharesTheOneContentObject()
    {
        var brush = new DrawingBrush(Square(0, 0, 10));
        brush.ForRendering();
        var first = ((TileBrush)brush.Snapshot).ContentSource;

        brush.TileMode = TileMode.FlipXY;
        brush.Stretch = Stretch.Uniform;

        Assert.That(brush.Snapshot, Is.Not.SameAs(brush), "a snapshot is a clone, or this proves nothing");
        Assert.That(((TileBrush)brush.Snapshot).ContentSource, Is.SameAs(first), "a property change must not re-key the bake");
        Assert.That(first, Is.SameAs(brush.ContentSource));
    }

    [Test]
    public void TheFrozenSnapshotCarriesTheTilingAndTheDrawing()
    {
        var brush = new DrawingBrush(Square(0, 0, 10))
        {
            TileMode = TileMode.FlipXY,
            Stretch = Stretch.Uniform,
            AlignmentX = AlignmentX.Right,
            AlignmentY = AlignmentY.Bottom,
            Viewport = new Rect(1, 2, 3, 4),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0.1, 0.2, 0.3, 0.4),
            ViewboxUnits = BrushMappingMode.Absolute,
            Tint = Colors.Lime,
            Opacity = 0.5
        };

        brush.ForRendering();
        var frozen = (DrawingBrush)brush.Snapshot;

        Assert.That(frozen, Is.Not.SameAs(brush));
        Assert.That(frozen.Drawing, Is.SameAs(brush.Drawing));
        Assert.That(frozen.TileMode, Is.EqualTo(TileMode.FlipXY));
        Assert.That(frozen.Stretch, Is.EqualTo(Stretch.Uniform));
        Assert.That(frozen.AlignmentX, Is.EqualTo(AlignmentX.Right));
        Assert.That(frozen.AlignmentY, Is.EqualTo(AlignmentY.Bottom));
        Assert.That(frozen.Viewport, Is.EqualTo(new Rect(1, 2, 3, 4)));
        Assert.That(frozen.ViewportUnits, Is.EqualTo(BrushMappingMode.Absolute));
        Assert.That(frozen.Viewbox, Is.EqualTo(new Rect(0.1, 0.2, 0.3, 0.4)));
        Assert.That(frozen.ViewboxUnits, Is.EqualTo(BrushMappingMode.Absolute));
        Assert.That(frozen.Tint, Is.EqualTo(Colors.Lime));
        Assert.That(frozen.Opacity, Is.EqualTo(0.5));
    }

    // It goes through the SAME arithmetic an ImageBrush does - that is the whole point of the shared base.
    [Test]
    public void ItLaysOutThroughTheSharedTilingArithmetic()
    {
        var brush = new DrawingBrush(new DrawingGroup { Children = { Square(0, 0, 20), Square(20, 0, 10) } })
        {
            Stretch = Stretch.Uniform
        };

        var layout = ImageTiling.Layout(brush, new Rect(0, 0, 200, 200), 1.0, 1.0);

        Assert.That(brush.ContentSize, Is.EqualTo(new Size(30, 20)));
        Assert.That(layout.Drawn.Z, Is.EqualTo(1f).Within(1e-4), "the wide axis fills the tile");
        Assert.That(layout.Drawn.W, Is.EqualTo(20f / 30f).Within(1e-4), "the other keeps the 3:2 ratio");
    }

    // A VIEWBOX is not a crop of the finished bake - it is what gets baked. Sizing the whole drawing and sampling a
    // quarter of the result spends three quarters of the resolution on what nobody sees, and the quarter that IS seen
    // arrives at a quarter of the tile's pixel density: measured on the stand as an edge that took 2 px to cross where
    // a full viewbox took 1.
    [Test]
    public void AViewboxIsBakedAtItsOwnResolution()
    {
        var brush = new DrawingBrush(new DrawingGroup { Children = { Square(0, 0, 40) } })
        {
            Stretch = Stretch.Fill,
            Viewbox = new Rect(0.25, 0.25, 0.5, 0.5)   // the middle quarter of the drawing
        };

        var whole = new DrawingBrush(new DrawingGroup { Children = { Square(0, 0, 40) } }) { Stretch = Stretch.Fill };

        var box = new Rect(0, 0, 200, 200);
        Assert.Multiple(() =>
        {
            // The slice is half the drawing on each axis, so it is baked at the SAME size the whole picture would be -
            // that size is the tile's, and the tile is what the slice has to fill.
            Assert.That(ImageTiling.BakeSize(brush, box.Size), Is.EqualTo(ImageTiling.BakeSize(whole, box.Size)),
                "the slice is baked at the tile's resolution, not at a fraction of it");

            // ...and because the texture then holds only the slice, the fill samples ALL of it.
            var layout = ImageTiling.Layout(brush, box, 1.0, 1.0, sourceIsSlice: true);
            Assert.That(layout.UvRect.X, Is.EqualTo(0f).Within(1e-4));
            Assert.That(layout.UvRect.Y, Is.EqualTo(0f).Within(1e-4));
            Assert.That(layout.UvRect.Z, Is.EqualTo(1f).Within(1e-4));
            Assert.That(layout.UvRect.W, Is.EqualTo(1f).Within(1e-4));

            // A RASTER source is untouched by any of this: its pixels exist once, and the viewbox stays a sub-rectangle.
            var raster = ImageTiling.Layout(brush, box, 1.0, 1.0);
            Assert.That(raster.UvRect.X, Is.EqualTo(0.25f).Within(1e-4));
            Assert.That(raster.UvRect.Z, Is.EqualTo(0.5f).Within(1e-4));
        });
    }

    // A bake for a size not ready yet is served by a STAND-IN from the cache, and BOTH ends of that rule have shipped a
    // defect. Too loose (any slice would do) and every not-yet-baked viewbox got whichever slice was baked first, which
    // reads as "the viewbox does nothing". Too strict (only the same slice) and a viewbox that has just changed has
    // nothing to show at all, so the fill BLINKS empty on every step of the slider. The rule is an ORDER, not a
    // permission: prefer this slice, fall back to another, and let the exact bake win as soon as it lands.
    [Test]
    public void AStandInPrefersThisSliceButNeverLeavesTheFillEmpty()
    {
        var wanted = (Width: 96, Height: 96, Slice: 54321);

        Assert.Multiple(() =>
        {
            // The same slice at ANOTHER size beats another slice at the exact size - that is the whole of the first bug.
            var mixed = new[] { (128, 128, 12345), (64, 64, 54321), (96, 96, 12345) };
            Assert.That(DrawingImageRaster.PickStandIn(mixed, wanted), Is.EqualTo(1), "this slice comes first, however far off its size");

            // ...and among the same slice, the nearest size.
            var sizes = new[] { (256, 256, 54321), (128, 128, 54321), (32, 32, 54321) };
            Assert.That(DrawingImageRaster.PickStandIn(sizes, wanted), Is.EqualTo(1), "the nearest size of the right slice");

            // Nothing of this slice is baked yet - which is exactly what a viewbox that just moved looks like. Another
            // slice is wrong for a frame; empty is a blink.
            var others = new[] { (256, 256, 12345), (128, 128, 12345) };
            Assert.That(DrawingImageRaster.PickStandIn(others, wanted), Is.EqualTo(1), "another slice rather than nothing");

            // A different aspect would arrive distorted, so it is not a stand-in at any rank.
            var shapes = new[] { (192, 96, 54321), (96, 32, 12345) };
            Assert.That(DrawingImageRaster.PickStandIn(shapes, wanted), Is.EqualTo(-1), "a different shape is a different picture");

            Assert.That(DrawingImageRaster.PickStandIn([], wanted), Is.EqualTo(-1), "nothing baked, nothing to show");
        });
    }
}
