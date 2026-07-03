using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Rendering.Retained;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

// Foundation of the render-cache redesign (docs/RENDER_CACHE_REDESIGN.md §4h): the sparse-set instance store. Pure CPU
// logic, so these run in the normal (non-GPU) suite. The invariants that matter for a retained render scene: handles
// stay valid across a swap-remove of an UNRELATED instance, the dense array stays packed, and the GPU dirty range
// covers exactly the touched slots.
[TestFixture]
public class InstanceBufferTests
{
    private record struct Item(int Tag);

    [Test]
    public void Add_ReturnsDistinctHandles_AndPacks()
    {
        var buf = new InstanceBuffer<Item>();
        var h0 = buf.Add(new Item(10));
        var h1 = buf.Add(new Item(20));
        var h2 = buf.Add(new Item(30));

        Assert.Multiple(() =>
        {
            Assert.That(buf.Count, Is.EqualTo(3));
            Assert.That(new[] { h0, h1, h2 }, Is.Unique);
            Assert.That(buf.Get(h0).Tag, Is.EqualTo(10));
            Assert.That(buf.Get(h1).Tag, Is.EqualTo(20));
            Assert.That(buf.Get(h2).Tag, Is.EqualTo(30));
            Assert.That(buf.Span.ToArray().Select(i => i.Tag), Is.EquivalentTo(new[] { 10, 20, 30 }));
        });
    }

    [Test]
    public void Patch_UpdatesInstance()
    {
        var buf = new InstanceBuffer<Item>();
        var h = buf.Add(new Item(1));
        buf.Add(new Item(2));

        buf.Patch(h, new Item(99));

        Assert.That(buf.Get(h).Tag, Is.EqualTo(99));
        Assert.That(buf.Span.ToArray().Select(i => i.Tag), Is.EquivalentTo(new[] { 99, 2 }));
    }

    [Test]
    public void Remove_KeepsPacked_AndOtherHandlesStayValid()
    {
        var buf = new InstanceBuffer<Item>();
        var h0 = buf.Add(new Item(10));
        var h1 = buf.Add(new Item(20));   // this one we remove (a MIDDLE element)
        var h2 = buf.Add(new Item(30));   // this one moves into h1's slot on swap-remove

        buf.Remove(h1);

        Assert.Multiple(() =>
        {
            Assert.That(buf.Count, Is.EqualTo(2), "dense array shrinks");
            Assert.That(buf.IsAlive(h1), Is.False);
            Assert.That(buf.IsAlive(h0), Is.True);
            Assert.That(buf.IsAlive(h2), Is.True);
            // The KEY invariant: the SWAPPED instance keeps its handle -> data mapping.
            Assert.That(buf.Get(h0).Tag, Is.EqualTo(10));
            Assert.That(buf.Get(h2).Tag, Is.EqualTo(30));
            Assert.That(buf.Span.ToArray().Select(i => i.Tag), Is.EquivalentTo(new[] { 10, 30 }));
        });
    }

    [Test]
    public void Remove_Last_NoSwap()
    {
        var buf = new InstanceBuffer<Item>();
        var h0 = buf.Add(new Item(10));
        var h1 = buf.Add(new Item(20));

        buf.Remove(h1);   // removing the LAST element: no swap needed

        Assert.That(buf.Count, Is.EqualTo(1));
        Assert.That(buf.Get(h0).Tag, Is.EqualTo(10));
        Assert.That(buf.IsAlive(h1), Is.False);
    }

    [Test]
    public void Remove_Only_EmptiesCleanly()
    {
        var buf = new InstanceBuffer<Item>();
        var h = buf.Add(new Item(7));
        buf.Remove(h);
        Assert.That(buf.Count, Is.EqualTo(0));
        Assert.That(buf.IsAlive(h), Is.False);
        Assert.That(buf.Span.Length, Is.EqualTo(0));
    }

    [Test]
    public void RemoveThenAdd_RecyclesHandleSpace_WithoutCorruption()
    {
        var buf = new InstanceBuffer<Item>();
        var a = buf.Add(new Item(1));
        var b = buf.Add(new Item(2));
        buf.Remove(a);
        var c = buf.Add(new Item(3));

        // The freed handle id is RECYCLED (bounded handle space), so `c` re-uses `a`'s int - that is the intended
        // contract (an owner must not touch a handle after Remove; IsAlive is only meaningful for a LIVE handle, and
        // checking it on the recycled `a` would just report `c`). What must hold is: no corruption of the live set.
        Assert.Multiple(() =>
        {
            Assert.That(c, Is.EqualTo(a), "freed handle id is recycled - handle space stays bounded");
            Assert.That(buf.Count, Is.EqualTo(2));
            Assert.That(buf.Get(b).Tag, Is.EqualTo(2));
            Assert.That(buf.Get(c).Tag, Is.EqualTo(3));
            Assert.That(buf.Span.ToArray().Select(i => i.Tag), Is.EquivalentTo(new[] { 2, 3 }));
        });
    }

    [Test]
    public void DirtyRange_TracksTouchedSlots_AndClears()
    {
        var buf = new InstanceBuffer<Item>();
        buf.Add(new Item(0));   // slot 0
        buf.Add(new Item(1));   // slot 1
        buf.Add(new Item(2));   // slot 2
        Assert.That(buf.HasDirty, Is.True, "adds are dirty");
        Assert.That(buf.DirtyStart, Is.EqualTo(0));
        Assert.That(buf.DirtyCount, Is.EqualTo(3));

        buf.ClearDirty();
        Assert.That(buf.HasDirty, Is.False, "clean after upload");
        Assert.That(buf.DirtyCount, Is.EqualTo(0));

        var h1 = FindHandleOfTag(buf, 1);
        buf.Patch(h1, new Item(11));
        Assert.Multiple(() =>
        {
            Assert.That(buf.HasDirty, Is.True, "patch is dirty");
            Assert.That(buf.DirtyStart, Is.EqualTo(1), "only slot 1 touched");
            Assert.That(buf.DirtyCount, Is.EqualTo(1));
        });
    }

    // Fuzz: random add/remove/patch against a reference map (handle -> value). Every live handle must read back its
    // reference value, and the dense span must be exactly the reference multiset - i.e. the sparse-set never loses,
    // duplicates or mis-maps an instance under swap-remove churn. This is the correctness backbone the retained scene
    // depends on (a lost/mis-mapped instance = a stale or missing pixel on screen).
    [Test]
    public void Fuzz_MatchesReferenceMap()
    {
        var rng = new Random(12345);
        var buf = new InstanceBuffer<Item>(4);
        var reference = new Dictionary<int, Item>();   // live handle -> expected value
        var tag = 0;

        for (var step = 0; step < 20000; step++)
        {
            var op = rng.Next(3);
            if (op == 0 || reference.Count == 0)   // add
            {
                var item = new Item(tag++);
                var h = buf.Add(item);
                Assert.That(reference.ContainsKey(h), Is.False, "a live handle must not be re-handed-out");
                reference[h] = item;
            }
            else if (op == 1)                       // remove a random live handle
            {
                var h = reference.Keys.ElementAt(rng.Next(reference.Count));
                buf.Remove(h);
                reference.Remove(h);
            }
            else                                    // patch a random live handle
            {
                var h = reference.Keys.ElementAt(rng.Next(reference.Count));
                var item = new Item(tag++);
                buf.Patch(h, item);
                reference[h] = item;
            }

            // Invariants (checked every ~200 steps to keep the test fast).
            if (step % 197 != 0) continue;
            Assert.That(buf.Count, Is.EqualTo(reference.Count));
            foreach (var (h, expected) in reference)
            {
                Assert.That(buf.IsAlive(h), Is.True);
                Assert.That(buf.Get(h), Is.EqualTo(expected));
            }
            Assert.That(buf.Span.ToArray(), Is.EquivalentTo(reference.Values));
        }
    }

    private static int FindHandleOfTag(InstanceBuffer<Item> buf, int wantTag)
    {
        // Handles were handed out 0,1,2,... in Add order for a fresh buffer with no removals.
        for (var h = 0; h < 64; h++)
            if (buf.IsAlive(h) && buf.Get(h).Tag == wantTag)
                return h;
        throw new InvalidOperationException($"no live handle with tag {wantTag}");
    }
}
