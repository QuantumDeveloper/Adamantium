using System;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering.Payloads;

public class RectanglePayload(Brush brush, Rect destinationRect, CornerRadius cornerRadius, Pen pen)
    : IEquatable<RectanglePayload>, IRenderCachePolicy
{
    public Brush Brush { get; } = brush;
    public Rect DestinationRect { get; } = destinationRect;
    public CornerRadius CornerRadius { get; } = cornerRadius;
    public Pen Pen { get; } = pen;

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not RectanglePayload payload) return true;
        
        return DestinationRect != payload.DestinationRect || CornerRadius != payload.CornerRadius;
    }

    public bool Equals(RectanglePayload other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(Brush, other.Brush) && DestinationRect.Equals(other.DestinationRect) &&
               CornerRadius.Equals(other.CornerRadius) && Equals(Pen, other.Pen);
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((RectanglePayload)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Brush, DestinationRect, CornerRadius, Pen);
    }
}