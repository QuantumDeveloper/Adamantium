using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

// Where one copy of a picture lands and how many copies fit. Pure rectangle arithmetic, so it is tested without a
// device - the same reason the nine-slice cutter is.
[TestFixture]
public class ImageTilingTests
{
    // 64x32 - deliberately NOT square, so an aspect-preserving mode has something to preserve.
    private sealed class Source : BitmapSource
    {
        public Source() : base(64, 32, 1, 1, Adamantium.Imaging.SurfaceFormat.B8G8R8A8.UNorm, new byte[64 * 32 * 4]) { }
    }

    private static ImageBrush Brush() => new(new Source());

    private static (Rect Bounds, Vector4F UvRect, Vector4F UvRepeat) Layout(ImageBrush brush, Rect bounds)
        => ImageTiling.Layout(brush, bounds, 1.0, 1.0);

    [Test]
    public void FillTakesTheWholeShapeAndTheWholeSource()
    {
        var shape = new Rect(0, 0, 300, 200);

        var (bounds, uv, repeat) = Layout(Brush(), shape);

        Assert.That(bounds, Is.EqualTo(shape));
        Assert.That(uv, Is.EqualTo(new Vector4F(0, 0, 1, 1)));
        Assert.That(repeat.X, Is.EqualTo(1f));
        Assert.That(repeat.Y, Is.EqualTo(1f));
    }

    // Tiling counts how many TILES fit - fractional, so the last one is cut by the shape's edge, as a tiled surface must.
    [Test]
    public void TilingRepeatsTheSourceAtItsOwnPixelSize()
    {
        var brush = Brush();
        brush.TileMode = TileMode.Tile;

        var (bounds, uv, repeat) = Layout(brush, new Rect(0, 0, 320, 128));

        Assert.That(bounds.Width, Is.EqualTo(320));
        Assert.That(uv, Is.EqualTo(new Vector4F(0, 0, 1, 1)), "each tile samples the whole picture");
        Assert.That(repeat.X, Is.EqualTo(5f).Within(1e-4), "320 / 64");
        Assert.That(repeat.Y, Is.EqualTo(4f).Within(1e-4), "128 / 32");
    }

    [Test]
    public void AStatedTileSizeWins()
    {
        var brush = Brush();
        brush.TileMode = TileMode.Tile;
        brush.TileSize = new Size(32, 32);

        var (_, _, repeat) = Layout(brush, new Rect(0, 0, 320, 128));

        Assert.That(repeat.X, Is.EqualTo(10f).Within(1e-4));
        Assert.That(repeat.Y, Is.EqualTo(4f).Within(1e-4));
    }

    // The mirror flags ride UvRepeat.z: 1 = X, 2 = Y, 3 = both. The shader reads them branch-free.
    [TestCase(TileMode.Tile, 0f)]
    [TestCase(TileMode.FlipX, 1f)]
    [TestCase(TileMode.FlipY, 2f)]
    [TestCase(TileMode.FlipXY, 3f)]
    public void TheMirrorFlagsAreCarriedOnTheRepeat(TileMode mode, float expected)
    {
        var brush = Brush();
        brush.TileMode = mode;

        var (_, _, repeat) = Layout(brush, new Rect(0, 0, 320, 128));

        Assert.That(repeat.Z, Is.EqualTo(expected));
    }

    // UniformToFill covers the shape and CROPS: the drawn rect is the whole shape, the sampled rect shrinks.
    [Test]
    public void UniformToFillCropsTheSourceRatherThanTheShape()
    {
        var brush = Brush();
        brush.Stretch = Stretch.UniformToFill;

        var (bounds, uv, _) = Layout(brush, new Rect(0, 0, 200, 200));   // square shape, 2:1 source

        Assert.That(bounds, Is.EqualTo(new Rect(0, 0, 200, 200)), "the shape is filled edge to edge");
        Assert.That(uv.Z, Is.EqualTo(0.5f).Within(1e-4), "half the source's width is sampled");
        Assert.That(uv.X, Is.EqualTo(0.25f).Within(1e-4), "and it is the CENTRED half");
        Assert.That(uv.W, Is.EqualTo(1f), "its full height");
    }

    // Uniform does the opposite: the picture keeps its shape, so what is DRAWN shrinks and centres.
    [Test]
    public void UniformFitsTheDrawnRectAndKeepsTheWholeSource()
    {
        var brush = Brush();
        brush.Stretch = Stretch.Uniform;

        var (bounds, uv, _) = Layout(brush, new Rect(0, 0, 200, 200));

        Assert.That(uv, Is.EqualTo(new Vector4F(0, 0, 1, 1)), "nothing is cropped");
        Assert.That(bounds.Width, Is.EqualTo(200).Within(1e-4), "the wide axis fills");
        Assert.That(bounds.Height, Is.EqualTo(100).Within(1e-4), "the other keeps the 2:1 ratio");
        Assert.That(bounds.Y, Is.EqualTo(50).Within(1e-4), "centred in what is left");
    }

    // None draws it at its own pixel size - and a picture bigger than its shape is cropped BY the shape, not scaled.
    [Test]
    public void NoneDrawsTheSourceAtItsOwnSize()
    {
        var brush = Brush();
        brush.Stretch = Stretch.None;

        var (bounds, _, _) = Layout(brush, new Rect(0, 0, 200, 200));

        Assert.That(bounds.Width, Is.EqualTo(64).Within(1e-4));
        Assert.That(bounds.Height, Is.EqualTo(32).Within(1e-4));
        Assert.That(bounds.X, Is.EqualTo(68).Within(1e-4), "centred");
    }

    [Test]
    public void ABrushWithNoSourceStillLaysOutTheWholeShape()
    {
        var (bounds, uv, repeat) = Layout(new ImageBrush(), new Rect(0, 0, 100, 50));

        Assert.That(bounds, Is.EqualTo(new Rect(0, 0, 100, 50)));
        Assert.That(uv, Is.EqualTo(new Vector4F(0, 0, 1, 1)));
        Assert.That(repeat.X, Is.EqualTo(1f));
    }
}
