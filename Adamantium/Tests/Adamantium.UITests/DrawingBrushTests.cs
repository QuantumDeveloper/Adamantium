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
}
