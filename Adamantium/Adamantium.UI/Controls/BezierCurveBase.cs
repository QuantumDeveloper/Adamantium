using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public abstract class BezierCurveBase : Shape
{
    protected BezierCurveBase()
    {
        
    }
    
    protected StreamGeometry StreamGeometry { get; }
    
    public static readonly AdamantiumProperty StartPointProperty =
        AdamantiumProperty.Register(nameof(StartPoint), typeof(Vector2), typeof(BezierCurveBase),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));
    
    public static readonly AdamantiumProperty EndPointProperty =
        AdamantiumProperty.Register(nameof(EndPoint), typeof(Vector2), typeof(BezierCurveBase),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));
    
    public Vector2 StartPoint
    {
        get => GetValue<Vector2>(StartPointProperty); 
        set => SetValue(StartPointProperty, value);
    }
    
    public Vector2 EndPoint
    {
        get => GetValue<Vector2>(EndPointProperty); 
        set => SetValue(EndPointProperty, value);
    }
    
    protected double CalculatePointsLength(Vector2[] points)
    {
        double cumulativeLength = 0;
        for (int i = 0; i < points.Length - 1; i++)
        {
            var vector = points[i + 1] - points[i];
            cumulativeLength += vector.Length();
        }

        return cumulativeLength / points.Length;
    }
}