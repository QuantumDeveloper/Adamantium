using System;
using System.Collections.Generic;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// The retained geometry-instancing scene (docs/RENDER_CACHE_REDESIGN.md §4d level-1 / §4h). Geometry is interned by
/// <see cref="GeometryKey"/>: all elements sharing a key share ONE local mesh and their per-element data
/// (<see cref="GeometryInstance"/>: world transform + colour) lives packed in one <see cref="InstanceBuffer{T}"/>, so N
/// identical shapes render as ONE instanced draw. The scene is RETAINED - a move/colour change is an O(1)
/// <see cref="Patch"/> of one slot, not a rebuild - which is the whole point: dragging a 600-tile grid patches 600
/// transforms instead of re-tessellating and re-recording the scene every frame.
/// </summary>
/// <remarks>
/// Owns no GPU resources itself: a <see cref="Segment.Mesh"/> is the shared LOCAL mesh (the GPU layer builds/binds its
/// vtx/idx from it) and the instance data is CPU-side until the draw uploads the dirty range. Single-threaded (the
/// render thread). Elements are addressed by their stable <c>RenderId</c>; a shape change (different key) moves the
/// element between segments transparently.
/// </remarks>
public sealed class GeometryInstanceRegistry
{
    public sealed class Segment
    {
        public GeometryKey Key { get; }

        /// <summary>The shared LOCAL mesh (element-space vertices) every instance in this segment draws. Set once when
        /// the segment is first created; the GPU layer builds its vertex/index buffers from it lazily.</summary>
        public object Mesh { get; internal set; }

        public InstanceBuffer<GeometryInstance> Instances { get; } = new();

        internal Segment(GeometryKey key, object mesh)
        {
            Key = key;
            Mesh = mesh;
        }
    }

    private readonly Dictionary<GeometryKey, Segment> _segments = new();
    private readonly Dictionary<Guid, (GeometryKey key, int handle)> _byElement = new();

    /// <summary>Distinct geometry keys currently in the scene (= number of instanced draws for these, before z/clip
    /// segmentation).</summary>
    public int SegmentCount => _segments.Count;

    /// <summary>All live instances across every segment.</summary>
    public int InstanceCount
    {
        get
        {
            var n = 0;
            foreach (var s in _segments.Values) n += s.Instances.Count;
            return n;
        }
    }

    /// <summary>
    /// Registers or refreshes the element's instance. If the element is new, it is added to the segment for
    /// <paramref name="key"/> (creating it, with <paramref name="mesh"/> as its shared local mesh, if first seen). If the
    /// element already exists with the SAME key, its instance is patched in place (shape unchanged - the common resize/
    /// move/recolour case). If the key CHANGED (its shape changed), the element is moved to the new segment.
    /// </summary>
    public void Set(Guid renderId, GeometryKey key, object mesh, in GeometryInstance instance)
    {
        if (_byElement.TryGetValue(renderId, out var cur))
        {
            if (cur.key.Equals(key))
            {
                _segments[cur.key].Instances.Patch(cur.handle, instance);
                return;
            }
            RemoveFrom(cur);   // shape changed -> leave the old segment, fall through to join the new one
        }

        var segment = GetOrCreateSegment(key, mesh);
        var handle = segment.Instances.Add(instance);
        _byElement[renderId] = (key, handle);
    }

    /// <summary>Transform/colour-only patch (the element's shape - its key - is unchanged). O(1). Returns false if the
    /// element was never <see cref="Set"/>.</summary>
    public bool Patch(Guid renderId, in GeometryInstance instance)
    {
        if (!_byElement.TryGetValue(renderId, out var cur)) return false;
        _segments[cur.key].Instances.Patch(cur.handle, instance);
        return true;
    }

    /// <summary>Removes the element from the scene (it left the visual tree). O(1) swap-remove; an emptied segment is
    /// kept so its mesh is reused when the shape reappears (recycling) - see <see cref="TrimEmptySegments"/>.</summary>
    public void Remove(Guid renderId)
    {
        if (_byElement.Remove(renderId, out var cur)) RemoveFrom(cur);
    }

    public bool Contains(Guid renderId) => _byElement.ContainsKey(renderId);

    /// <summary>The segments to draw. Each is one instanced draw of <see cref="Segment.Instances"/> using
    /// <see cref="Segment.Mesh"/> (z-order / clip segmentation layers on top - a later phase).</summary>
    public IEnumerable<Segment> Segments => _segments.Values;

    /// <summary>Drops segments that currently hold no instances (frees their shared mesh). Optional maintenance for a
    /// scene with an unbounded churn of unique shapes; a recycling list keeps its handful of shapes, so this is a no-op
    /// there. Returns the dropped meshes so the caller can release their GPU buffers.</summary>
    public List<object> TrimEmptySegments()
    {
        List<object> dropped = null;
        List<GeometryKey> keys = null;
        foreach (var pair in _segments)
        {
            if (pair.Value.Instances.Count != 0) continue;
            (keys ??= new List<GeometryKey>()).Add(pair.Key);
            (dropped ??= new List<object>()).Add(pair.Value.Mesh);
        }
        if (keys != null)
            foreach (var k in keys)
                _segments.Remove(k);
        return dropped ?? new List<object>();
    }

    private Segment GetOrCreateSegment(GeometryKey key, object mesh)
    {
        if (_segments.TryGetValue(key, out var segment))
        {
            // Segment already interned (another element of the same shape, or an emptied one being reused). Keep the
            // existing shared mesh; refresh it only if it had been released (mesh == null).
            segment.Mesh ??= mesh;
            return segment;
        }
        segment = new Segment(key, mesh);
        _segments[key] = segment;
        return segment;
    }

    private void RemoveFrom((GeometryKey key, int handle) cur)
    {
        _segments[cur.key].Instances.Remove(cur.handle);
    }
}
