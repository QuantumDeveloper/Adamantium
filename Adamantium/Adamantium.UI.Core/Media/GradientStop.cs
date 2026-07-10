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

    public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Color),
        typeof(Color), typeof(GradientStop), new PropertyMetadata(Colors.Transparent));

    public static readonly AdamantiumProperty OffsetProperty = AdamantiumProperty.Register(nameof(Offset),
        typeof(double), typeof(GradientStop), new PropertyMetadata(0.0));

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
