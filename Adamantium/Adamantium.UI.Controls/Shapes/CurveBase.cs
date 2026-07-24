using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Shapes;

/// <summary>Common base for the sampled curve shapes (Bézier, B-spline, NURBS, poly-line). Owns <see cref="Samples"/> -
/// the number of points the curve is flattened into, spaced EVENLY by arc length. Samples fully controls the curvature:
/// 2 (or fewer) is a straight chord end-to-end; more points trace the curve more finely.</summary>
public abstract class CurveBase : Shape
{
    /// <summary>Number of points the curve is flattened into (spaced evenly by arc length). &lt;=2 = a straight chord.</summary>
    public static readonly AdamantiumProperty SamplesProperty =
        AdamantiumProperty.Register(nameof(Samples), typeof(uint), typeof(CurveBase),
            new PropertyMetadata((uint)32, PropertyMetadataOptions.AffectsMeasure));

    public uint Samples
    {
        get => GetValue<uint>(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }
}
