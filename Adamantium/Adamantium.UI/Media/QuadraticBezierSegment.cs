namespace Adamantium.UI.Media;

public class QuadraticBezierSegment : BezierSegmentBase
{
    public QuadraticBezierSegment()
    {
        
    }

    public QuadraticBezierSegment(Vector2 controlPoint, Vector2 point, bool isStroked)
    {
        ControlPoint = controlPoint;
        Point = point;
        IsStroked = isStroked;
    }
    
    public static readonly AdamantiumProperty ControlPointProperty =
        AdamantiumProperty.Register(nameof(ControlPoint), typeof(Vector2), typeof(QuadraticBezierSegment),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));

    public Vector2 ControlPoint
    {
        get => GetValue<Vector2>(ControlPointProperty);
        set => SetValue(ControlPointProperty, value);
    }
    
    internal override Vector2[] ProcessSegment(Vector2 currentPoint)
    {
        var rate = CalculatePointsLength(new[] { currentPoint, ControlPoint, Point });
        return MathHelper.GetQuadraticBezier(currentPoint, ControlPoint, Point, (uint)rate).ToArray();
    }
}