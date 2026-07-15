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

    public Pen Pen { get; } = pen;

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not EllipsePayload payload) return true;

        return StartAngle != payload.StartAngle || 
               SweepAngle != payload.SweepAngle ||
               EllipseType != payload.EllipseType || 
               DestinationRect != payload.DestinationRect;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(StartAngle, SweepAngle, EllipseType, DestinationRect, Pen);
    }

    public bool Equals(EllipsePayload other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(DestinationRect, other.DestinationRect) && Equals(Brush, other.Brush) &&
               StartAngle.Equals(other.StartAngle) && SweepAngle.Equals(other.SweepAngle) &&
               EllipseType == other.EllipseType && Equals(Pen, other.Pen);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((EllipsePayload)obj);
    }
}