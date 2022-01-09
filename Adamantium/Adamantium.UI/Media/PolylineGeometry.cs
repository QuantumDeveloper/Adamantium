using System.Collections.Generic;

namespace Adamantium.UI.Media;

public class PolylineGeometry : Geometry
{
    private Rect bounds;
    
    public PolylineGeometry()
    {
        
    }
    
    public PolylineGeometry(IEnumerable<Vector2> points)
    {
        Points = new PointsCollection(points);
        ProcessGeometry();
    }

    public static readonly AdamantiumProperty PointsProperty = AdamantiumProperty.Register(nameof(Points),
        typeof(PointsCollection), typeof(PolylineGeometry),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));
    
    public PointsCollection Points
    {
        get => GetValue<PointsCollection>(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public override Rect Bounds => bounds;

    public override Geometry Clone()
    {
        throw new System.NotImplementedException();
    }

    public override void RecalculateBounds()
    {
        bounds = Rect.FromPoints(Points);
    }

    protected internal override void ProcessGeometryCore()
    {
        OutlineMesh.SetPoints(Points);
    }
}