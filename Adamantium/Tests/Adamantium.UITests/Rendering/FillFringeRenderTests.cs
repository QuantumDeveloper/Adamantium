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
