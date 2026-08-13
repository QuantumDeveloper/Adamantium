using System;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering.Payloads;

public class GeometryPayload(Brush brush, Geometry geometry, Pen pen = null, Matrix4x4F? localTransform = null) : IEquatable<GeometryPayload>, IRenderCachePolicy
{
    /// <summary>Where this draw places the geometry, ON TOP of the element's own world transform - how a
    /// <see cref="Adamantium.UI.Core.Media.Drawings.Drawing"/> puts many shapes at their own positions and scales while
    /// they all belong to one element. Identity for every ordinary draw. It rides the INSTANCE, never the mesh, so a
    /// shape drawn at five sizes is still one mesh and five instances.</summary>
    public Matrix4x4F LocalTransform { get; } = localTransform ?? Matrix4x4F.Identity;

    // The LIVE brush, read through its immutable snapshot - see RectanglePayload.
    private readonly Brush _brush = brush?.ForRendering();

    public Brush Brush => _brush?.Snapshot;

    /// <summary>The LIVE brush, by reference. What draws per-unit holds THIS and dereferences its snapshot per draw:
    /// holding the snapshot object instead freezes the fill at record time, so dragging a brush's own parameters
    /// changed nothing on screen until something else forced a re-record.</summary>
    internal Brush LiveBrush => _brush;

    public Geometry Geometry { get; } = Tessellate(geometry);

    /// <summary>What the geometry held WHEN THIS PAYLOAD WAS RECORDED. The instance is not enough: a Polygon reopens its
    /// one StreamGeometry with new points, so the same object describes a different shape from one record to the next.</summary>
    public int GeometryVersion { get; } = geometry?.Version ?? 0;

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
        return Equals(Brush, other.Brush) && Geometry.Equals(other.Geometry)
               && GeometryVersion == other.GeometryVersion && Equals(Pen, other.Pen)
               && LocalTransform == other.LocalTransform;
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
        return HashCode.Combine(Brush, Geometry, GeometryVersion, Pen, LocalTransform);
    }

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not GeometryPayload geometryPayload) return true;

        // NOT the instance alone: a Polygon reuses ONE StreamGeometry and reopens it with new points, so the reference
        // is identical while the shape is not. That read as "nothing to rebuild" and the GPU kept the mesh tessellated
        // from the first record - the figure stopped resizing and only its slot moved.
        return Geometry != geometryPayload.Geometry || GeometryVersion != geometryPayload.GeometryVersion;
    }
}