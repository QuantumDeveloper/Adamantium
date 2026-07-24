using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

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
        // Dense uniform-t base, then EVEN arc-length resample (~3px) - smooth stroke, no sub-pixel bunching.
        var fine = MathHelper.GetQuadraticBezier(currentPoint, ControlPoint, Point, 256);
        var count = System.Math.Clamp((int)(MathHelper.PolylineLength(fine) / 3.0), 8, 256);
        return MathHelper.ResampleByArcLength(fine, count);
    }
}