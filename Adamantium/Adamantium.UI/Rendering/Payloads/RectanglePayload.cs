using System;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering.Payloads;

public class RectanglePayload(Brush brush, Rect destinationRect, CornerRadius cornerRadius, Pen pen)
    : IEquatable<RectanglePayload>, IRenderCachePolicy
{
    // The LIVE brush, dereferenced to its immutable snapshot on every read (see Brush.Snapshot). Holding the snapshot
    // itself would pin the appearance the brush had at RECORD time, so an animated brush - which is repainted by re-baking
    // this very payload, not by re-recording the element - would never change on screen.
    private readonly Brush _brush = brush?.ForRendering();

    public Brush Brush => _brush?.Snapshot;

    // The LIVE brush by REFERENCE ONLY - never read its mutable values through this. The render cache indexes units by it so
    // the compositor can find the slots a shared animating brush paints, and reference identity is thread-safe.
    internal Brush LiveBrush => _brush;
    public Rect DestinationRect { get; } = destinationRect;
    public CornerRadius CornerRadius { get; } = cornerRadius;
    // A COPY, taken on the record thread - the caller keeps editing its own pen (caps, join, dash array are all
    // reachable) while the applier reads those very fields to build the stroke. Same fix as GeometryPayload.
    public Pen Pen { get; } = pen?.CloneForRendering();

    /// <summary>False = draw the edges hard. Taken from the drawing component's UseAnalyticAA at RECORD time, because
    /// that is where the component is known; the batch carries it to the shader.</summary>
    public bool AntiAlias { get; init; } = true;

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
               CornerRadius.Equals(other.CornerRadius) && Equals(Pen, other.Pen) && AntiAlias == other.AntiAlias;
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
        return HashCode.Combine(Brush, DestinationRect, CornerRadius, Pen, AntiAlias);
    }
}