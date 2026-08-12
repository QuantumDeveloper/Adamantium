using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core.RoutedEvents;
﻿namespace Adamantium.UI.Core.Media;

/// <summary>
/// A soft band of colour around a shape's outline - what makes a control look LIT rather than lifted. It has no
/// direction: an aura reaches the same distance on every side, which is exactly what separates it from a
/// <see cref="Shadow"/> and why the two are different properties rather than one with the offset zeroed.
/// <para>Drawn OUTSIDE the element's <see cref="Adamantium.UI.Core.IUIComponent.Bounds"/> (or inside it, with
/// <see cref="Inner"/>). The engine does NOT grow the layout to fit it: bounds drive draw order and the repaint region,
/// and widening them from here would mean rewriting the shared render path. Leave the room yourself with
/// <c>Margin</c> - an aura with no margin is clipped by the first ancestor that clips, and nothing will say so.</para>
/// </summary>
public sealed class Aura : AdamantiumComponent
{
    public static readonly AdamantiumProperty RadiusProperty = AdamantiumProperty.Register(nameof(Radius),
        typeof(double), typeof(Aura), new PropertyMetadata(8.0, OnChanged));

    public static readonly AdamantiumProperty SpreadProperty = AdamantiumProperty.Register(nameof(Spread),
        typeof(double), typeof(Aura), new PropertyMetadata(0.0, OnChanged));

    public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Color),
        typeof(Color), typeof(Aura), new PropertyMetadata(Colors.White, OnChanged));

    public static readonly AdamantiumProperty OpacityProperty = AdamantiumProperty.Register(nameof(Opacity),
        typeof(double), typeof(Aura), new PropertyMetadata(1.0, OnChanged));

    public static readonly AdamantiumProperty InnerProperty = AdamantiumProperty.Register(nameof(Inner),
        typeof(bool), typeof(Aura), new PropertyMetadata(false, OnChanged));

    /// <summary>How far the glow REACHES past the outline, in pixels, before it has faded to nothing.</summary>
    public double Radius
    {
        get => GetValue<double>(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>Pixels of FULL-strength colour before the fade starts - a solid rim the falloff begins outside of.</summary>
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

    /// <summary>Multiplies the colour's own alpha - the knob to reach for when animating the glow on and off.</summary>
    public double Opacity
    {
        get => GetValue<double>(OpacityProperty);
        set => SetValue(OpacityProperty, value);
    }

    /// <summary>Glow INWARD from the outline instead of outward: the lit-from-within look, and the only form that needs
    /// no margin at all because it never leaves the element.</summary>
    public bool Inner
    {
        get => GetValue<bool>(InnerProperty);
        set => SetValue(InnerProperty, value);
    }

    /// <summary>Raised when any value changes, so the element wearing it can re-record.</summary>
    public event EventHandler Changed;

    private static void OnChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
        => (d as Aura)?.Changed?.Invoke(d, EventArgs.Empty);
}

