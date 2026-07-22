using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>A gradient swept ANGULARLY around <see cref="Center"/>: the stops lay out over one full turn (offset 0 at
/// <see cref="StartAngle"/>, offset 1 back at the same ray), so the last stop meets the first at the start ray. This is the
/// CSS <c>conic-gradient</c> - a colour wheel, a pie chart, a radial-menu sweep, a spinner. Coordinates are RELATIVE to the
/// filled bounds (0..1); <see cref="StartAngle"/> is in degrees, 0 = up (12 o'clock), increasing clockwise.</summary>
public sealed class ConicGradientBrush : GradientBrush
{
    public ConicGradientBrush() { }

    public ConicGradientBrush(GradientStopCollection stops) : base(stops) { }

    // PAINT, both: the sweep is RELATIVE to the filled bounds, so moving the centre or turning the start angle re-colours
    // the same pixels - never the element's shape or its layout (see Brush.Opacity).
    public static readonly AdamantiumProperty CenterProperty = AdamantiumProperty.Register(nameof(Center),
        typeof(Vector2), typeof(ConicGradientBrush), new PropertyMetadata(new Vector2(0.5f, 0.5f), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty StartAngleProperty = AdamantiumProperty.Register(nameof(StartAngle),
        typeof(double), typeof(ConicGradientBrush), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>The point (relative) the angle sweeps around. Default is the centre of the bounds.</summary>
    public Vector2 Center
    {
        get => GetValue<Vector2>(CenterProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }
            SetValue(CenterProperty, value);
        }
    }

    /// <summary>Where offset 0 sits, in degrees; 0 = up (12 o'clock), increasing clockwise.</summary>
    public double StartAngle
    {
        get => GetValue<double>(StartAngleProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }
            SetValue(StartAngleProperty, value);
        }
    }

    protected override Brush CreateClone() =>
        new ConicGradientBrush(CopyStops())
        {
            Center = Center,
            StartAngle = StartAngle,
            SpreadMethod = SpreadMethod,
            Opacity = Opacity
        };
}
