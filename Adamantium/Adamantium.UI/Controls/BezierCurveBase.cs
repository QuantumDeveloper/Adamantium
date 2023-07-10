using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public abstract class BezierCurveBase : Shape
{
    protected BezierCurveBase()
    {
        StreamGeometry = new StreamGeometry();
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
}