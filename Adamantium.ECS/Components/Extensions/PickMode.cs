using System;

namespace Adamantium.ECS.Components.Extensions;

/// <summary>
/// What a pick tests. An entity is tested every requested way that applies to it; the nearest hit along the ray wins.
/// </summary>
[Flags]
public enum PickMode
{
    None = 0,

    /// <summary>Every collider, merged bounds included: a model is hit as a whole.</summary>
    Colliders = 1,

    /// <summary>Colliders of entities that have a mesh: a model is hit by its part.</summary>
    MeshColliders = 2,

    /// <summary>The exact triangles of triangle meshes: what is drawn under the pointer.</summary>
    Triangles = 4,

    /// <summary>The exact segments of line meshes, within an aperture in pixels.</summary>
    Lines = 8
}
