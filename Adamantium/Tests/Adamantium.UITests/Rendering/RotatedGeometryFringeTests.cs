using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.UI.Rendering.Retained;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A shape turned by a transform must keep its anti-aliased edge. A diagonal is the only place the question can be
/// asked at all - an axis-aligned edge lands on the pixel grid and looks clean with no fringe whatsoever - which is
/// why an icon that TURNS is where a missing fringe shows up first, and why it looks like "the rotation broke it".
/// <para>Written for a drawing whose nested group carries its own Transform: the geometry never changes, only where
/// the group puts it, so the mesh is shared and the turn rides in the instance. That is the instanced path, and the
/// comparison here is against the per-unit one, which draws the same shape with its own fringe.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class RotatedGeometryFringeTests
{
    private const int Dim = 64;

    // A bar across the middle, turned 30 degrees about the centre: both long edges cross the pixel grid at an angle,
    // so every scanline through them has an edge to soften.
    private static byte[] Render(bool instanced)
    {
        var wasEnabled = InstancedFillCollector.Enabled;
        InstancedFillCollector.Enabled = instanced;
        try
        {
            var device = GpuTestDevice.Device;
            var factory = new RenderUnitFactory(device, new StubResourceFactory());
            using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

            var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
            var bar = new RectangleGeometry(new Rect(8, 26, 48, 12));
            var turn = Matrix4x4F.Translation(-Dim / 2f, -Dim / 2f, 0)
                       * Matrix4x4F.RotationZ((float)(Math.PI / 6))
                       * Matrix4x4F.Translation(Dim / 2f, Dim / 2f, 0);
            stage.RenderAction = s => s.DrawGeometry(Brushes.White, bar, null, turn);

            var root = new VisualRoot(stage, Dim, Dim);
            Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();

            using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
            var bytes = new byte[(int)img.TotalSizeInBytes];
            Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            InstancedFillCollector.Enabled = wasEnabled;
        }
    }

    // How many pixels are neither the black ground nor the white bar. On a turned shape those are the fringe: with an
    // analytic edge there is roughly one per scanline per edge, without one there are none at all.
    private static int SoftPixels(byte[] px)
    {
        var soft = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            var v = px[i + 2];   // BGRA: red, and the bar is white on black, so any channel says the same
            if (v > 12 && v < 243) soft++;
        }
        return soft;
    }

    [Test]
    public void ATurnedShapeKeepsItsSoftEdge()
    {
        var perUnit = SoftPixels(Render(instanced: false));
        var instanced = SoftPixels(Render(instanced: true));

        TestContext.WriteLine($"soft edge pixels: per-unit {perUnit}, instanced {instanced}");
        Assert.That(perUnit, Is.GreaterThan(20), "the per-unit path softens a diagonal - if it does not, this test is measuring the wrong thing");
        Assert.That(instanced, Is.GreaterThan(perUnit / 2),
            "the instanced path must soften the same diagonal: a turned icon loses its anti-aliasing when it does not");
    }

    // The SAME geometry blown up by its transform, against one authored at that size. A drawing shown at two sizes
    // shares one tessellation - that is the point of it - so the curve is cut once, and the question is at which size.
    // If it is cut for the small one and stretched to the large one, every curve on the large one is a polygon, and no
    // fringe can put that back: the fringe softens whatever edge it is given, including a faceted one.
    private static byte[] RenderRounded(bool byTransform)
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
        // A disc: all curve, nothing else, so the count below measures only how finely it was cut.
        var small = new RectangleGeometry(new Rect(0, 0, 16, 16), new CornerRadius(8));
        var large = new RectangleGeometry(new Rect(0, 0, 48, 48), new CornerRadius(24));
        var place = Matrix4x4F.Translation(8, 8, 0);
        var blowUp = Matrix4x4F.Scaling(3f, 3f, 1f) * Matrix4x4F.Translation(8, 8, 0);

        stage.RenderAction = byTransform
            ? s => s.DrawGeometry(Brushes.White, small, null, blowUp)
            : s => s.DrawGeometry(Brushes.White, large, null, place);

        var root = new VisualRoot(stage, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
        RenderDirty.Clear();

        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var bytes = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
        return bytes;
    }

    [Test]
    public void ACurveBlownUpByItsTransformStaysACurve()
    {
        var stretched = SoftPixels(RenderRounded(byTransform: true));
        var authored = SoftPixels(RenderRounded(byTransform: false));

        TestContext.WriteLine($"soft edge pixels on the disc: stretched {stretched}, authored at size {authored}");
        Assert.That(stretched, Is.EqualTo(authored).Within(authored * 0.35),
            "a curve cut for a SMALL shape and stretched is a polygon at the large one - the same disc must not " +
            "depend on whether its size came from the geometry or from the transform");
    }

    // What a drawing actually is: several shapes replayed into ONE session by one control, overlapping each other, with
    // an inner group carrying its own turn. A fringe is held back and flushed after the fills it belongs with, so the
    // question a single shape cannot ask is whether a LATER fill in the same session takes an earlier one's edge away.
    private static byte[] RenderBadge(int shapes)
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
        // The badge's own proportions: authored in 24 units and blown up to the box, which is what an Image does to a
        // DrawingImage (see DrawingImage.Render).
        var scale = Dim / 24f;
        var card = new RectangleGeometry(new Rect(0, 0, 24, 24), new CornerRadius(4));
        var bar = new RectangleGeometry(new Rect(10.5, 3, 3, 18), new CornerRadius(1.5));
        var cross = new RectangleGeometry(new Rect(3, 10.5, 18, 3), new CornerRadius(1.5));
        var fit = Matrix4x4F.Scaling(scale, scale, 1f);
        var turn = Matrix4x4F.Translation(-12, -12, 0)
                   * Matrix4x4F.RotationZ((float)(Math.PI / 6))
                   * Matrix4x4F.Translation(12, 12, 0) * fit;

        // Three FLAT tones, so "softened" can be counted exactly: anything that is none of them is a blend, and only an
        // anti-aliased edge produces one.
        stage.RenderAction = s =>
        {
            if (shapes > 0) s.DrawGeometry(new SolidColorBrush(Grey), card, null, fit);
            if (shapes > 1) s.DrawGeometry(Brushes.White, bar, null, turn);
            if (shapes > 2) s.DrawGeometry(Brushes.White, cross, null, turn);
        };

        var root = new VisualRoot(stage, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
        RenderDirty.Clear();

        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var bytes = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
        return bytes;
    }

    private static readonly Color Grey = Color.FromRgba(128, 128, 128, 255);

    // Exactly the pixels that are none of the three tones drawn - i.e. a blend, i.e. a softened edge.
    private static int Blended(byte[] px)
    {
        var blended = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            var v = px[i + 2];
            if (v != 0 && v != 128 && v != 255) blended++;
        }
        return blended;
    }

    [Test]
    public void EveryShapeOfADrawingKeepsItsEdge()
    {
        var one = Blended(RenderBadge(1));
        var two = Blended(RenderBadge(2));
        var three = Blended(RenderBadge(3));

        TestContext.WriteLine($"soft edge pixels: card {one}, +turned bar {two}, +crossing bar {three}");
        Assert.That(two, Is.GreaterThan(one),
            "the turned bar has two long diagonals - adding it must add softened pixels, not leave the count where it was");
        Assert.That(three, Is.GreaterThan(two),
            "and so must the bar crossing it: a later fill in the same session must not cost the earlier ones their edge");
    }

    // The unit factory needs one, but nothing here draws a texture or text.
    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();
        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
