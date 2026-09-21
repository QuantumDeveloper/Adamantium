using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Shapes;

/// <summary>A NURBS curve: a <see cref="BSpline"/> (degree + uniform/non-uniform knots) PLUS per-control-point
/// <see cref="Weights"/> - the "R" (rational). Weights are the one thing a B-spline lacks: they pull the curve toward a
/// point and let it trace an EXACT conic.</summary>
public class Nurbs : BSpline
{
    public Nurbs()
    {
    }

    /// <summary>Per-control-point weights. Higher weight pulls the curve TOWARD that point; equal (or null) weights = a
    /// plain B-spline. Length should match <see cref="Polyline.Points"/>; missing entries default to 1.</summary>
    public static readonly AdamantiumProperty WeightsProperty =
        AdamantiumProperty.Register(nameof(Weights), typeof(IReadOnlyList<double>), typeof(Nurbs),
            new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public IReadOnlyList<double> Weights
    {
        get => GetValue<IReadOnlyList<double>>(WeightsProperty);
        set => SetValue(WeightsProperty, value);
    }

    protected override void OnRender(IDrawingContext context)
    {
        // Same eval as BSpline but WITH weights. Samples fully controls curvature. FRESH geometry (see CubicBezierCurve).
        var count = (int)System.Math.Max(Samples, 2u);
        var pts = MathHelper.ResampleByArcLength(
            MathHelper.GetNurbsCurve(Points, Weights, ResolveDegree(), IsUniform, 1.0 / 256.0), count);
        var geometry = new StreamGeometry { IsClosed = false };
        geometry.Open().BeginFigure(pts[0], false, false).PolylineLineTo(pts.Skip(1), true);

        // Open, stroke-only curve: fill with Fill (null = none), not Stroke, and leave the figure open.
        context.ForControl(this).DrawGeometry(Fill, geometry, GetPen());
    }
}
