using System;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Effects.Generated;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Regular-polygon SDF batch: N same-clip triangles/pentagons/hexagons in ONE instanced draw, each a per-instance
// PolygonItem the pixel shader reconstructs from its distance field (pass Polygon). Sibling of the rect and ellipse
// collectors, and deliberately its own: a polygon and an ellipse share a shape of record, not a shape.
internal sealed class RegularPolygonCollector : ShapeSdfCollector<PolygonItem>
{
    // A/B / safety-valve toggle: off routes every polygon back to its per-unit tessellated draw.
    public static bool Enabled = true;

    public RegularPolygonCollector() : base(512) { }

    protected override IEffectPass DrawPass => Effect.BatchPolygonPass;

    /// <summary>THE one statement of what this batch draws - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatch(RegularPolygonPayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not (null or SolidColorBrush)) return false;   // gradient/image fill -> per-unit
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        // A DASHED or TRIMMED pen needs an arc length along the contour, and this shape has none baked - the mask the
        // rect and ellipse use is theirs, not a polygon's. Refused rather than drawn with the wrong pattern.
        if (NeedsArcLength(p.Pen)) return false;

        var hasFill = p.Brush is SolidColorBrush { Color.A: > 0 };
        var hasStroke = p.Pen is { Brush: SolidColorBrush { Color.A: > 0 } };
        return hasFill || hasStroke;
    }

    public bool CanBatch(RegularPolygonPayload p) => WantsBatch(p);

    /// <summary>A DASHED or TRIMMED pen needs an arc length along the contour, and this shape bakes none. Asked by the
    /// sibling brush batches too, so all four refuse the same pen.</summary>
    internal static bool NeedsArcLength(Pen pen) =>
        pen is { } p && (p.DashStrokeArray is { Count: > 0 } || p.TrimStart > 0.0 || p.TrimEnd < 1.0);

    /// <summary>The three numbers that describe a polygon to a shader, in the slot a shape without corner radii leaves
    /// free: corners, start angle in RADIANS, ring thickness in device px. One statement, read by this batch and by the
    /// gradient/pattern/texture siblings that paint the same shape with another source of colour.</summary>
    internal static Vector4F ShapeNumbers(RegularPolygonPayload p, float scale) =>
        new(p.Corners, (float)MathHelper.DegreesToRadians(p.StartAngle), (float)(p.RingThickness * scale), 0);

    public bool TryAdd(RegularPolygonPayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0,
        int clipSlot = -1, int fadeSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!BakeItem(p, world, opacity, transformSlot, out var item)) return false;   // rotation/shear -> per-unit
        item.Clip = new Vector4F(clipSlot, fadeSlot, 0, 0);
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    /// <summary>Bake one polygon into an instance record WITHOUT appending it - shared by TryAdd and the paint fast-path,
    /// exactly as the rect and ellipse batches do. False = not bakeable this way (a rotated or sheared world).</summary>
    public static bool BakeItem(RegularPolygonPayload p, Matrix4x4F world, double opacity, int transformSlot, out PolygonItem item)
    {
        item = default;
        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;   // rotation/shear -> per-unit

        var color = Vector4F.Zero;
        if (p.Brush is SolidColorBrush solid)
        {
            color = solid.Color.ToVector4();
            color.W *= (float)(opacity * solid.Opacity);
        }

        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        var r = p.DestinationRect;
        RectBatchCollector.BakeStroke(p.Pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1, out var dash);
        item = new PolygonItem
        {
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            Params = new Vector4F(transformSlot, p.Corners, (float)(p.RingThickness * sx), (float)MathHelper.DegreesToRadians(p.StartAngle)),
            Color = color,
            StrokeColor = strokeColor,
            Stroke0 = stroke0,
            Stroke1 = stroke1,
            Dash = dash,
            // -1, never 0, for BOTH slots (.x clip, .y opacity): zero is a valid slot belonging to somebody else, so a
            // record that forgot to stamp its own would be cut by a stranger's shape or faded by a stranger's alpha.
            // The stampers are TryAdd, TryStage and the patch.
            Clip = new Vector4F(-1, -1, 0, 0)
        };
        return true;
    }
}
