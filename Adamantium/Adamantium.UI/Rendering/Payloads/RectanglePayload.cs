using System;
using Adamantium.ProceduralGeometry;
using Adamantium.Mathematics;
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

    // The FRAME: a border of its own thickness on each side, drawn INSIDE the rect. Not a pen - a pen is one width
    // centred on a contour, and four different widths are not a contour offset at all. Fill and frame ride in the SAME
    // instance on purpose: drawn as two shapes they share an outline, and both anti-alias it, which composites to a
    // dark hairline all the way round (what the old CombinedGeometry ring did).
    private readonly Brush _borderBrush;

    public Brush BorderBrush => _borderBrush?.Snapshot;

    internal Brush LiveBorderBrush => _borderBrush;

    public Thickness BorderThickness { get; }

    public bool HasFrame => BorderThickness.Left > 0 || BorderThickness.Top > 0
                         || BorderThickness.Right > 0 || BorderThickness.Bottom > 0;

    /// <summary>What the border leaves for the fill. The inner box is NOT concentric - insetting the sides by different
    /// amounts moves the centre by half their difference - which is why it is stated once here.</summary>
    public Rect FrameInnerRect => DestinationRect.Deflate(BorderThickness);

    /// <summary>The inner outline's corners: each shrinks by the THICKER of the two sides meeting at it. A scalar corner
    /// cannot stay parallel to the outer one under unequal sides, and taking the thicker of the pair keeps the inner arc
    /// from bulging out past the border on the heavier side.
    /// <para>Must match CompositeFillBorder in BatchEffect.fx - the batch and the tessellated fallback have to cut the
    /// same ring, or a rect that leaves the batch (a rotated world) would visibly change shape.</para></summary>
    public CornerRadius FrameInnerCorners
    {
        get
        {
            var t = BorderThickness;
            var c = CornerRadius;
            return new CornerRadius(
                Math.Max(0.0, c.TopLeft - Math.Max(t.Left, t.Top)),
                Math.Max(0.0, c.TopRight - Math.Max(t.Top, t.Right)),
                Math.Max(0.0, c.BottomRight - Math.Max(t.Right, t.Bottom)),
                Math.Max(0.0, c.BottomLeft - Math.Max(t.Bottom, t.Left)));
        }
    }

    public RectanglePayload(Brush brush, Rect destinationRect, CornerRadius cornerRadius, Brush borderBrush, Thickness borderThickness)
        : this(brush, destinationRect, cornerRadius, null)
    {
        _borderBrush = borderBrush?.ForRendering();
        BorderThickness = borderThickness;
    }

    public bool RequiresBufferRebuild(IRenderCachePolicy newState)
    {
        if (newState is not RectanglePayload payload) return true;

        return DestinationRect != payload.DestinationRect || CornerRadius != payload.CornerRadius
            || !BorderThickness.Equals(payload.BorderThickness);
    }

    public bool Equals(RectanglePayload other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(Brush, other.Brush) && DestinationRect.Equals(other.DestinationRect) &&
               CornerRadius.Equals(other.CornerRadius) && Equals(Pen, other.Pen) && AntiAlias == other.AntiAlias &&
               Equals(BorderBrush, other.BorderBrush) && BorderThickness.Equals(other.BorderThickness);
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
        return HashCode.Combine(Brush, DestinationRect, CornerRadius, Pen, AntiAlias, BorderBrush, BorderThickness);
    }
}