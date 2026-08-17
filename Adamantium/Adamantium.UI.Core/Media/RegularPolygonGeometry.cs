using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.ProceduralGeometry.Shapes;

namespace Adamantium.UI.Core.Media;

/// <summary>A REGULAR POLYGON inscribed in a rect: N corners evenly round it, the first one on the +x axis. Three corners
/// is a triangle, enough of them is a circle - the corner count is the only thing that separates them.
/// <para>This is the tessellated twin of the SDF batch's polygon (BatchEffect.fx, pass Polygon): it draws what the batch
/// declines - a rotated or sheared world - and what needs a mesh rather than a field, like hit-testing a path.</para></summary>
public sealed class RegularPolygonGeometry : Geometry
{
    private Rect bounds;

    public static readonly AdamantiumProperty RectProperty = AdamantiumProperty.Register(nameof(Rect),
        typeof(Rect), typeof(RegularPolygonGeometry), new PropertyMetadata(Rect.Empty, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty CornersProperty = AdamantiumProperty.Register(nameof(Corners),
        typeof(Int32), typeof(RegularPolygonGeometry), new PropertyMetadata(3, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty StartAngleProperty = AdamantiumProperty.Register(nameof(StartAngle),
        typeof(Double), typeof(RegularPolygonGeometry), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure));

    public RegularPolygonGeometry()
    {
        IsClosed = true;
    }

    public RegularPolygonGeometry(Rect rect, int corners, double startAngle = 0) : this()
    {
        Rect = rect;
        Corners = corners;
        StartAngle = startAngle;
        bounds = rect;
    }

    public Rect Rect
    {
        get => GetValue<Rect>(RectProperty);
        set => SetValue(RectProperty, value);
    }

    /// <summary>How many corners. Below three is not a polygon - the tessellator raises it to three, and so does this.</summary>
    public Int32 Corners
    {
        get => GetValue<Int32>(CornersProperty);
        set => SetValue(CornersProperty, value);
    }

    /// <summary>Where corner 0 sits, in degrees from the +x axis. It offsets the PARAMETER, so a turned polygon still
    /// fills the same rect - the batch turns it the same way.</summary>
    public Double StartAngle
    {
        get => GetValue<Double>(StartAngleProperty);
        set => SetValue(StartAngleProperty, value);
    }

    /// <summary>THE statement of what shape a regular polygon is, as geometry. A RING is the outer shape with the inner
    /// one taken out of it - the tessellated twin of the batch subtracting the field's own inward offset. Everything that
    /// needs the polygon as a mesh asks here: the per-unit fallback, the distance field a halo reads, and the brush path
    /// (a gradient, a pattern or a picture is painted on geometry, and the polygon pass paints one colour).</summary>
    public static Geometry Build(Rect rect, int corners, double startAngle = 0, double ringThickness = 0)
    {
        var outer = new RegularPolygonGeometry(rect, corners, startAngle);
        if (ringThickness <= 0) return outer;

        var inner = rect.Deflate(new Thickness(ringThickness));
        if (inner.Width <= 0 || inner.Height <= 0) return outer;   // thicker than the shape: nothing left to hollow

        return new CombinedGeometry
        {
            GeometryCombineMode = GeometryCombineMode.Exclude,
            Geometry1 = outer,
            Geometry2 = new RegularPolygonGeometry(inner, corners, startAngle)
        };
    }

    public override Rect Bounds => bounds;

    public override Geometry Clone() => new RegularPolygonGeometry(Rect, Corners, StartAngle);

    public override void RecalculateBounds()
    {
        bounds = Rect;
        if (Transform != null)
        {
            bounds = bounds.TransformToAABB(Transform.Matrix);
        }
    }

    protected internal override void ProcessGeometryCore(GeometryType geometryType)
    {
        var rect = Rect;
        bounds = rect;

        // Shapes.Polygon takes RADII (it multiplies cos/sin by them), and centres the shape on the origin - so the mesh
        // is moved to the rect the same way the ellipse's is.
        var radii = new Vector2(rect.Width / 2, rect.Height / 2);
        var translation = Matrix4x4.Translation((float)(rect.X + rect.Width / 2), (float)(rect.Y + rect.Height / 2), 0);
        Mesh = Shapes.Polygon.GenerateGeometry(geometryType, radii, Corners, StartAngle, translation);
    }
}
