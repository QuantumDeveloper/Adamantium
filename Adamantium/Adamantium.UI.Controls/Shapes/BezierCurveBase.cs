using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Shapes;

public abstract class BezierCurveBase : CurveBase
{
    protected BezierCurveBase()
    {
    }

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