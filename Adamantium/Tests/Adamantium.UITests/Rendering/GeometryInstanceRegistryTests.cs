using System;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Rendering.Retained;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

// The retained geometry-instancing scene core (docs/RENDER_CACHE_REDESIGN.md §4h): interning by GeometryKey, so N
// identical shapes collapse to ONE segment of N instances; O(1) patch/remove; a shape change moves an element between
// segments. Pure CPU - no GPU.
[TestFixture]
public class GeometryInstanceRegistryTests
{
    private static readonly object Mesh = new();   // opaque shared-mesh stand-in (registry stores it as object)

    private static GeometryKey Tile(double size) =>
        GeometryKey.RoundedRectangle(new Rect(0, 0, size, size), new CornerRadius(4));

    private static GeometryInstance Inst(float tag) =>
        GeometryInstance.FromWorld(Matrix4x4F.Identity, new Vector4F(tag, 0, 0, 1));

    [Test]
    public void SameKey_CollapsesToOneSegment_WithNInstances()
    {
        var reg = new GeometryInstanceRegistry();
        for (var i = 0; i < 600; i++)
            reg.Set(Guid.NewGuid(), Tile(64), Mesh, Inst(i));

        Assert.Multiple(() =>
        {
            Assert.That(reg.SegmentCount, Is.EqualTo(1), "600 identical tiles = one shared segment (one instanced draw)");
            Assert.That(reg.InstanceCount, Is.EqualTo(600));
            Assert.That(reg.Segments.Single().Instances.Count, Is.EqualTo(600));
        });
    }

    [Test]
    public void DifferentKeys_AreSeparateSegments()
    {
        var reg = new GeometryInstanceRegistry();
        reg.Set(Guid.NewGuid(), Tile(64), Mesh, Inst(1));
        reg.Set(Guid.NewGuid(), Tile(128), Mesh, Inst(2));                                   // different size
        reg.Set(Guid.NewGuid(), GeometryKey.EllipseArc(new Rect(0, 0, 64, 64), 0, 360, 0), Mesh, Inst(3));

        Assert.That(reg.SegmentCount, Is.EqualTo(3));
        Assert.That(reg.InstanceCount, Is.EqualTo(3));
    }

    [Test]
    public void Set_SameKeyAgain_PatchesInPlace_NoNewInstance()
    {
        var reg = new GeometryInstanceRegistry();
        var id = Guid.NewGuid();
        reg.Set(id, Tile(64), Mesh, Inst(1));
        reg.Set(id, Tile(64), Mesh, Inst(9));   // same shape, new colour/transform -> patch, not add

        Assert.That(reg.InstanceCount, Is.EqualTo(1));
        Assert.That(reg.Segments.Single().Instances.Span[0].Color.X, Is.EqualTo(9f), "the instance was patched in place");
    }

    [Test]
    public void Patch_UpdatesTheRightInstance()
    {
        var reg = new GeometryInstanceRegistry();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        reg.Set(a, Tile(64), Mesh, Inst(1));
        reg.Set(b, Tile(64), Mesh, Inst(2));

        Assert.That(reg.Patch(a, Inst(7)), Is.True);
        Assert.That(reg.Patch(Guid.NewGuid(), Inst(0)), Is.False, "patching an unknown element is a no-op / false");

        var colors = reg.Segments.Single().Instances.Span.ToArray().Select(i => i.Color.X).OrderBy(x => x).ToArray();
        Assert.That(colors, Is.EqualTo(new[] { 2f, 7f }), "only element a's instance changed (1 -> 7)");
    }

    [Test]
    public void ShapeChange_MovesElementBetweenSegments()
    {
        var reg = new GeometryInstanceRegistry();
        var id = Guid.NewGuid();
        reg.Set(id, Tile(64), Mesh, Inst(1));
        Assert.That(reg.SegmentCount, Is.EqualTo(1));

        reg.Set(id, Tile(128), Mesh, Inst(1));   // shape grew -> different key -> moves segments

        Assert.Multiple(() =>
        {
            Assert.That(reg.InstanceCount, Is.EqualTo(1), "still exactly one instance - it MOVED, not duplicated");
            var counts = reg.Segments.Select(s => s.Instances.Count).OrderBy(c => c).ToArray();
            Assert.That(counts, Is.EqualTo(new[] { 0, 1 }), "old segment emptied, new one holds the instance");
        });
    }

    [Test]
    public void Remove_SwapRemove_KeepsOtherInstancesValid()
    {
        var reg = new GeometryInstanceRegistry();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        for (var i = 0; i < ids.Length; i++) reg.Set(ids[i], Tile(64), Mesh, Inst(i));

        reg.Remove(ids[1]);   // swap-remove: the last instance moves into slot 1

        Assert.That(reg.InstanceCount, Is.EqualTo(4));
        // Every surviving element must still patch to the correct slot (handles stayed valid across the swap).
        Assert.That(reg.Patch(ids[4], Inst(40)), Is.True);
        Assert.That(reg.Patch(ids[1], Inst(99)), Is.False, "the removed element is gone");
        var colors = reg.Segments.Single().Instances.Span.ToArray().Select(i => i.Color.X).OrderBy(x => x).ToArray();
        Assert.That(colors, Is.EqualTo(new[] { 0f, 2f, 3f, 40f }), "0,2,3 intact; 4 patched to 40; 1 removed");
    }

    [Test]
    public void TrimEmptySegments_DropsEmptied_ReturnsTheirMeshes()
    {
        var reg = new GeometryInstanceRegistry();
        var id = Guid.NewGuid();
        var meshA = new object();
        reg.Set(id, Tile(64), meshA, Inst(1));
        reg.Set(Guid.NewGuid(), Tile(128), new object(), Inst(2));

        reg.Remove(id);   // empties the size-64 segment (kept for reuse until trimmed)
        Assert.That(reg.SegmentCount, Is.EqualTo(2), "emptied segment is kept by default (mesh retained for recycling)");

        var dropped = reg.TrimEmptySegments();
        Assert.Multiple(() =>
        {
            Assert.That(reg.SegmentCount, Is.EqualTo(1), "trim dropped the empty segment");
            Assert.That(dropped, Is.EqualTo(new[] { meshA }), "trim returned the dropped mesh so the caller frees its GPU buffers");
        });
    }
}
