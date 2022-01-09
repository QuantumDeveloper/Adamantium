using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public abstract class BezierCurveBase : Shape
{
    protected BezierCurveBase()
    {
        
    }
    
    protected SplineGeometry SplineGeometry { get; }
    
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
    
    protected double CalculatePointsLength(Vector2[] points)
    {
        double cumulativeLength = 0;
        for (int i = 0; i < points.Length - 1; i++)
        {
            var vector = points[i + 1] - points[i];
            cumulativeLength += vector.Length();
        }

        return cumulativeLength / points.Length;
    }
}

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
        var rate = CalculatePointsLength(new[] { StartPoint, ControlPoint, EndPoint });
        var result = MathHelper.GetQuadraticBezier(StartPoint, ControlPoint, EndPoint, (uint)rate).ToArray();
        SplineGeometry.Points = new PointsCollection(result);
        
        base.OnRender(context);
        
        context.BeginDraw(this);
        context.DrawGeometry(Stroke, SplineGeometry, GetPen());
        context.EndDraw(this);
    }
}

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
        var rate = CalculatePointsLength(new[] { StartPoint, ControlPoint1, ControlPoint2, EndPoint });
        var result = MathHelper.GetCubicBezier(StartPoint, ControlPoint1, ControlPoint2, EndPoint, (uint)rate).ToArray();
        SplineGeometry.Points = new PointsCollection(result);
        
        base.OnRender(context);
        
        context.BeginDraw(this);
        context.DrawGeometry(Stroke, SplineGeometry, GetPen());
        context.EndDraw(this);
    }
}