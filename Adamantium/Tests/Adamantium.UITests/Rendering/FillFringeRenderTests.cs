using System;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// Pixel-level proof that the analytic-AA fill fringe lands OUTSIDE the shape and is exactly one DEVICE pixel wide at any
/// scale. Both matter and neither is visible from CPU state: the fringe is expanded in the vertex shader in pixels (see
/// FillFringeEffect.fx), so a sign slip in the screen-space miter would feather INWARD - eating a pixel of the body
/// instead of smoothing its edge - and a scale leaking back into the ring geometry would widen it under a zoom.
/// </summary>
[TestFixture]
[Category("Gpu")]
public class FillFringeRenderTests
{
    private const int Dim = 240;

    // A square drawn as a GEOMETRY (not a RectanglePayload): the SDF rect batch is self-anti-aliasing and would never
    // build a fringe, so the fringe path needs an arbitrary-geometry fill.
    private static byte[] RenderSquare(double scale, Brush fill = null)
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Transparent };

        var brush = fill ?? Brushes.Red;
        var content = new TestControl
        {
            RenderAction = s => s.DrawGeometry(brush, new RectangleGeometry(new Rect(10, 10, 40, 40)))
        };
        content.Bounds = new Rect(0, 0, Dim, Dim);
        content.RenderSize = new Size(Dim, Dim);
        if (scale != 1.0) content.RenderTransform = new Transform { ScaleX = scale, ScaleY = scale };

        var root = new VisualRoot(content, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");

        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var bytes = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
        return bytes;
    }

    // B8G8R8A8 read-back onto a TRANSPARENT clear, so alpha alone is the coverage at that pixel whatever the fill is.
    private static int Alpha(byte[] pixels, int x, int y) => pixels[(y * Dim + x) * 4 + 3];

    private static void AssertOnePixelEdge(byte[] pixels, int edge)
    {
        var y = edge / 2;   // a row well inside the square's vertical span at either scale

        // The body is opaque right up to its own edge - the fringe must not eat into it.
        Assert.That(Alpha(pixels, edge - 1, y), Is.EqualTo(255), "the last body pixel must stay fully covered");
        // The pixel the contour passes through carries partial coverage: that IS the analytic edge.
        Assert.That(Alpha(pixels, edge, y), Is.InRange(1, 254), "the edge pixel must be partially covered (the fringe)");
        // One device pixel wide - at 4x too, which is the whole point of expanding in screen space.
        Assert.That(Alpha(pixels, edge + 1, y), Is.EqualTo(0), "the fringe must not reach a second pixel");
    }

    // Solid fill: body AND fringe are drawn by the INSTANCED path (one shared ring per mesh).
    [TestCase(1.0, 50, TestName = "Fringe_IsOutside_AndOnePixelWide_AtScale1")]
    [TestCase(4.0, 200, TestName = "Fringe_IsOutside_AndOnePixelWide_AtScale4")]
    public void Fringe_IsOutside_AndOnePixelWide(double scale, int edge) => AssertOnePixelEdge(RenderSquare(scale), edge);

    // A GRADIENT fill still feathers per-unit (FillFringeEffect.fx), which offsets the ring in screen pixels the same
    // way - so it has to hold at both scales too, or the two paths have drifted apart.
    [TestCase(1.0, 50, TestName = "GradientFringe_IsOutside_AndOnePixelWide_AtScale1")]
    [TestCase(4.0, 200, TestName = "GradientFringe_IsOutside_AndOnePixelWide_AtScale4")]
    public void GradientFringe_IsOutside_AndOnePixelWide(double scale, int edge)
    {
        var brush = new LinearGradientBrush
        {
            GradientStops = { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1) }
        };
        AssertOnePixelEdge(RenderSquare(scale, brush), edge);
    }

    // A gradient fill on ARBITRARY geometry goes through the instanced gradient pass, whose record now stores the
    // transform RELATIVE to a transform-table slot instead of a baked world. Getting that wrong moves the shape (the
    // classic symptom: it collapses toward the clip origin), so this pins where it lands and that the ramp survives.
    [Test]
    public void InstancedGradientGeometry_LandsWhereItIsPlaced()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Transparent };

        var brush = new LinearGradientBrush
        {
            StartPoint = new Vector2(0, 0),
            EndPoint = new Vector2(1, 0),
            GradientStops = { new GradientStop(Colors.Red, 0), new GradientStop(Colors.Blue, 1) }
        };
        var content = new TestControl
        {
            RenderAction = s => s.DrawGeometry(brush, new RectangleGeometry(new Rect(10, 10, 40, 40)))
        };
        content.Bounds = new Rect(0, 0, Dim, Dim);
        content.RenderSize = new Size(Dim, Dim);

        var root = new VisualRoot(content, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True);

        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        int A(int x, int y) => pixels[(y * Dim + x) * 4 + 3];
        int R(int x, int y) => pixels[(y * Dim + x) * 4 + 2];
        int B(int x, int y) => pixels[(y * Dim + x) * 4 + 0];

        // Inside the square is opaque, outside is empty - i.e. it is drawn at 10..50, not somewhere else.
        Assert.That(A(30, 30), Is.EqualTo(255), "the shape must cover its own bounds");
        Assert.That(A(5, 30), Is.EqualTo(0), "nothing must be drawn left of the shape");
        Assert.That(A(60, 30), Is.EqualTo(0), "nothing must be drawn right of the shape");
        // ...and it is still a left-to-right red->blue ramp.
        Assert.That(R(13, 30), Is.GreaterThan(R(47, 30)), "red must fade out from left to right");
        Assert.That(B(47, 30), Is.GreaterThan(B(13, 30)), "blue must build up towards the right");
    }

    // Same for the PATTERN pass (noise shares this record), which also moved from a baked world onto a slot.
    [Test]
    public void InstancedPatternGeometry_LandsWhereItIsPlaced()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Transparent };

        var brush = new PatternBrush
        {
            Pattern = PatternType.Checkerboard,
            Color1 = Colors.Red,
            Color2 = Colors.Blue,
            CellSize = 8
        };
        var content = new TestControl
        {
            RenderAction = s => s.DrawGeometry(brush, new RectangleGeometry(new Rect(10, 10, 40, 40)))
        };
        content.Bounds = new Rect(0, 0, Dim, Dim);
        content.RenderSize = new Size(Dim, Dim);

        var root = new VisualRoot(content, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True);

        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        int A(int x, int y) => pixels[(y * Dim + x) * 4 + 3];

        Assert.That(A(30, 30), Is.EqualTo(255), "the shape must cover its own bounds");
        Assert.That(A(5, 30), Is.EqualTo(0), "nothing must be drawn left of the shape");
        Assert.That(A(60, 30), Is.EqualTo(0), "nothing must be drawn right of the shape");

        // Both checker colours must appear inside it - i.e. the pattern is still a pattern, not one flat fill.
        var reds = 0;
        var blues = 0;
        for (var x = 12; x < 48; x++)
        {
            var r = pixels[(30 * Dim + x) * 4 + 2];
            var b = pixels[(30 * Dim + x) * 4 + 0];
            if (r > 200 && b < 60) reds++;
            if (b > 200 && r < 60) blues++;
        }
        Assert.That(reds, Is.GreaterThan(0), "the pattern's first colour must show");
        Assert.That(blues, Is.GreaterThan(0), "the pattern's second colour must show");
    }

    // The unit factory needs one, but nothing here draws a texture or text.
    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, System.Collections.Generic.IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();
        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
