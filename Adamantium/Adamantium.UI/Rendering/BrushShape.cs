using Adamantium.Mathematics;
using Adamantium.UI.Rendering.Payloads;

namespace Adamantium.UI.Rendering;

/// <summary>Which SHAPE a brush batch paints its fill on. Three batches - gradient, pattern/noise and texture - differ
/// only in where the colour comes from, and each of them draws a rounded rect, an ellipse or a regular polygon. They
/// share ONE distance function in the shader (<c>BrushShapeDistance</c>), so they share this description of the shape
/// too, instead of each inventing its own flag.
/// <para>Nothing grows in the records: the shape rides in the corner-radius slot a non-rect has no use for - the
/// largest radius for a rect, a negative sentinel otherwise - and a polygon's own three numbers (corners, start angle,
/// ring) take the four corner radii it does not have.</para></summary>
internal readonly record struct BrushShape(BrushShapeKind Kind, Vector4F Numbers)
{
    public static readonly BrushShape Rect = new(BrushShapeKind.RoundedRect, Vector4F.Zero);

    public static readonly BrushShape Ellipse = new(BrushShapeKind.Ellipse, Vector4F.Zero);

    public static BrushShape Polygon(RegularPolygonPayload payload, float scale) =>
        new(BrushShapeKind.Polygon, RegularPolygonCollector.ShapeNumbers(payload, scale));

    /// <summary>What goes into the record's corner-radius slot: a rect passes the largest of its own radii, the other
    /// two a sentinel the pixel shader reads as "not a rect".</summary>
    public float RadiusFlag(Vector4F radii) => Kind switch
    {
        BrushShapeKind.Ellipse => -1f,
        BrushShapeKind.Polygon => -2f,
        _ => RectBatchCollector.MaxOf(radii)
    };

    /// <summary>What goes into the record's four corner radii: a polygon's own numbers, a rect's radii, nothing at all
    /// for an ellipse.</summary>
    public Vector4F RadiiFor(Vector4F rectRadii) => Kind switch
    {
        BrushShapeKind.Ellipse => Vector4F.Zero,
        BrushShapeKind.Polygon => Numbers,
        _ => rectRadii
    };
}
