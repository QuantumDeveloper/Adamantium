using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Shapes;

/// <summary>An open B-spline through its <see cref="Polyline.Points"/> (control points) with a configurable polynomial
/// <see cref="Degree"/> and uniform / non-uniform knots (<see cref="IsUniform"/>). <see cref="Nurbs"/> extends it with
/// per-control-point weights.</summary>
public class BSpline : Polyline
{
    public BSpline()
    {
    }

    /// <summary>Knot spacing. false = non-uniform / clamped (reaches the end points, the usual look); true = uniform.</summary>
    public static readonly AdamantiumProperty IsUniformProperty =
        AdamantiumProperty.Register(nameof(IsUniform), typeof(bool), typeof(BSpline),
            new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>Piecewise-polynomial degree. 0 = automatic (Points.Count - 1). Otherwise clamped to [1, Points.Count - 1].</summary>
    public static readonly AdamantiumProperty DegreeProperty =
        AdamantiumProperty.Register(nameof(Degree), typeof(uint), typeof(BSpline),
            new PropertyMetadata((uint)0, PropertyMetadataOptions.AffectsMeasure));

    public bool IsUniform
    {
        get => GetValue<bool>(IsUniformProperty);
        set => SetValue(IsUniformProperty, value);
    }

    public uint Degree
    {
        get => GetValue<uint>(DegreeProperty);
        set => SetValue(DegreeProperty, value);
    }

    /// <summary>Degree resolved for the current Points: 0 = auto (Points.Count - 1), else clamped to [1, Points.Count - 1].</summary>
    protected int ResolveDegree()
    {
        var max = System.Math.Max(Points.Count - 1, 1);
        return Degree > 0 ? System.Math.Clamp((int)Degree, 1, max) : max;
    }

    protected override void OnRender(IDrawingContext context)
    {
        // Samples fully controls curvature (count 2 -> straight chord). FRESH geometry (see CubicBezierCurve). A B-spline
        // is a NON-rational NURBS: the general basis with all weights = 1 (null).
        var count = (int)System.Math.Max(Samples, 2u);
        var pts = MathHelper.ResampleByArcLength(
            MathHelper.GetNurbsCurve(Points, null, ResolveDegree(), IsUniform, 1.0 / 256.0), count);
        var geometry = new StreamGeometry { IsClosed = false };
        geometry.Open().BeginFigure(pts[0], false, false).PolylineLineTo(pts.Skip(1), true);

        // Open, stroke-only curve: fill with Fill (null = none), not Stroke, and leave the figure open.
        context.ForControl(this).DrawGeometry(Fill, geometry, GetPen());
    }
}
