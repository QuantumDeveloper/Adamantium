using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public class BezierLine : BezierCurveBase
{
    public BezierLine()
    {
    }
    
    public static readonly AdamantiumProperty BezierTypeProperty = AdamantiumProperty.Register(nameof(BezierType),
        typeof(BezierLine), typeof(Shape),
        new PropertyMetadata(BezierType.Quadratic,
            PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsRender));
        
    public static readonly AdamantiumProperty ControlPoint1Property = AdamantiumProperty.Register(nameof(ControlPoint1),
        typeof(BezierLine), typeof(Vector2),
        new PropertyMetadata(default(Vector2), PropertyMetadataOptions.AffectsRender));
        
    public static readonly AdamantiumProperty ControlPoint2Property = AdamantiumProperty.Register(nameof(ControlPoint2),
        typeof(BezierLine), typeof(Vector2),
        new PropertyMetadata(default(Vector2), PropertyMetadataOptions.AffectsRender));

    public BezierType BezierType
    {
        get => GetValue<BezierType>(BezierTypeProperty);
        set => SetValue(BezierTypeProperty, value);
    }
        
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
        streamContext.BeginFigure(StartPoint, true, false).QuadraticBezierTo(ControlPoint1, EndPoint);
        context.DrawGeometry(Fill, StreamGeometry, GetPen());
    }
}