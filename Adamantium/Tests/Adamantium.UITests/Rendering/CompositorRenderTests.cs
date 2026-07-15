using System;
using System.Threading;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering;
using Adamantium.UI.Rendering.RenderUnits;
using Adamantium.Vulkan.Core;
using NUnit.Framework;
using Image = Adamantium.Imaging.Image;

namespace Adamantium.UITests.Rendering;

// The claim the compositor is FOR, tested where it can't be faked: on the GPU, with the loop thread doing nothing at all.
//
// Every headless test of the compositor can only show that some numbers changed. This one shows that the PIXELS moved - the
// production RenderCache, the real transform table, the retained instances - while AnimationManager was never ticked once.
// That is the whole promise: a theme cascade holds the loop thread for a second, and the spinner keeps turning anyway.
[TestFixture]
public class CompositorRenderTests
{
    private IGraphicsDevice _device;

    [SetUp]
    public void SetUp() => _device = GpuTestDevice.Device;

    [TearDown]
    public void TearDown() => AnimationManager.Reset();

    [Test]
    public void TheRenderThreadMovesTheElementWhileTheLoopThreadDoesNothing()
    {
        var factory = new RenderUnitFactory(_device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(_device, factory, 64, 64) { ClearColor = Colors.CornflowerBlue };

        // A 16x16 red square at the left edge, which the animation slides 32 px to the right over one second.
        var root = new TestRoot(64, 64);
        var control = new TestControl
        {
            RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 16, 16))
        };
        control.Bounds = new Rect(0, 24, 16, 16);
        control.RenderSize = new Size(16, 16);

        var transform = new Transform();
        control.RenderTransform = transform;
        root.Add(control);

        var slide = new Animation
        {
            Duration = TimeSpan.FromSeconds(1),
            KeyFrames =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter("TranslateX", 0.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter("TranslateX", 32.0) } }
            }
        };
        slide.Apply(transform);

        Assert.That(Compositor.EntryFor(transform), Is.Not.Null, "the slide must be composited");

        Assert.That(renderer.RenderFrame(root), Is.True);
        var first = Sample(renderer, out var bg);

        // The loop thread is STALLED here: no AnimationManager.Tick, no layout, no re-record of our own doing. Only time
        // passes - and the only thing that may act on it is the render thread.
        Thread.Sleep(400);

        Assert.That(renderer.RenderFrame(root), Is.True);
        var second = Sample(renderer, out _);

        Assert.That(first.Left, Is.Not.EqualTo(bg), "the square starts at the left");
        Assert.That(first.Right, Is.EqualTo(bg), "...and nothing is on the right yet");

        Assert.That(second.Right, Is.Not.EqualTo(bg),
            "after 400 ms the render thread must have slid the square right - with the loop thread never ticked");

        // The square has visibly moved ~13 px, yet the PROPERTY is still where the animation started (it is written only by
        // a loop tick, and there was none). So nothing but the compositor can have moved those pixels - which is the claim.
        Assert.That(transform.TranslateX, Is.LessThan(1.0),
            "the property must NOT have advanced: only the render thread moved the square");
    }

    [Test]
    public void ItTurnsTheElementAboutItsOwnCentre_NotTheWindowOrigin()
    {
        // The compositor composes the element's matrix ITSELF - render transform about the origin, then the layout offset.
        // Get that composition wrong and the spinner does not merely stutter: it flies to the corner of the window, or turns
        // about a point it does not own. A rotating BAR pins it - a square would look the same whatever it turned about.
        var factory = new RenderUnitFactory(_device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(_device, factory, 64, 64) { ClearColor = Colors.CornflowerBlue };

        var root = new TestRoot(64, 64);
        var control = new TestControl
        {
            RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 24, 8)),
            RenderTransformOrigin = new Vector2(0.5, 0.5)   // its own centre
        };
        control.Bounds = new Rect(20, 28, 24, 8);           // a 24x8 bar centred at (32,32) - NOT at the window origin
        control.RenderSize = new Size(24, 8);

        var transform = new Transform();
        control.RenderTransform = transform;
        root.Add(control);

        // A full turn every FOUR seconds, so a quarter turn is a whole second away - far longer than a frame takes to
        // render and read back. The wait below then lands on the angle itself rather than on a hoped-for stopwatch reading.
        new Animation
        {
            Duration = TimeSpan.FromSeconds(4),
            IterationCount = double.PositiveInfinity,
            KeyFrames =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter("RotationAngle", 0.0) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter("RotationAngle", 360.0) } }
            }
        }.Apply(transform);

        // Horizontal: (22,32) is inside the bar, (32,22) is above it.
        Assert.That(renderer.RenderFrame(root), Is.True);
        var flat = Probe(renderer, out var bg);
        Assert.That(flat.LeftOfCentre, Is.Not.EqualTo(bg), "the bar starts horizontal");
        Assert.That(flat.AboveCentre, Is.EqualTo(bg));

        // Wait for the ANGLE, not for a duration. Rendering a frame and reading it back costs a couple of hundred
        // milliseconds here, and pinning the test to a sleep instead of to the thing it asserts about is how a test starts
        // failing on a slower machine for no reason. The loop thread is still never ticked.
        var entry = Compositor.EntryFor(transform);
        while (entry.Elapsed < 1.0) Thread.Sleep(5);   // a quarter of a four-second turn = 90 degrees

        Assert.That(renderer.RenderFrame(root), Is.True);
        var upright = Probe(renderer, out _);
        Assert.That(upright.AboveCentre, Is.Not.EqualTo(bg), "a quarter turn later the bar stands upright");
        Assert.That(upright.LeftOfCentre, Is.EqualTo(bg), "...and no longer reaches to its left");
    }

    [Test]
    public void TheRenderThreadRepaintsABrushOpacityWhileTheLoopThreadDoesNothing()
    {
        // The skeleton pulse, proven on the GPU: a shared brush's Opacity animates, and the units painting with it must be
        // re-baked - all on the render thread. Nothing about the element changes; the colour lives entirely in the brush.
        var factory = new RenderUnitFactory(_device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(_device, factory, 64, 64) { ClearColor = Colors.CornflowerBlue };

        // A wide opacity swing so the blended pixel shifts visibly; a 4-second period so a frame's render+readback (~200 ms)
        // is a small fraction of it and the two samples land at clearly different opacities.
        var brush = new SolidColorBrush(Colors.Red) { Opacity = 0.1 };
        var root = new TestRoot(64, 64);
        var control = new TestControl { RenderAction = s => s.DrawRectangle(brush, new Rect(0, 0, 40, 40)) };
        control.Bounds = new Rect(12, 12, 40, 40);
        control.RenderSize = new Size(40, 40);
        root.Add(control);

        new PulseAnimation { Property = "Opacity", Min = 0.1, Max = 0.9, Duration = System.TimeSpan.FromSeconds(4) }.Apply(brush);
        Assert.That(Compositor.EntryFor(brush), Is.Not.Null, "a brush-opacity pulse must be composited");

        Assert.That(renderer.RenderFrame(root), Is.True);
        var early = Centre(renderer);

        // Wait for the opacity to climb toward its peak - by ELAPSED, not a fixed sleep, so the test isn't hostage to how
        // long a frame takes. The loop thread is never ticked.
        var entry = Compositor.EntryFor(brush);
        while (entry.Elapsed < 1.8) Thread.Sleep(5);   // ~0.9 of a 4-second pulse to Max: near the top

        Assert.That(renderer.RenderFrame(root), Is.True);
        var high = Centre(renderer);

        Assert.That(high, Is.Not.EqualTo(early),
            "a higher brush opacity must blend a redder pixel - re-baked by the render thread with the loop thread idle");
        Assert.That(brush.Opacity, Is.EqualTo(0.1),
            "and the LIVE brush opacity is untouched: paint is not mirrored, so only the compositor changed the picture");
    }

    private static uint Centre(OffscreenTestRenderer renderer)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"comp_{Guid.NewGuid():N}.png");
        renderer.Save(path, ImageFileType.Png);
        try { return Image.Load(path).GetPixelBuffer(0, 0).GetPixel<uint>(32, 32); }
        finally { System.IO.File.Delete(path); }
    }

    private static (uint LeftOfCentre, uint AboveCentre) Probe(OffscreenTestRenderer renderer, out uint background)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"comp_{Guid.NewGuid():N}.png");
        renderer.Save(path, ImageFileType.Png);
        try
        {
            var px = Image.Load(path).GetPixelBuffer(0, 0);
            background = px.GetPixel<uint>(2, 2);
            return (px.GetPixel<uint>(22, 32), px.GetPixel<uint>(32, 22));
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    // Two probe points: inside the square's starting place, and where it should be once it has slid.
    private static (uint Left, uint Right) Sample(OffscreenTestRenderer renderer, out uint background)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"comp_{Guid.NewGuid():N}.png");
        renderer.Save(path, ImageFileType.Png);
        try
        {
            var px = Image.Load(path).GetPixelBuffer(0, 0);
            background = px.GetPixel<uint>(2, 2);
            return (px.GetPixel<uint>(8, 32), px.GetPixel<uint>(20, 32));
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    // The unit factory needs one, but nothing in this test draws a texture or text.
    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();
        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
