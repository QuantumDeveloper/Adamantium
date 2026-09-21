using System;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering.Payloads;

/// <summary>A REGULAR POLYGON inscribed in <see cref="DestinationRect"/>: the shape whose only distinguishing number is
/// how many corners it has - 3 a triangle, 6 a hexagon, enough of them a circle.
/// <para>Its own payload rather than a corner count bolted onto the ellipse: they are two shapes, and one of them would
/// have ended up carrying fields (an angular cut, a chord) that mean nothing to it.</para></summary>
public class RegularPolygonPayload(Brush brush, Rect destinationRect, int corners, Pen pen)
    : IEquatable<RegularPolygonPayload>, IRenderCachePolicy
{
    /// <summary>Fewer than three corners is not a polygon; the shape clamps rather than refuses, the way the tessellator
    /// does (Shapes.Polygon raises anything below three to three).</summary>
    public const int MinCorners = 3;

    // The LIVE brush, read through its immutable snapshot - see RectanglePayload.
    private readonly Brush _brush = brush?.ForRendering();

    public Brush Brush => _brush?.Snapshot;

    internal Brush LiveBrush => _brush;

    public Rect DestinationRect { get; } = destinationRect;

    public int Corners { get; } = Math.Max(MinCorners, corners);

    // A COPY, taken on the record thread - see RectanglePayload.Pen.
    public Pen Pen { get; } = pen?.CloneForRendering();

    /// <summary>Leave a RING this thick (DIPs, inward from the outline) instead of a solid shape - a hollow triangle is a
    /// chevron, and it costs the same one instance. Geometry, not a stroke: the pen stays free.</summary>
    public Double RingThickness { get; init; }

    public bool HasRing => RingThickness > 0;

    /// <summary>Where corner 0 sits, in DEGREES from the +x axis, positive the same way round as the ellipse's start
    /// angle. Without it a triangle can only ever point right - this is what stands one on a corner or flat on a side.</summary>
    public Double StartAngle { get; init; }

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not RegularPolygonPayload payload) return true;

        return DestinationRect != payload.DestinationRect || Corners != payload.Corners ||
               RingThickness != payload.RingThickness || StartAngle != payload.StartAngle;
    }

    public override int GetHashCode() => HashCode.Combine(DestinationRect, Corners, Pen, RingThickness, StartAngle);

    public bool Equals(RegularPolygonPayload other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(DestinationRect, other.DestinationRect) && Equals(Brush, other.Brush) &&
               Corners == other.Corners && Equals(Pen, other.Pen) && RingThickness.Equals(other.RingThickness) &&
               StartAngle.Equals(other.StartAngle);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((RegularPolygonPayload)obj);
    }
}
