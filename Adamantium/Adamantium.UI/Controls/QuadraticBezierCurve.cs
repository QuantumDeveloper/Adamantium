using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public class QuadraticBezierCurve : BezierCurveBase
{
    public QuadraticBezierCurve()
    {
        
    }
    
    public static readonly AdamantiumProperty ControlPointProperty =
        AdamantiumProperty.Register(nameof(ControlPoint), typeof(Vector2), typeof(QuadraticBezierCurve),
            new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsMeasure));
    
    public Vector2 ControlPoint
    {
        get => GetValue<Vector2>(ControlPointProperty); 
        set => SetValue(ControlPointProperty, value);
    }
    
    protected override void OnRender(DrawingContext context)
    {
        var streamContext = StreamGeometry.Open();
        streamContext.BeginFigure(StartPoint, true, true).QuadraticBezierTo(ControlPoint, EndPoint, true);
        
        context.DrawGeometry(Stroke, StreamGeometry, GetPen());
    }
}