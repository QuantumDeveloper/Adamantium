using System;

namespace Adamantium.Graphics.Core.Vertices;

/// <summary>
/// Marks a vertex struct whose buffer is bound as PER-INSTANCE data (advanced once per instance) instead of per-vertex.
/// Used for instanced expansion - one buffer element describes a whole primitive (e.g. a sprite/glyph quad) and the
/// corners come from <c>SV_VertexID</c>, so the geometry shader that used to expand points is no longer needed. Flips
/// the binding's <see cref="Adamantium.Vulkan.Core.VertexInputRate"/> to <c>Instance</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class PerInstanceDataAttribute : Attribute
{
}
