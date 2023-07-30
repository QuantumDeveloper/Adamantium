using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public class CubicBezierCurve : BezierCurveBase
{
    public CubicBezierCurve()
    {
        
    }
    
    public static readonly AdamantiumProperty ControlPoint1Property =
        AdamantiumProperty.Register(nameof(ControlPoint1), typeof(Vector2), typeof(CubicBezierCurve),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));
    
    public static readonly AdamantiumProperty ControlPoint2Property =
        AdamantiumProperty.Register(nameof(ControlPoint2), typeof(Vector2), typeof(CubicBezierCurve),
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

    protected override void OnRender(DrawingContext context)
    {
        var streamContext = StreamGeometry.Open();
        streamContext.BeginFigure(StartPoint, true, true).CubicBezierTo(ControlPoint1, ControlPoint2, EndPoint, true);
        
        context.DrawGeometry(Stroke, StreamGeometry, GetPen());
    }
}