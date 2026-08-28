using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.Vulkan.Core;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A brush REWRITTEN IN PLACE has to reach the pixels of everything painting with it, whether or not those elements are
/// re-recorded that frame.
/// <para>It used to reach only the ones that happened to be in the frame's dirty set, because that set is what the paint
/// patch walks. An in-place recolour writes no property, adds no unit and moves no slot, so an element the change did not
/// re-record was left in the previous colour until something unrelated forced a walk. On a palette repaint that showed as
/// icons following the theme on one switch and not on the next, and coming right as soon as anything was scrolled.
/// See <c>RenderCache.ApplyBrushRepaints</c>.</para>
/// <para>The scene here is exactly that case: the brush is captured by the draw action rather than assigned to a
/// brush-valued property, so recolouring it marks NOTHING - the second frame has no dirty element to find.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class BrushRepaintByIndexTests
{
    private const int Dim = 120;

    // B8G8R8A8 read-back, as every other pixel test in this folder reads it.
    private static (byte R, byte G) Pixel(OffscreenTestRenderer renderer, int x, int y)
    {
        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var bytes = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
        var i = (y * Dim + x) * 4;
        return (bytes[i + 2], bytes[i + 1]);
    }

    [Test]
    public void ARecolouredBrush_ReachesTheShape_WithNothingMarkingTheElement()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Transparent };

        var brush = new SolidColorBrush(Colors.Red);
        var content = new TestControl
        {
            RenderAction = s => s.DrawGeometry(brush, new RectangleGeometry(new Rect(20, 20, 60, 60)))
        };
        content.Bounds = new Rect(0, 0, Dim, Dim);
        content.RenderSize = new Size(Dim, Dim);

        var root = new VisualRoot(content, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
        Assume.That(Pixel(renderer, 50, 50).R, Is.GreaterThan(200), "precondition: the shape is drawn red");

        var repaintsBefore = renderer.Cache.BrushRepaintTotal;
        brush.Color = Colors.Lime;

        Assert.That(renderer.RenderFrame(root), Is.True, "the second frame must render");

        var after = Pixel(renderer, 50, 50);
        Assert.Multiple(() =>
        {
            Assert.That(after.G, Is.GreaterThan(200), "the shape must wear the new colour");
            Assert.That(after.R, Is.LessThan(60), "...and none of the old one");
            Assert.That(renderer.Cache.BrushRepaintTotal, Is.GreaterThan(repaintsBefore),
                "...and it has to have travelled by the brush index - nothing else knows this element changed");
        });
    }

    /// <summary>The STROKED half of the same question. Half the icons in a set are strokes - a cross, a checkmark, an
    /// arrow - and a stroke keeps its colour somewhere else entirely from a fill, so the fill following the palette says
    /// nothing about them.</summary>
    [Test]
    public void ARecolouredBrush_ReachesA_STROKE_WithNothingMarkingTheElement()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Transparent };

        var brush = new SolidColorBrush(Colors.Red);
        var pen = new Pen(brush, 8);
        var content = new TestControl
        {
            RenderAction = s => s.DrawGeometry(null, new LineGeometry(new Vector2(20, 60), new Vector2(100, 60)), pen)
        };
        content.Bounds = new Rect(0, 0, Dim, Dim);
        content.RenderSize = new Size(Dim, Dim);

        var root = new VisualRoot(content, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
        Assume.That(Pixel(renderer, 60, 60).R, Is.GreaterThan(200), "precondition: the stroke is drawn red");

        brush.Color = Colors.Lime;
        Assert.That(renderer.RenderFrame(root), Is.True, "the second frame must render");

        var after = Pixel(renderer, 60, 60);
        Assert.Multiple(() =>
        {
            Assert.That(after.G, Is.GreaterThan(200), "the stroke must wear the new colour");
            Assert.That(after.R, Is.LessThan(60), "...and none of the old one");
        });
    }

    /// <summary>The real icon path, end to end: a shared <see cref="DrawingImage"/> resource shown by an
    /// <see cref="Adamantium.UI.Controls.Image"/>, recoloured through the brush the drawing holds - which is how a theme
    /// palette repaints one. The element is NOT the brush's owner here (the brush lives inside the drawing), so nothing
    /// about the element itself says it changed.</summary>
    [TestCase(false, TestName = "AnIconFILL_FollowsItsBrush")]
    [TestCase(true, TestName = "AnIconSTROKE_FollowsItsBrush")]
    public void AnIconDrawing_FollowsItsBrush(bool stroked)
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Transparent };

        var brush = new SolidColorBrush(Colors.Red);
        var drawing = new Adamantium.UI.Core.Media.Drawings.GeometryDrawing
        {
            Geometry = new RectangleGeometry(new Rect(0, 0, 10, 10))
        };
        if (stroked)
        {
            drawing.Stroke = brush;
            drawing.StrokeThickness = 3;
        }
        else
        {
            drawing.Brush = brush;
        }

        var image = new Adamantium.UI.Controls.Image
        {
            Source = new DrawingImage { Drawing = drawing },
            Stretch = Stretch.Fill,
            Width = Dim,
            Height = Dim
        };
        // A real control lays ITSELF out - exactly what VisualRenderer does, and what an Image needs before it draws.
        var root = new VisualRoot(image, Dim, Dim);
        ((IMeasurableComponent)root).Measure(new Size(Dim, Dim));
        ((IMeasurableComponent)root).Arrange(new Rect(new Size(Dim, Dim)));

        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");

        // A fill covers the middle; a stroke only ever paints its contour, so each is sampled where it actually is.
        var (sx, sy) = stroked ? (Dim / 2, 8) : (Dim / 2, Dim / 2);
        Assume.That(Pixel(renderer, sx, sy).R, Is.GreaterThan(200), "precondition: the icon is drawn red");

        brush.Color = Colors.Lime;
        Assert.That(renderer.RenderFrame(root), Is.True, "the second frame must render");

        var after = Pixel(renderer, sx, sy);
        Assert.Multiple(() =>
        {
            Assert.That(after.G, Is.GreaterThan(200), "the icon must wear the new colour");
            Assert.That(after.R, Is.LessThan(60), "...and none of the old one");
        });
    }

    // The unit factory needs one, but nothing here draws a texture or text.
    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new System.NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, System.Collections.Generic.IReadOnlyList<byte[]> layers) => throw new System.NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new System.NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new System.NotSupportedException();
        public Adamantium.Graphics.Fonts.FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new System.NotSupportedException();
    }
}
