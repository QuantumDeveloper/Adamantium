using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>One colour marker in a gradient: a <see cref="Color"/> placed at a normalised <see cref="Offset"/> (0 = the
/// gradient's start, 1 = its end). The gradient interpolates between adjacent stops sorted by offset.</summary>
public sealed class GradientStop : AdamantiumComponent
{
    public GradientStop() { }

    public GradientStop(Color color, double offset)
    {
        Color = color;
        Offset = offset;
    }

    // PAINT: a stop is pure colour placement - the shimmer sweeps its band by animating Offset alone, and nothing about the
    // element it paints changes (see Brush.Opacity). A stop is not itself a Brush, so its change reaches the drawing element
    // through the owning GradientBrush (GradientBrush.OnStopChanged -> RaiseChanged); the flag CLASSIFIES the change.
    public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Color),
        typeof(Color), typeof(GradientStop), new PropertyMetadata(Colors.Transparent, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty OffsetProperty = AdamantiumProperty.Register(nameof(Offset),
        typeof(double), typeof(GradientStop), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsPaint));

    public Color Color
    {
        get => GetValue<Color>(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public double Offset
    {
        get => GetValue<double>(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    public override string ToString() => $"{Color} @ {Offset}";
}
