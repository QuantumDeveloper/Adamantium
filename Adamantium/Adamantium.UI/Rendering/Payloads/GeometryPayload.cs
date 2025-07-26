using System;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering.Payloads;

public class GeometryPayload(Brush brush, Geometry geometry, Pen pen = null) : IEquatable<GeometryPayload>, IRenderCachePolicy
{
    public Brush Brush { get; } = brush;

    public Geometry Geometry { get; } = geometry;

    public Pen Pen { get; } = pen;

    public bool Equals(GeometryPayload other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(Brush, other.Brush) && Geometry.Equals(other.Geometry) && Equals(Pen, other.Pen);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((GeometryPayload)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Brush, Geometry, Pen);
    }

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not GeometryPayload geometryPayload) return true;
        
        return Geometry != geometryPayload.Geometry;
    }
}