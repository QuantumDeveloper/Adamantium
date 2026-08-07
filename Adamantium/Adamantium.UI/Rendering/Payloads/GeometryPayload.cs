using System;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering.Payloads;

public class GeometryPayload(Brush brush, Geometry geometry, Pen pen = null) : IEquatable<GeometryPayload>, IRenderCachePolicy
{
    // The LIVE brush, read through its immutable snapshot - see RectanglePayload.
    private readonly Brush _brush = brush?.ForRendering();

    public Brush Brush => _brush?.Snapshot;

    public Geometry Geometry { get; } = Tessellate(geometry);

    /// <summary>Tessellate HERE, where the payload is built - inside component.Render, on the RECORD thread. It used to
    /// happen in the render unit instead, which the applier constructs: that put an IN-PLACE rebuild of the live
    /// <see cref="Geometry.Mesh"/> (and of the unsynchronised <c>IsProcessed</c> flag guarding it) on the render thread,
    /// while the property system kept invalidating the same flag from the loop thread. Measured: 1728 tessellations on
    /// the render thread against 315 invalidations from the loop thread in one run. The render thread must only READ.</summary>
    private static Geometry Tessellate(Geometry g)
    {
        g?.ProcessGeometry(GeometryType.Both);
        return g;
    }

    // A COPY, taken here on the record thread. The pen the caller passed stays editable from the loop thread (its caps,
    // join and dash array are all reachable), and the applier reads exactly those fields when it builds the stroke
    // contours - so holding the caller's instance put a live, mutable object on the far side of the seam. The brush
    // INSIDE the pen stays live on purpose: it is read through its own immutable snapshot, so an animated stroke brush
    // keeps animating.
    public Pen Pen { get; } = pen?.CloneForRendering();

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