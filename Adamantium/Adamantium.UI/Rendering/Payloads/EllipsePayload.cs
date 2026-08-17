using System;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering.Payloads;

public class EllipsePayload(
    Brush brush,
    Rect destinationRect,
    double startAngle,
    double sweepAngle,
    EllipseType ellipseType,
    Pen pen)
    : IEquatable<EllipsePayload>, IRenderCachePolicy
{
    // The LIVE brush, read through its immutable snapshot - see RectanglePayload.
    private readonly Brush _brush = brush?.ForRendering();

    public Brush Brush => _brush?.Snapshot;

    // Live brush by REFERENCE ONLY, for the compositor's brush->units index - see RectanglePayload.LiveBrush.
    internal Brush LiveBrush => _brush;

    public Rect DestinationRect { get; } = destinationRect;

    public Double StartAngle { get; } = startAngle;

    public Double SweepAngle { get; } = sweepAngle;

    public EllipseType EllipseType { get; } = ellipseType;

    // A COPY, taken on the record thread - the caller keeps editing its own pen (caps, join, dash array are all
    // reachable) while the applier reads those very fields to build the stroke. Same fix as GeometryPayload.
    public Pen Pen { get; } = pen?.CloneForRendering();

    /// <summary>How thick a RING to leave, in DIPs, measured inward from the outline. 0 = a solid shape.
    /// <para>This makes an annulus - and with a sweep, an annular sector - a SHAPE rather than a thick stroke. A ring gauge
    /// was drawn by stroking a partial ellipse, which put its thickness in the PEN: the pen was then spent, so the ring
    /// could not carry an outline of its own, and the thickness could not be told apart from a border. As geometry it is
    /// just the ellipse's field minus its inward offset - the same intersection a sector is - and the pen is free again.</para></summary>
    public Double RingThickness { get; init; }

    public bool HasRing => RingThickness > 0;

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not EllipsePayload payload) return true;

        return StartAngle != payload.StartAngle ||
               SweepAngle != payload.SweepAngle ||
               EllipseType != payload.EllipseType ||
               DestinationRect != payload.DestinationRect ||
               RingThickness != payload.RingThickness;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(StartAngle, SweepAngle, EllipseType, DestinationRect, Pen, RingThickness);
    }

    public bool Equals(EllipsePayload other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(DestinationRect, other.DestinationRect) && Equals(Brush, other.Brush) &&
               StartAngle.Equals(other.StartAngle) && SweepAngle.Equals(other.SweepAngle) &&
               EllipseType == other.EllipseType && Equals(Pen, other.Pen) &&
               RingThickness.Equals(other.RingThickness);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((EllipsePayload)obj);
    }
}