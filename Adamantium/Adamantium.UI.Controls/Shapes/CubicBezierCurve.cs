using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Shapes;

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

    protected override void OnRender(IDrawingContext context)
    {
        // Samples fully controls curvature: count 2 -> a straight chord (ResampleByArcLength returns the two endpoints);
        // more points trace the curve. FRESH geometry each render so a geometry/Samples change is seen as a changed
        // payload (the render cache compares geometry by reference; a reused instance mutated in place looks unchanged).
        var count = (int)System.Math.Max(Samples, 2u);
        var pts = MathHelper.ResampleByArcLength(
            MathHelper.GetCubicBezier(StartPoint, ControlPoint1, ControlPoint2, EndPoint, 256), count);
        var geometry = new StreamGeometry();
        geometry.Open().BeginFigure(pts[0], false, false).PolylineLineTo(pts.Skip(1), true);
        context.ForControl(this).DrawGeometry(Fill, geometry, GetPen());
    }
}