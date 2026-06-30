using System;
using System.Linq;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Effects.Generated;
using Adamantium.UI.Rendering;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.UI.Rendering.RenderUnits;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

// Variant B: integration tests against a REAL render device (the engine's normal CreateRenderDevice; there
// is no dedicated headless device yet). No swapchain/surface/window and no presentation are involved - we
// only CONSTRUCT render units (device-level work: buffers, effect) and assert their CPU-side state, so a
// presenter is never needed. This exercises the actual RenderUnit update path that variant A can't reach
// with a fake factory (e.g. that a pen change really builds a StrokeRenderer). Tagged [Category("Gpu")] so
// it can be filtered out where no Vulkan device is available.
[TestFixture]
[Category("Gpu")]
public class GpuRenderUnitTests
{
    private MainGraphicsDevice _main;
    private IGraphicsDevice _device;
    private UIBasicEffect _effect;
    private readonly StubResourceFactory _resourceFactory = new();

    private static readonly Rect BoxA = new Rect(0, 0, 10, 10);
    private static readonly Rect BoxB = new Rect(0, 0, 40, 40);

    [OneTimeSetUp]
    public void CreateDevice()
    {
        _main = MainGraphicsDevice.Create(new GraphicsDeviceFactory(), 3, "UITests", true);
        _device = _main.CreateRenderDevice();
        _effect = new UIBasicEffect(_device);
    }

    [OneTimeTearDown]
    public void DisposeDevice()
    {
        _main?.Dispose();
    }

    private static RectanglePayload Rect(Brush brush, Rect box, Pen pen) =>
        new RectanglePayload(brush, box, new CornerRadius(0), pen);

    private static IDrawCommand Command(object payload, float opacity = 1f)
    {
        var component = new TestControl();
        var renderData = new RenderData(opacity, Matrix4x4F.Identity, false, default);
        return new Adamantium.UI.Rendering.DrawCommand(component, component.RenderId, payload, renderData);
    }

    // Bundles the per-test render services into the RenderUnitContext every unit now takes. A fresh GpuBufferManager
    // per unit is fine - it's a lightweight context and each component owns/disposes its own rented handles.
    private RenderUnitContext Ctx(UIBasicEffect ui, StrokeEffect stroke = null) =>
        new(_device, _resourceFactory, ui, stroke, null, new GpuBufferManager(_device));

    // StrokeEffect is null on purpose: these tests exercise the CPU stroke path (StrokeRenderComponent). With a null
    // GPU stroke effect ProcessStrokeData falls back to CPU, so the unit's update/dispose behaviour is what's asserted.
    private RectangleRenderUnit NewRectUnit(RectanglePayload payload) =>
        new RectangleRenderUnit(Command(payload), Ctx((UIBasicEffect)_effect.Clone()));

    [Test]
    public void Pen_Added_BuildsStroke()
    {
        var unit = NewRectUnit(Rect(Brushes.Red, BoxA, pen: null));
        Assert.That(unit.StrokeRenderer, Is.Null, "no pen -> no stroke");

        unit.UpdateWithDrawCommand(Command(Rect(Brushes.Red, BoxA, new Pen(Brushes.Black, 2))));
        Assert.That(unit.StrokeRenderer, Is.Not.Null, "pen added must build a stroke (the #7 fix)");
    }

    [Test]
    public void Resize_WithPen_RebuildsStroke_ToFollowGeometry()
    {
        var pen = new Pen(Brushes.Black, 2);
        var unit = NewRectUnit(Rect(Brushes.Red, BoxA, pen));
        var strokeBefore = unit.StrokeRenderer;
        Assert.That(strokeBefore, Is.Not.Null);

        // Resize (geometry rebuild) with the same pen: the stroke wraps the geometry, so it must be rebuilt.
        unit.UpdateWithDrawCommand(Command(Rect(Brushes.Red, BoxB, pen)));
        Assert.That(unit.StrokeRenderer, Is.Not.Null);
        Assert.That(unit.StrokeRenderer, Is.Not.SameAs(strokeBefore), "stroke must follow the new geometry");
    }

    [Test]
    public void Stroke_UniformOnlyPenChange_Repoints_NoRebuild()
    {
        // Real StrokeEffect -> the GPU stroke path (GpuStrokeRenderComponent), where TryRepoint lives.
        var strokeEffect = new StrokeEffect(_device);
        var unit = new RectangleRenderUnit(Command(Rect(Brushes.Red, BoxB, new Pen(Brushes.Black, 2))),
            Ctx((UIBasicEffect)_effect.Clone(), strokeEffect));
        var stroke = unit.StrokeRenderer;
        Assert.That(stroke, Is.InstanceOf<GpuStrokeRenderComponent>(), "solid pen + StrokeEffect -> GPU stroke");

        // Thickness is a GPU uniform; buffer sizes don't change -> the SAME component is repointed, no per-frame realloc
        // (the stroke-animation optimisation: dash offset / thickness / colour / trim all take this path).
        unit.UpdateWithDrawCommand(Command(Rect(Brushes.Red, BoxB, new Pen(Brushes.Black, 7))));
        Assert.That(unit.StrokeRenderer, Is.SameAs(stroke), "thickness-only pen change must repoint, not rebuild");

        // A join change resizes the corner fans -> the buffer sizes differ, so it must rebuild (new component).
        unit.UpdateWithDrawCommand(Command(Rect(Brushes.Red, BoxB, new Pen(Brushes.Black, 7, penLineJoin: PenLineJoin.Round))));
        Assert.That(unit.StrokeRenderer, Is.Not.SameAs(stroke), "join change resizes buffers -> must rebuild");
    }

    [Test]
    public void Resize_GpuStroke_SameTopology_UpdatesInPlace_NoRebuild()
    {
        // Real StrokeEffect -> the GPU stroke path. A rectangle keeps its corner topology across sizes, so a resize
        // rewrites the stroke's points into the reused buffers (TryUpdateGeometry) instead of building a new component
        // (and a new allocation) every frame - the resize/animation fast path the buffer-reuse work added.
        var strokeEffect = new StrokeEffect(_device);
        var unit = new RectangleRenderUnit(Command(Rect(Brushes.Red, BoxA, new Pen(Brushes.Black, 2))),
            Ctx((UIBasicEffect)_effect.Clone(), strokeEffect));
        var stroke = unit.StrokeRenderer;
        Assert.That(stroke, Is.InstanceOf<GpuStrokeRenderComponent>(), "solid pen + StrokeEffect -> GPU stroke");

        unit.UpdateWithDrawCommand(Command(Rect(Brushes.Red, BoxB, new Pen(Brushes.Black, 2))));
        Assert.That(unit.StrokeRenderer, Is.SameAs(stroke), "same-topology resize updates the GPU stroke in place");
    }

    [Test]
    public void Line_DashOffsetOnlyChange_Repoints_NoRebuild()
    {
        var strokeEffect = new StrokeEffect(_device);
        IDrawCommand LineCmd(double dashOffset, double endX) =>
            Command(new LinePayload(new Vector2(0, 0), new Vector2((float)endX, 50),
                new Pen(Brushes.Black, 3, dashOffset, new double[] { 6, 4 })));

        var unit = new LineRenderUnit(LineCmd(0, 100), Ctx((UIBasicEffect)_effect.Clone(), strokeEffect));
        var stroke = unit.StrokeRenderer;
        Assert.That(stroke, Is.InstanceOf<GpuStrokeRenderComponent>(), "solid dashed line -> GPU stroke");

        // Same endpoints, only the dash offset animates -> repoint, no per-frame buffer rebuild (the reported case).
        unit.UpdateWithDrawCommand(LineCmd(20, 100));
        Assert.That(unit.StrokeRenderer, Is.SameAs(stroke), "dash-offset-only change on a line must repoint, not rebuild");

        // Endpoint move changes the ribbon -> must rebuild.
        unit.UpdateWithDrawCommand(LineCmd(20, 130));
        Assert.That(unit.StrokeRenderer, Is.Not.SameAs(stroke), "endpoint move must rebuild");
    }

    [Test]
    public void ColourOnlyChange_KeepsGeometryRenderer_NoRebuild()
    {
        var unit = NewRectUnit(Rect(Brushes.Red, BoxA, pen: null));
        var geomBefore = unit.GeometryRenderer;

        unit.UpdateWithDrawCommand(Command(Rect(Brushes.Blue, BoxA, pen: null)));
        Assert.That(unit.GeometryRenderer, Is.SameAs(geomBefore), "colour-only change must not rebuild the buffer");
    }

    [Test]
    public void HeadlessPresenter_BuildsSwapchain_WhenExtensionAvailable()
    {
        // VK_EXT_headless_surface is loader-provided and absent on some loaders (e.g. this dev box, loader
        // 1.4.321). Skip cleanly where it's unavailable; validate the window-less swapchain where it exists.
        var hasExt = Adamantium.Vulkan.Core.Instance.EnumerateInstanceExtensionProperties()
            .Any(e => e.ExtensionName == "VK_EXT_headless_surface");
        if (!hasExt)
            Assert.Ignore("VK_EXT_headless_surface not advertised by this Vulkan loader - headless-swapchain path unavailable here.");

        var parameters = new PresentationParameters(PresenterType.Headless, 256, 256, IntPtr.Zero);
        var presenter = GraphicsPresenter.Create(_device, parameters, "test_headless");

        Assert.That(presenter, Is.InstanceOf<HeadlessPresenter>());
        Assert.That(presenter.RenderTarget, Is.Not.Null, "headless swapchain must expose a render target");
        Assert.That(presenter.GetCurrentImage(), Is.Not.Null);

        presenter.Dispose();
    }

    [Test]
    public void OffscreenRenderer_RendersFrame_AndReadsBackAnImage()
    {
        var factory = new RenderUnitFactory(_device, _resourceFactory);
        using var renderer = new OffscreenTestRenderer(_device, factory, 64, 64);

        // Headless extension is absent on this box -> RenderTarget fallback (would be Headless on Linux/Mesa).
        TestContext.WriteLine($"Offscreen presenter kind = {renderer.PresenterKind}");

        var root = new TestRoot(64, 64);
        // Two differently-coloured rects, separated horizontally and centred vertically (so the sample points
        // are robust to a Y flip). This verifies PER-DRAW isolation: each draw must keep its own colour - the
        // exact thing that removing the per-unit effect clone (and the state cache) could break.
        var control = new TestControl
        {
            RenderAction = s => s
                .DrawRectangle(Brushes.Red, new Rect(4, 20, 24, 24))
                .DrawRectangle(Brushes.Green, new Rect(36, 20, 24, 24))
        };
        root.Add(control);

        renderer.ClearColor = Colors.CornflowerBlue;
        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"offscreen_{System.Guid.NewGuid():N}.png");
        renderer.Save(path, ImageFileType.Png);
        try
        {
            var img = Adamantium.Imaging.Image.Load(path);
            var px = img.GetPixelBuffer(0, 0);
            var left = px.GetPixel<uint>(16, 32);  // inside the first (red) rect
            var right = px.GetPixel<uint>(48, 32); // inside the second (green) rect
            var bg = px.GetPixel<uint>(2, 2);      // cleared background
            TestContext.WriteLine($"left=0x{left:X8} right=0x{right:X8} bg=0x{bg:X8}");

            Assert.That(bg, Is.Not.EqualTo(0u), "background must be cleared to a visible colour");
            Assert.That(left, Is.Not.EqualTo(bg), "first rect must render over the background");
            Assert.That(right, Is.Not.EqualTo(bg), "second rect must render over the background");
            Assert.That(left, Is.Not.EqualTo(right), "the two rects must keep their distinct per-draw colours");
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    // Renderer clipping: a child whose draw OVERFLOWS its ClipToBounds parent must be scissored to the parent's
    // bounds. This is the foundation the ScrollViewer (and ContentControl/Presenter transitions) need - without the
    // per-unit scissor the overflow bleeds across the whole window. Sample points are chosen to read the same way
    // under a vertical read-back flip (centre row/column, and Y points that are background in either orientation).
    [Test]
    public void ClipToBounds_Parent_ScissorsOverflowingChild()
    {
        var factory = new RenderUnitFactory(_device, _resourceFactory);
        using var renderer = new OffscreenTestRenderer(_device, factory, 64, 64) { ClearColor = Colors.CornflowerBlue };

        // A 24x24 clipping window at (20,20); inside it a child whose 64x64 red draw overflows it on all sides.
        // Geometry is set directly (Bounds/RenderSize, the inputs WorldTransform + the scissor read) so the test
        // doesn't depend on a layout pass - the window spans x,y 20..44 and the child's red draw shares that origin.
        var child = new TestControl
        {
            RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 64, 64)),
            Bounds = new Rect(0, 0, 64, 64),
            RenderSize = new Size(64, 64)
        };
        var clip = new TestControl
        {
            ClipToBounds = true,
            Bounds = new Rect(20, 20, 24, 24),
            RenderSize = new Size(24, 24)
        };
        clip.Add(child);

        var root = new TestRoot(64, 64);
        root.Add(clip);

        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"clip_{System.Guid.NewGuid():N}.png");
        renderer.Save(path, ImageFileType.Png);
        try
        {
            var img = Adamantium.Imaging.Image.Load(path);
            var px = img.GetPixelBuffer(0, 0);
            var inside = px.GetPixel<uint>(32, 32);        // centre of the clip window -> red survives
            var clippedRight = px.GetPixel<uint>(54, 32);  // within the red draw (x<64) but right of the window -> clipped
            var clippedV = px.GetPixel<uint>(32, 54);      // outside the window in Y -> clipped (flip-safe: row 9 is above the draw, also bg)
            var bg = px.GetPixel<uint>(2, 2);              // cleared background
            TestContext.WriteLine($"inside=0x{inside:X8} clippedRight=0x{clippedRight:X8} clippedV=0x{clippedV:X8} bg=0x{bg:X8}");

            Assert.That(bg, Is.Not.EqualTo(0u), "background must be a visible clear colour");
            Assert.That(inside, Is.Not.EqualTo(bg), "inside the clip window the child must render");
            Assert.That(clippedRight, Is.EqualTo(bg), "child overflow to the right of the window must be scissored away");
            Assert.That(clippedV, Is.EqualTo(bg), "child overflow past the window in Y must be scissored away");
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    // The framework adorner stage: a SelectionAdorner drawn as a SECOND pass on top of the content. Proves the
    // overlay renders over the content (white corner handle sits outside the red box, on a cleared background) -
    // i.e. RenderBounds-driven tooling frames from the framework, not the host. Raw BGRA read-back avoids Image.Load.
    [Test]
    public void Adorner_DrawsSelectionFrame_OnTopOfContent()
    {
        var factory = new RenderUnitFactory(_device, _resourceFactory);
        using var renderer = new OffscreenTestRenderer(_device, factory, 64, 64) { ClearColor = Colors.Black };

        var box = new Adamantium.UI.Controls.Shapes.Rectangle { Width = 40, Height = 40, Fill = Brushes.Red };
        box.Measure(new Size(64, 64));
        box.Arrange(new Rect(10, 10, 40, 40));

        var root = new TestRoot(64, 64);
        root.Add(box);

        // Selection frame on the box: drawn on top, a couple px outside its bounds, with white corner handles.
        renderer.Adorners = [new Adamantium.UI.Controls.Adorners.SelectionAdorner(box)];

        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"adorner_{System.Guid.NewGuid():N}.bgra");
        renderer.SaveRaw(path);
        try
        {
            var bytes = System.IO.File.ReadAllBytes(path);   // native B8G8R8A8, top-left origin (no Y flip)
            bool Black(int x, int y) { var o = (y * 64 + x) * 4; return bytes[o] < 40 && bytes[o + 1] < 40 && bytes[o + 2] < 40; }
            bool Red(int x, int y)   { var o = (y * 64 + x) * 4; return bytes[o] < 100 && bytes[o + 1] < 100 && bytes[o + 2] > 140; }
            bool White(int x, int y) { var o = (y * 64 + x) * 4; return bytes[o] > 180 && bytes[o + 1] > 180 && bytes[o + 2] > 180; }

            Assert.Multiple(() =>
            {
                Assert.That(Red(30, 30), Is.True, "content (red box) must render");
                Assert.That(Black(2, 2), Is.True, "background outside the adorned area stays cleared");
                Assert.That(White(15, 15), Is.True, "selection handle must render ON TOP, tucked inside the box corner");
            });
        }
        finally { System.IO.File.Delete(path); }
    }

    // An element that FILLS the surface (touches every edge): the selection chrome must be drawn INSIDE its bounds so it
    // stays within the window framebuffer. Regression: it used to inflate the frame + handles OUTSIDE the bounds, so for
    // an edge element they fell off-window and were clipped to a few corner specks (the designer's "no frame" bug).
    [Test]
    public void Adorner_OnEdgeTouchingElement_StaysInsideBounds_NotClipped()
    {
        var factory = new RenderUnitFactory(_device, _resourceFactory);
        using var renderer = new OffscreenTestRenderer(_device, factory, 64, 64) { ClearColor = Colors.Black };

        var box = new Adamantium.UI.Controls.Shapes.Rectangle { Width = 64, Height = 64, Fill = Brushes.Red };
        box.Measure(new Size(64, 64));
        box.Arrange(new Rect(0, 0, 64, 64));   // fills the surface -> touches every edge

        var root = new TestRoot(64, 64);
        root.Add(box);
        renderer.Adorners = [new Adamantium.UI.Controls.Adorners.SelectionAdorner(box)];
        Assert.That(renderer.RenderFrame(root), Is.True);

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"adorner_edge_{System.Guid.NewGuid():N}.bgra");
        renderer.SaveRaw(path);
        try
        {
            var bytes = System.IO.File.ReadAllBytes(path);   // B8G8R8A8, top-left origin
            // CornflowerBlue ~ (R100,G149,B237): high B, LOW R - distinct from white (high R) and red (low B).
            bool Frame(int x, int y) { var o = (y * 64 + x) * 4; return bytes[o] > 150 && bytes[o + 2] < 160 && bytes[o + 1] > 90; }
            bool White(int x, int y) { var o = (y * 64 + x) * 4; return bytes[o] > 180 && bytes[o + 1] > 180 && bytes[o + 2] > 180; }
            bool Red(int x, int y)   { var o = (y * 64 + x) * 4; return bytes[o] < 100 && bytes[o + 1] < 100 && bytes[o + 2] > 140; }

            bool AnyFrameInLeftStrip() { for (var x = 0; x < 6; x++) for (var y = 26; y < 38; y++) if (Frame(x, y)) return true; return false; }
            bool AnyWhiteInTopLeft()   { for (var x = 0; x < 10; x++) for (var y = 0; y < 10; y++) if (White(x, y)) return true; return false; }

            Assert.Multiple(() =>
            {
                Assert.That(AnyFrameInLeftStrip(), Is.True, "frame is drawn INSIDE the left edge, not clipped off-window");
                Assert.That(AnyWhiteInTopLeft(), Is.True, "a corner handle sits inside the top-left corner");
                Assert.That(Red(32, 32), Is.True, "the element still shows through the centre");
            });
        }
        finally { System.IO.File.Delete(path); }
    }

    // Multi-state oracle for the dynamic-state cache: two draws with DIFFERENT blend equations must each honour
    // their own blend. White @ 50% over black -> AlphaBlend gives grey, Premultiplied gives white. If the
    // state cache wrongly drops the blend change for the second draw, both come out identical.
    [Test]
    public void StateCache_PerDrawBlendChange_IsHonoured()
    {
        var prms = new PresentationParameters(PresenterType.RenderTarget, 64, 64, IntPtr.Zero);
        using var presenter = GraphicsPresenter.Create(_device, prms, "blend_test");

        var left = new RectangleRenderUnit(Command(Rect(Brushes.White, new Rect(4, 20, 24, 24), null), 0.5f),
            Ctx(_effect));
        left.GeometryRenderer.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        var right = new RectangleRenderUnit(Command(Rect(Brushes.White, new Rect(36, 20, 24, 24), null), 0.5f),
            Ctx(_effect));
        right.GeometryRenderer.ColorBlendEquation = ColorBlendEquations.Premultiplied;

        var proj = Matrix4x4F.OrthoOffCenter(0, 64, 0, 64, 0, 100000);
        left.Update(Matrix4x4F.Identity, proj, 1.0);
        right.Update(Matrix4x4F.Identity, proj, 1.0);

        var vp = new Viewport { Width = 64, Height = 64, MinDepth = 0, MaxDepth = 1 };
        var sc = new Rect2D { Offset = new Offset2D(), Extent = new Extent2D { Width = 64, Height = 64 } };

        _device.ClearColor = Colors.Black;
        _device.SetRenderTargets(presenter.RenderTarget);
        _device.SetDepthBuffer(presenter.DepthBuffer);
        _device.MSAALevel = presenter.MSAALevel;
        _device.Presenter = presenter;
        Assert.That(_device.BeginDraw(), Is.True);
        _device.SetViewports(vp);
        _device.SetScissors(sc);
        left.Render();
        right.Render();
        _device.EndDraw();
        _device.Submit();
        presenter.Present();
        _device.FrameEnded();
        _device.DeviceWaitIdle();

        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"blend_{System.Guid.NewGuid():N}.png");
        presenter.RenderTarget.Save(path, ImageFileType.Png);
        try
        {
            var img = Adamantium.Imaging.Image.Load(path);
            var px = img.GetPixelBuffer(0, 0);
            var alpha = px.GetPixel<uint>(16, 32);  // AlphaBlend -> grey
            var premul = px.GetPixel<uint>(48, 32); // Premultiplied -> white
            var bg = px.GetPixel<uint>(2, 2);       // black
            TestContext.WriteLine($"alphaBlend=0x{alpha:X8} premultiplied=0x{premul:X8} bg=0x{bg:X8}");

            Assert.That(alpha, Is.Not.EqualTo(bg), "alpha-blended rect must render");
            Assert.That(premul, Is.Not.EqualTo(bg), "premultiplied rect must render");
            Assert.That(alpha, Is.Not.EqualTo(premul), "different blend states must produce different results");
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    // Leak regression: every GraphicsResource used to be rooted in the device's resource set for its whole
    // lifetime (no removal on Dispose) - a steady memory leak as buffers/RTs churned. Disposing must now
    // unregister them.
    [Test]
    public void GraphicsResources_Unregister_OnDispose()
    {
        var device = (GraphicsDevice)_device;
        int before = device.RegisteredResourceCount;

        var verts = new Adamantium.Graphics.Core.Vertices.UIVertex[3];
        var buffers = new System.Collections.Generic.List<Adamantium.Graphics.Buffer>();
        for (int i = 0; i < 50; i++)
            buffers.Add(Adamantium.Graphics.Buffer.Vertex.New(_device, verts));

        Assert.That(device.RegisteredResourceCount, Is.EqualTo(before + 50), "resources must register on creation");

        foreach (var b in buffers) b.Dispose();

        Assert.That(device.RegisteredResourceCount, Is.EqualTo(before), "disposed resources must unregister (no leak)");
    }

    // Leak regression: disposing a unit must dispose its renderers (geometry + stroke). Previously the unit
    // dropped them, leaking their vertex/index buffers on every cache replace and window resize.
    [Test]
    public void RenderUnit_Dispose_FreesItsRenderers()
    {
        var device = (GraphicsDevice)_device;
        var before = device.RegisteredResourceCount;

        var unit = new RectangleRenderUnit(
            Command(Rect(Brushes.Red, new Rect(0, 0, 32, 32), new Pen(Brushes.Black, 2))),
            Ctx(_effect));
        Assert.That(device.RegisteredResourceCount, Is.GreaterThan(before), "unit creates geometry + stroke buffers");

        unit.Dispose();
        Assert.That(device.RegisteredResourceCount, Is.EqualTo(before),
            "disposing a unit must dispose its renderers' buffers (they leaked on every rebuild/resize)");
    }

    // Leak regression: an MSAA RenderTarget owns a resolve texture (a ToDispose'd child). Texture.Dispose did
    // not call base, so the resolve texture leaked on every window resize.
    [Test]
    public void RenderTarget_Dispose_FreesResolveTexture()
    {
        var device = (GraphicsDevice)_device;
        var before = device.RegisteredResourceCount;

        var rt = device.CreateRenderTarget(64, 64, MSAALevel.X4, SurfaceFormat.R8G8B8A8.UNorm);
        Assert.That(device.RegisteredResourceCount, Is.GreaterThan(before), "RT + its resolve texture register");

        rt.Dispose();
        Assert.That(device.RegisteredResourceCount, Is.EqualTo(before),
            "disposing a RenderTarget must also free its resolve texture (it leaked on every resize)");
    }

    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();
        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
