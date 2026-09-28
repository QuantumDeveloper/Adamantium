using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Models;

namespace Adamantium.Engine.Rendering;

// Render-layer store of GPU geometry, keyed by (mesh instance, vertex format). Identical meshes share one buffer set -
// the foundation the batching/instancing/BDA work (F.5/2) builds on. Owned + disposed by the rendering processor;
// ECS components no longer own GPU buffers.
public sealed class MeshGeometryCache : DisposableObject
{
    private readonly IGraphicsDevice _device;
    private readonly Dictionary<GeometryKey, RenderGeometry> _geometries = [];

    public MeshGeometryCache(IGraphicsDevice device)
    {
        _device = device;
    }

    public RenderGeometry GetOrCreate(Mesh mesh, Type vertexType)
    {
        var key = new GeometryKey(mesh, vertexType);
        if (!_geometries.TryGetValue(key, out var geometry))
        {
            geometry = ToDispose(new RenderGeometry(mesh, vertexType));
            _geometries[key] = geometry;
        }

        geometry.EnsureUpToDate(_device);
        return geometry;
    }

    // Reference identity on the mesh (a replaced Mesh instance is a fresh entry) plus the vertex format, so a mesh used
    // both skinned and static keeps separate buffers.
    private readonly struct GeometryKey : IEquatable<GeometryKey>
    {
        private readonly Mesh _mesh;
        private readonly Type _vertexType;

        public GeometryKey(Mesh mesh, Type vertexType)
        {
            _mesh = mesh;
            _vertexType = vertexType;
        }

        public bool Equals(GeometryKey other) => ReferenceEquals(_mesh, other._mesh) && _vertexType == other._vertexType;
        public override bool Equals(object obj) => obj is GeometryKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_mesh), _vertexType);
    }
}
