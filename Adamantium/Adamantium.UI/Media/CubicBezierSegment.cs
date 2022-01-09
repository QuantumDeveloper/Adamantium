namespace Adamantium.UI.Media;

public class CubicBezierSegment : BezierSegmentBase
{
    public CubicBezierSegment()
    {
        
    }

    public CubicBezierSegment(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 point, bool isStroked)
    {
        ControlPoint1 = controlPoint1;
        ControlPoint2 = controlPoint2;
        Point = point;
        IsStroked = isStroked;
    }
    
    public static readonly AdamantiumProperty ControlPoint1Property =
        AdamantiumProperty.Register(nameof(ControlPoint1), typeof(Vector2), typeof(CubicBezierSegment),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));
    
    public static readonly AdamantiumProperty ControlPoint2Property =
        AdamantiumProperty.Register(nameof(ControlPoint2), typeof(Vector2), typeof(CubicBezierSegment),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));

    public Vector2 ControlPoint1
    {
        get => GetValue<Vector2>(ControlPoint1Property); 
        set => SetValue(ControlPoint1Property, value);
    }
    
    public Vector2 ControlPoint2
    {
        get => GetValue<Vector2>(ControlPoint2Property); 
        set => SetValue(ControlPoint2Property, value);
    }
    
    internal override Vector2[] ProcessSegment(Vector2 currentPoint)
    {
        var rate = CalculatePointsLength(new[] { currentPoint, ControlPoint1, ControlPoint2, Point });
        return MathHelper.GetCubicBezier(currentPoint, ControlPoint1, ControlPoint2, Point, (uint)rate).ToArray();
    }
}