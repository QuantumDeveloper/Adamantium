using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core.RoutedEvents;
namespace Adamantium.UI.Core.Media;

/// <summary>
/// A shape's shadow cast on what lies behind it - what makes a control look LIFTED. It has a DIRECTION, which is the
/// whole difference from an <see cref="Aura"/>: the offset says where the light is, and the spread says how high the
/// element floats. Same vocabulary as CSS <c>box-shadow</c>, so a design handed over in those terms transfers as is.
/// <para>Drawn OUTSIDE the element's <see cref="Adamantium.UI.Core.IUIComponent.Bounds"/> (or inside it, with
/// <see cref="Inner"/>). The engine does NOT grow the layout to fit it: bounds drive draw order and the repaint region,
/// and widening them from here would mean rewriting the shared render path. Leave the room yourself with
/// <c>Margin</c> - a shadow with no margin is clipped by the first ancestor that clips, and nothing will say so.</para>
/// </summary>
public sealed class Shadow : AdamantiumComponent
{
    public static readonly AdamantiumProperty OffsetXProperty = AdamantiumProperty.Register(nameof(OffsetX),
        typeof(double), typeof(Shadow), new PropertyMetadata(0.0, OnChanged));

    public static readonly AdamantiumProperty OffsetYProperty = AdamantiumProperty.Register(nameof(OffsetY),
        typeof(double), typeof(Shadow), new PropertyMetadata(4.0, OnChanged));

    public static readonly AdamantiumProperty BlurRadiusProperty = AdamantiumProperty.Register(nameof(BlurRadius),
        typeof(double), typeof(Shadow), new PropertyMetadata(12.0, OnChanged));

    public static readonly AdamantiumProperty SpreadProperty = AdamantiumProperty.Register(nameof(Spread),
        typeof(double), typeof(Shadow), new PropertyMetadata(0.0, OnChanged));

    public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Color),
        typeof(Color), typeof(Shadow), new PropertyMetadata(Colors.Black, OnChanged));

    public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
        typeof(double), typeof(Shadow), new PropertyMetadata(0.35, OnChanged));

    public static readonly AdamantiumProperty InnerProperty = AdamantiumProperty.Register(nameof(Inner),
        typeof(bool), typeof(Shadow), new PropertyMetadata(false, OnChanged));

    /// <summary>How far the shadow is thrown sideways, in pixels - i.e. where the light is.</summary>
    public double OffsetX
    {
        get => GetValue<double>(OffsetXProperty);
        set => SetValue(OffsetXProperty, value);
    }

    /// <summary>How far the shadow is thrown down, in pixels. Positive is downward, as a light from above gives.</summary>
    public double OffsetY
    {
        get => GetValue<double>(OffsetYProperty);
        set => SetValue(OffsetYProperty, value);
    }

    /// <summary>How SOFT the edge is: the width, in pixels, over which the shadow fades out.</summary>
    public double BlurRadius
    {
        get => GetValue<double>(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    /// <summary>How far the shadow is INFLATED past the shape before it starts to fade - what reads as height above the
    /// surface. Negative shrinks it, for a shadow that only peeks out from under an edge.</summary>
    public double Spread
    {
        get => GetValue<double>(SpreadProperty);
        set => SetValue(SpreadProperty, value);
    }

    public Color Color
    {
        get => GetValue<Color>(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Multiplies the colour's own alpha. A real shadow is never opaque - the default is deliberately low.</summary>
    public double Opacity
    {
        get => GetValue<double>(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    /// <summary>Cast the shadow INSIDE the shape instead of behind it (CSS <c>inset</c>): the pressed / recessed look.</summary>
    public bool Inner
    {
        get => GetValue<bool>(InnerProperty);
        set => SetValue(InnerProperty, value);
    }

    /// <summary>Raised when any value changes, so the element wearing it can re-record.</summary>
    public event EventHandler Changed;

    private static void OnChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
        => (d as Shadow)?.Changed?.Invoke(d, EventArgs.Empty);
}
