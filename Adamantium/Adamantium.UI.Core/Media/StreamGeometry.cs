using Adamantium.Mathematics;
using Adamantium.Mathematics.Triangulation;
using Adamantium.ProceduralGeometry;

namespace Adamantium.UI.Core.Media;

public sealed class StreamGeometry : Geometry
{
    private Rect bounds;
    private StreamGeometryContext context;

    public StreamGeometry()
    {
        context = new StreamGeometryContext();
    }

    public override Rect Bounds => bounds;

    public StreamGeometryContext Open()
    {
        context = new StreamGeometryContext();
        // Reopening REPLACES the figures, so the tessellated mesh no longer describes this geometry. Without saying so,
        // ProcessGeometry sees IsProcessed and keeps the mesh built from the FIRST content for good - and a Polygon
        // (which reopens its one StreamGeometry on every render) would draw its original outline forever while layout
        // moved and resized the slot under it.
        InvalidateGeometry();
        return context;
    }

    public static readonly AdamantiumProperty FillRuleProperty = AdamantiumProperty.Register(nameof(FillRule),
       typeof(FillRule), typeof(StreamGeometry),
       new PropertyMetadata(FillRule.EvenOdd, PropertyMetadataOptions.AffectsRender));

    public FillRule FillRule
    {
        get => GetValue<FillRule>(FillRuleProperty);
        set => SetValue(FillRuleProperty, value);
    }

    public override Geometry Clone()
    {
        throw new NotImplementedException();
    }

    public override void RecalculateBounds()
    {
        context.ProcessFigures();
        var contours = context.GetContours();
        var points = new List<Vector2>();
        foreach (var contour in contours)
        {
            points.AddRange(contour.Points);
        }
        bounds = Rect.FromPoints(points);
    }

    protected internal override void ProcessGeometryCore(GeometryType geometryType)
    {
        context.ProcessFigures();
        var contours = context.GetContours();
        Mesh.Clear();

        if (geometryType == GeometryType.Outlined)
        {
            Mesh.AddContours(contours);
            return;
        }

        var polygon = new Polygon();
        polygon.FillRule = FillRule;
        polygon.AddContours(contours);

        var points = polygon.FillIndirect();
        Mesh.AddContours(polygon.ProcessedContours);

        Mesh.SetPoints(points);
    }
}