using System;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Models;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.UI.Core;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>An immutable snapshot of an arbitrary geometry's tessellated mesh, taken on the record/update thread so the
/// instanced-fill applier bakes/uploads WITHOUT reading the live <see cref="Mesh"/> (which a later re-tessellation
/// overwrites in place). Built-in shape units already snapshot their mesh into the render component's vertices; this is
/// the equivalent freeze for the instanced Path/Polygon path (docs/RENDER_THREAD_PLAN.md). The per-key content
/// fingerprint (<see cref="Key"/>) is computed here, so identical shapes still merge into one instanced draw.</summary>
internal sealed class FrozenMesh
{
    public UIVertex[] Vertices { get; }
    public int[] Indices { get; }
    public PrimitiveType Topology { get; }
    public Rect Bounds { get; }          // the GEOMETRY's local bounds (the gradient shader maps a fragment to a 0..1 uv)
    public GeometryKey Key { get; }
    public bool HasPoints => Vertices.Length > 0;

    private FrozenMesh(UIVertex[] vertices, int[] indices, PrimitiveType topology, Rect bounds, GeometryKey key)
    {
        Vertices = vertices;
        Indices = indices;
        Topology = topology;
        Bounds = bounds;
        Key = key;
    }

    /// <summary>Snapshot a live tessellated mesh (call AFTER ProcessGeometry). Null if it has no drawable vertices.
    /// ToUIVertices() already returns a fresh array (we own it); Indices is the mesh's internal array, so it is copied.</summary>
    public static FrozenMesh From(Mesh mesh, Rect bounds)
    {
        if (mesh is not { HasPoints: true }) return null;
        var vertices = mesh.ToUIVertices();
        if (vertices.Length == 0) return null;
        var indices = mesh.Indices is { Length: > 0 } ix ? (int[])ix.Clone() : [];
        return new FrozenMesh(vertices, indices, mesh.MeshTopology, bounds,
            GeometryKey.ArbitraryMesh(Fingerprint(vertices, indices)));
    }

    // Stable content hash of the LOCAL mesh (vertex bytes + indices): identical tessellations share a key/segment, and ANY
    // difference - including size - yields a different key, so same-topology-different-size shapes never merge.
    private static long Fingerprint(UIVertex[] vertices, int[] indices)
    {
        unchecked
        {
            ulong h = 14695981039346656037UL;   // FNV-1a
            foreach (var b in MemoryMarshal.AsBytes(vertices.AsSpan())) { h ^= b; h *= 1099511628211UL; }
            foreach (var i in indices) { h ^= (uint)i; h *= 1099511628211UL; }
            return (long)h;
        }
    }
}
