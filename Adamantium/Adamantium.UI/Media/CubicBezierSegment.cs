namespace Adamantium.UI.Media;

public class CubicBezierSegment : BezierSegmentBase
{
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
        return MathHelper.GetCubicBezier(currentPoint, ControlPoint1, ControlPoint2, Point, SampleRate).ToArray();
    }
}