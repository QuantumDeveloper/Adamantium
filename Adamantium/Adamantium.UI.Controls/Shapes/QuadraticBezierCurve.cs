using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Shapes;

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
    
    protected override void OnRender(IDrawingContext context)
    {
        // Samples fully controls curvature (count 2 -> straight chord). FRESH geometry (see CubicBezierCurve).
        var count = (int)System.Math.Max(Samples, 2u);
        var pts = MathHelper.ResampleByArcLength(
            MathHelper.GetQuadraticBezier(StartPoint, ControlPoint, EndPoint, 256), count);
        var geometry = new StreamGeometry();
        geometry.Open().BeginFigure(pts[0], false, false).PolylineLineTo(pts.Skip(1), true);

        // Open, stroke-only curve: fill with Fill (null = none), not Stroke, and leave the figure open.
        context.ForControl(this).DrawGeometry(Fill, geometry, GetPen());
    }
}