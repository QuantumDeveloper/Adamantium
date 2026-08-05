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
/// A 1-unit-wide rectangle blown up by RenderTransform.ScaleX - exactly how the TabControl selection bar is drawn
/// (a 1px base rect, TranslateX = the tab's offset, ScaleX = the tab's extent). The scale lives in the transform TABLE
/// (nothing is baked into the instance any more), so the SDF rect shader still sees a 1-unit-wide rect and everything
/// it measures in LOCAL units - its outset, and the coverage ramp derived from it - is stretched by the same factor on
/// screen. This asserts the bar's edges stay crisp and where they belong.
/// </summary>
[TestFixture]
[Category("Gpu")]
public class ScaledRectEdgeTests
{
    private const int Dim = 240;
    private const int BarHeight = 3;
    private const double ScaleX = 100.0;

    // motionNode = the element drives its own subtree (what an ANIMATED RenderTransform makes it - the selection bar
    // becomes one the first time the selection slides). Then RelWorld is identity and the scale lives in the transform
    // TABLE instead of the baked bounds - which is the case the SDF shader's LOCAL-unit outset cannot survive.
    [TestCase(false, TestName = "ScaledBar_HasCrispEdges_AtItsScaledWidth")]
    [TestCase(true, TestName = "ScaledBar_HasCrispEdges_AtItsScaledWidth_AsMotionNode")]
    public void ScaledBar_HasCrispEdges_AtItsScaledWidth(bool motionNode)
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Transparent };

        // The element carries the scale; the draw itself is a 1 x 3 rect at the origin - the tab bar's exact shape.
        var bar = new TestControl
        {
            RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 1, BarHeight)),
            RenderTransform = new Transform { ScaleX = ScaleX, TranslateX = 20, TranslateY = 20 }
        };
        bar.Bounds = new Rect(0, 0, 1, BarHeight);
        bar.RenderSize = new Size(1, BarHeight);
        bar.IsRenderMotionNode = motionNode;

        var root = new VisualRoot(bar, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True);

        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        int Alpha(int x, int y) => pixels[(y * Dim + x) * 4 + 3];

        var y = 21;   // inside the 3px-tall bar (top edge at 20)
        // Scanned across the row, report where coverage starts and ends and how many pixels are PARTIAL - a stretched
        // local-unit ramp shows up as a wide band of partial coverage instead of one pixel at each end.
        int first = -1, last = -1, partial = 0;
        for (var x = 0; x < Dim; x++)
        {
            var a = Alpha(x, y);
            if (a > 0) { if (first < 0) first = x; last = x; }
            if (a is > 0 and < 255) partial++;
        }
        TestContext.WriteLine($"bar row: first={first} last={last} partial={partial}");

        Assert.That(first, Is.EqualTo(20), "the bar must start where TranslateX put it");
        Assert.That(last, Is.EqualTo(20 + (int)ScaleX - 1), "the bar must end at TranslateX + ScaleX");
        Assert.That(partial, Is.LessThanOrEqualTo(2), "an axis-aligned bar has at most one partial pixel per end");
    }

    // The same bar with a GRADIENT fill: a different pass (GradientPS re-reads its record by BDA), so it converts the
    // stroke record to pixels via its own interpolator. Both ends must stay put and the fill must still be a gradient.
    [Test]
    public void ScaledGradientBar_HasCrispEdges_AndKeepsItsGradient()
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
        var bar = new TestControl
        {
            RenderAction = s => s.DrawRectangle(brush, new Rect(0, 0, 1, BarHeight)),
            RenderTransform = new Transform { ScaleX = ScaleX, TranslateX = 20, TranslateY = 20 }
        };
        bar.Bounds = new Rect(0, 0, 1, BarHeight);
        bar.RenderSize = new Size(1, BarHeight);
        bar.IsRenderMotionNode = true;

        var root = new VisualRoot(bar, Dim, Dim);
        Assert.That(renderer.RenderFrame(root), Is.True);

        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        int Alpha(int x, int y) => pixels[(y * Dim + x) * 4 + 3];
        int Red(int x, int y) => pixels[(y * Dim + x) * 4 + 2];
        int Blue(int x, int y) => pixels[(y * Dim + x) * 4 + 0];

        var y = 21;
        int first = -1, last = -1, partial = 0;
        for (var x = 0; x < Dim; x++)
        {
            var a = Alpha(x, y);
            if (a > 0) { if (first < 0) first = x; last = x; }
            if (a is > 0 and < 255) partial++;
        }
        TestContext.WriteLine($"gradient bar: first={first} last={last} partial={partial} " +
                              $"left=({Red(21, y)},{Blue(21, y)}) right=({Red(118, y)},{Blue(118, y)})");

        Assert.That(first, Is.EqualTo(20), "the bar must start where TranslateX put it");
        Assert.That(last, Is.EqualTo(20 + (int)ScaleX - 1), "the bar must end at TranslateX + ScaleX");
        Assert.That(partial, Is.LessThanOrEqualTo(2), "an axis-aligned bar has at most one partial pixel per end");
        // The fill is still a left-to-right red->blue ramp, i.e. converting the SDF to pixels left the uv alone.
        Assert.That(Red(21, y), Is.GreaterThan(Red(118, y)), "red must fade out from left to right");
        Assert.That(Blue(118, y), Is.GreaterThan(Blue(21, y)), "blue must build up towards the right");
    }

    // A PROCEDURAL fill (pattern/noise share one pass and one cell field). Where the element's scale lives - baked into
    // the bounds, or left in the transform-table slot - must not change what it looks like: the cell is an absolute
    // length in slot units, so converting the SDF to pixels without converting the cell would resize the checkers.
    // Rendering it both ways and comparing the pixels is the direct test of that conversion.
    [Test]
    public void PatternFill_LooksTheSame_WhetherTheScaleIsBakedOrInTheSlot()
    {
        byte[] Render(bool motionNode)
        {
            var factory = new RenderUnitFactory(GpuTestDevice.Device, new StubResourceFactory());
            using var renderer = new OffscreenTestRenderer(GpuTestDevice.Device, factory, Dim, Dim) { ClearColor = Colors.Transparent };

            var brush = new PatternBrush
            {
                Pattern = PatternType.Checkerboard,
                Color1 = Colors.Red,
                Color2 = Colors.Blue,
                CellSize = 4
            };
            // The same 100x40 box either way: as a plain 100-wide draw, or as a 1-wide draw with ScaleX=100 in the slot.
            var box = new TestControl
            {
                RenderAction = s => s.DrawRectangle(brush, new Rect(0, 0, motionNode ? 1 : 100, 40)),
                RenderTransform = new Transform { ScaleX = motionNode ? 100 : 1, TranslateX = 20, TranslateY = 20 }
            };
            box.Bounds = new Rect(0, 0, motionNode ? 1 : 100, 40);
            box.RenderSize = new Size(motionNode ? 1 : 100, 40);
            box.IsRenderMotionNode = motionNode;

            var root = new VisualRoot(box, Dim, Dim);
            Assert.That(renderer.RenderFrame(root), Is.True);

            using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
            var bytes = new byte[(int)img.TotalSizeInBytes];
            Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
            return bytes;
        }

        var baked = Render(motionNode: false);
        var slotted = Render(motionNode: true);

        var differing = 0;
        for (var i = 0; i < baked.Length; i += 4)
            if (Math.Abs(baked[i] - slotted[i]) > 8 || Math.Abs(baked[i + 1] - slotted[i + 1]) > 8 ||
                Math.Abs(baked[i + 2] - slotted[i + 2]) > 8 || Math.Abs(baked[i + 3] - slotted[i + 3]) > 8)
                differing++;
        TestContext.WriteLine($"pattern: differing pixels {differing} of {baked.Length / 4}");

        Assert.That(differing, Is.LessThanOrEqualTo(baked.Length / 4 / 200), "the pattern must not depend on where the scale lives");
    }

    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, System.Collections.Generic.IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();
        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
