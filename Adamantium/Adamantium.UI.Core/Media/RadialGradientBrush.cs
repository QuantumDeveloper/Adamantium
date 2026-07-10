using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>A gradient radiating outward from <see cref="GradientOrigin"/> (offset 0) to the ellipse boundary defined by
/// <see cref="Center"/> + <see cref="RadiusX"/>/<see cref="RadiusY"/> (offset 1). All values are RELATIVE to the filled
/// bounds (0..1), so the default (centre 0.5,0.5 / radius 0.5) inscribes the bounds.</summary>
public sealed class RadialGradientBrush : GradientBrush
{
    public RadialGradientBrush() { }

    public RadialGradientBrush(GradientStopCollection stops) : base(stops) { }

    public static readonly AdamantiumProperty CenterProperty = AdamantiumProperty.Register(nameof(Center),
        typeof(Vector2), typeof(RadialGradientBrush), new PropertyMetadata(new Vector2(0.5f, 0.5f)));

    public static readonly AdamantiumProperty GradientOriginProperty = AdamantiumProperty.Register(nameof(GradientOrigin),
        typeof(Vector2), typeof(RadialGradientBrush), new PropertyMetadata(new Vector2(0.5f, 0.5f)));

    public static readonly AdamantiumProperty RadiusXProperty = AdamantiumProperty.Register(nameof(RadiusX),
        typeof(double), typeof(RadialGradientBrush), new PropertyMetadata(0.5));

    public static readonly AdamantiumProperty RadiusYProperty = AdamantiumProperty.Register(nameof(RadiusY),
        typeof(double), typeof(RadialGradientBrush), new PropertyMetadata(0.5));

    /// <summary>The ellipse centre (relative). The stops lay out from <see cref="GradientOrigin"/> to this ellipse's edge.</summary>
    public Vector2 Center
    {
        get => GetValue<Vector2>(CenterProperty);
        set => SetValue(CenterProperty, value);
    }

    /// <summary>Where offset 0 sits (relative). Defaults to the centre; move it for an off-centre "spotlight".</summary>
    public Vector2 GradientOrigin
    {
        get => GetValue<Vector2>(GradientOriginProperty);
        set => SetValue(GradientOriginProperty, value);
    }

    public double RadiusX
    {
        get => GetValue<double>(RadiusXProperty);
        set => SetValue(RadiusXProperty, value);
    }

    public double RadiusY
    {
        get => GetValue<double>(RadiusYProperty);
        set => SetValue(RadiusYProperty, value);
    }
}
