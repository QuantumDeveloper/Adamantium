using System;
using System.Collections.Generic;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
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
using Adamantium.UI.Rendering.Retained;
using Adamantium.UI.Rendering.RenderUnits;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

// The LIVE general-geometry instancer: N identical Path/Polygon fills must collapse into ONE shared mesh drawn as ONE
// instanced call, in their natural z-layer. (It replaced the InstanceBuffer/GeometryInstanceRegistry design, whose tests
// were deleted with it - this fixture is the coverage for what actually ships.)
//
// Needs a real device: a segment allocates its mesh + instance buffers on the GPU. Only the COLLECTION side is asserted -
// Flush issues real draws and so needs a live command buffer/render pass.
[TestFixture]
[Category("Gpu")]
public class InstancedFillCollectorTests
{
    private IGraphicsDevice _device;
    private UIBasicEffect _effect;
    private readonly StubResourceFactory _resourceFactory = new();

    private static readonly Rect2D NoScissor = new() { Offset = new Offset2D(), Extent = new Extent2D { Width = 1000, Height = 1000 } };

    [OneTimeSetUp]
    public void CreateDevice()
    {
        _device = GpuTestDevice.Device;   // shared: a second device in the process kills the test host
        _effect = new UIBasicEffect(_device);
    }

    private GeometryRenderUnit Unit(Brush brush, double size)
    {
        var geometry = new RectangleGeometry(new Rect(0, 0, size, size), new CornerRadius(0));
        var component = new TestControl();
        var command = new DrawCommand(component, component.RenderId, new GeometryPayload(brush, geometry),
            new RenderData(1f, Matrix4x4F.Identity, false, default));
        return new GeometryRenderUnit(command,
            new RenderUnitContext(_device, _resourceFactory, (UIBasicEffect)_effect.Clone(), null, null, new GpuBufferManager(_device)));
    }

    private static GeometryKey KeyOf(GeometryRenderUnit unit)
    {
        Assert.That(unit.TryGetInstancedFill(out var key, out _, out _), Is.True, "the unit must expose an instanceable fill");
        return key;
    }

    private InstancedFillCollector NewCollector()
    {
        var collector = new InstancedFillCollector(_device, new GpuBufferManager(_device));
        collector.BeginFrame();
        return collector;
    }

    // The headline contract: same LOCAL geometry => same key => ONE segment holding N instances (one instanced draw),
    // instead of N meshes and N draws. This is the whole reason the collector exists.
    [Test]
    public void IdenticalGeometry_CollapsesToOneSegment_WithManyInstances()
    {
        using var collector = NewCollector();
        var units = new List<GeometryRenderUnit>();
        for (var i = 0; i < 50; i++) units.Add(Unit(Brushes.Red, 10));
        var key = KeyOf(units[0]);

        foreach (var unit in units)
            Assert.That(collector.TryAdd(unit, Matrix4x4F.Identity, NoScissor, new Rect(0, 0, 10, 10), transformSlot: 0), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(collector.SegmentCount, Is.EqualTo(1), "50 identical shapes must share ONE mesh segment");
            Assert.That(collector.InstanceCountOf(key), Is.EqualTo(50), "...holding all 50 instances");
            Assert.That(collector.PendingKeyCount, Is.EqualTo(1), "one key pending for this clip group");
            Assert.That(collector.Active, Is.True);
        });

        foreach (var unit in units) unit.Dispose();
    }

    // Different local geometry must NOT merge (a fingerprint that collapsed two shapes would draw one of them as the
    // other), so a differently-sized rect gets its own key and its own segment.
    [Test]
    public void DifferentGeometry_GetsItsOwnSegment()
    {
        using var collector = NewCollector();
        var small = Unit(Brushes.Red, 10);
        var large = Unit(Brushes.Red, 40);

        Assert.That(KeyOf(small), Is.Not.EqualTo(KeyOf(large)), "different local geometry must not share a key");

        collector.TryAdd(small, Matrix4x4F.Identity, NoScissor, new Rect(0, 0, 10, 10), transformSlot: 0);
        collector.TryAdd(large, Matrix4x4F.Identity, NoScissor, new Rect(0, 0, 40, 40), transformSlot: 0);

        Assert.That(collector.SegmentCount, Is.EqualTo(2));
        Assert.That(collector.InstanceCountOf(KeyOf(small)), Is.EqualTo(1));
        Assert.That(collector.InstanceCountOf(KeyOf(large)), Is.EqualTo(1));

        small.Dispose();
        large.Dispose();
    }

    // The COLOUR is per-instance, not part of the identity: two same-shape fills in different colours still share the one
    // mesh (that is what makes a grid of differently-coloured tiles a single draw).
    [Test]
    public void SameShapeDifferentColour_StillSharesOneSegment()
    {
        using var collector = NewCollector();
        var red = Unit(Brushes.Red, 10);
        var blue = Unit(Brushes.Blue, 10);

        Assert.That(KeyOf(red), Is.EqualTo(KeyOf(blue)), "colour is per-instance - it must not split the mesh");

        collector.TryAdd(red, Matrix4x4F.Identity, NoScissor, new Rect(0, 0, 10, 10), transformSlot: 0);
        collector.TryAdd(blue, Matrix4x4F.Identity, NoScissor, new Rect(0, 0, 10, 10), transformSlot: 0);

        Assert.That(collector.SegmentCount, Is.EqualTo(1));
        Assert.That(collector.InstanceCountOf(KeyOf(red)), Is.EqualTo(2));

        red.Dispose();
        blue.Dispose();
    }

    // Only a SOLID fill instances here. A gradient goes to the parallel gradient batch, and an image/null fill draws
    // per-unit - so CanBatch must say no rather than silently dropping the shape's real appearance.
    [Test]
    public void OnlySolidFill_CanBatch()
    {
        using var collector = NewCollector();
        var solid = Unit(Brushes.Red, 10);
        var gradient = Unit(new LinearGradientBrush(), 10);

        Assert.Multiple(() =>
        {
            Assert.That(collector.CanBatch(solid), Is.True, "a solid arbitrary-geometry fill instances");
            Assert.That(collector.CanBatch(gradient), Is.False, "a gradient fill does NOT join the solid batch");
            Assert.That(collector.TryAdd(gradient, Matrix4x4F.Identity, NoScissor, new Rect(0, 0, 10, 10), transformSlot: 0), Is.False,
                "a rejected fill must report false so the caller draws it per-unit");
        });

        solid.Dispose();
        gradient.Dispose();
    }

    // Paint order: the collected fills are drawn at the FLUSH, so a later non-batched unit that OVERLAPS them must force
    // a flush first or it would be painted under them. The overlap predicate is what the walk asks.
    [Test]
    public void OverlapsPending_IsTrue_OnlyForSomethingOverThePendingGroup()
    {
        using var collector = NewCollector();
        var unit = Unit(Brushes.Red, 10);

        Assert.That(collector.OverlapsPending(new Rect(0, 0, 10, 10)), Is.False, "nothing collected yet -> nothing to paint over");

        collector.TryAdd(unit, Matrix4x4F.Identity, NoScissor, new Rect(10, 10, 20, 20), transformSlot: 0);

        Assert.Multiple(() =>
        {
            Assert.That(collector.OverlapsPending(new Rect(20, 20, 20, 20)), Is.True, "overlapping -> must flush before drawing on top");
            Assert.That(collector.OverlapsPending(new Rect(100, 100, 5, 5)), Is.False, "disjoint -> no flush needed");
        });

        unit.Dispose();
    }

    // The instance buffer grows ONLY at BeginFrame (the safe point, after the frame fence - growing it mid-frame would
    // free a buffer the GPU is still reading). So a frame that overflows must fall back to a per-unit draw for the surplus
    // - never drop it - and the NEXT frame must have room. This is the whole growth protocol.
    [Test]
    public void InstanceBufferFull_FallsBackPerUnit_AndGrowsOnTheNextFrame()
    {
        using var collector = NewCollector();
        var units = new List<GeometryRenderUnit>();
        for (var i = 0; i < 70; i++) units.Add(Unit(Brushes.Red, 10));
        var key = KeyOf(units[0]);

        var acceptedFirstFrame = 0;
        foreach (var unit in units)
            if (collector.TryAdd(unit, Matrix4x4F.Identity, NoScissor, new Rect(0, 0, 10, 10), transformSlot: 0)) acceptedFirstFrame++;

        var capacityFirstFrame = collector.GpuCapacityOf(key);
        Assert.Multiple(() =>
        {
            Assert.That(acceptedFirstFrame, Is.EqualTo(capacityFirstFrame),
                "a frame accepts exactly what the instance buffer holds; the surplus is refused (drawn per-unit), not dropped");
            Assert.That(acceptedFirstFrame, Is.LessThan(70), "70 instances must not fit the initial buffer - this is the overflow case");
        });

        // Next frame: the buffer is grown at the safe point, so the whole set now fits.
        collector.BeginFrame();
        Assert.That(collector.InstanceCountOf(key), Is.EqualTo(0), "BeginFrame resets the per-frame accumulation");

        var acceptedSecondFrame = 0;
        foreach (var unit in units)
            if (collector.TryAdd(unit, Matrix4x4F.Identity, NoScissor, new Rect(0, 0, 10, 10), transformSlot: 0)) acceptedSecondFrame++;

        Assert.Multiple(() =>
        {
            Assert.That(collector.GpuCapacityOf(key), Is.GreaterThan(capacityFirstFrame), "the buffer grew at BeginFrame");
            Assert.That(acceptedSecondFrame, Is.EqualTo(70), "the whole set now instances");
            Assert.That(collector.SegmentCount, Is.EqualTo(1), "growth must not split the shared mesh");
        });

        foreach (var unit in units) unit.Dispose();
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
