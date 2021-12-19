namespace Adamantium.UI.Media;

public class QuadraticBezierSegment : BezierSegmentBase
{
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
        return MathHelper.GetQuadraticBezier(currentPoint, ControlPoint, Point, 20).ToArray();
    }
}