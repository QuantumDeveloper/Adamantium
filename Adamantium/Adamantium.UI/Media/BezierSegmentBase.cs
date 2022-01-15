namespace Adamantium.UI.Media;

public abstract class BezierSegmentBase : PathSegment
{
    public static readonly AdamantiumProperty PointProperty =
        AdamantiumProperty.Register(nameof(Point), typeof(Vector2), typeof(BezierSegmentBase),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));

    public Vector2 Point
    {
        get => GetValue<Vector2>(PointProperty);
        set => SetValue(PointProperty, value);
    }
    
    protected double CalculatePointsLength(Vector2[] points)
    {
        double cumulativeLength = 0;
        for (int i = 0; i < points.Length - 1; i++)
        {
            var vector = points[i + 1] - points[i];
            cumulativeLength += vector.Length();
        }

        return cumulativeLength;
    }
}