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
    public static readonly AdamantiumProperty IsEnabledProperty = AdamantiumProperty.Register(nameof(IsEnabled),
        typeof(bool), typeof(Aura), new PropertyMetadata(true, OnChanged));

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

    /// <summary>Switch the glow off without losing its settings - what a trigger or a binding wants (a focus ring that
    /// comes and goes) and what zeroing the radius or the opacity would only fake, at the cost of somewhere to keep the
    /// real values while it is off.</summary>
    public bool IsEnabled
    {
        get => GetValue<bool>(IsEnabledProperty);
        set
        {
            var wasLiving = IsLiving;
            SetValue(IsEnabledProperty, value);
            UpdateClock(wasLiving);
        }
    }

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

    // --- Living aura -------------------------------------------------------------------------------------------------
    // A still glow is a rim of colour; a LIVING one breathes - the reach wanders along the outline and drifts over time,
    // and the colour travels a palette. Opt-in by setting Turbulence (and usually Flow): at zero this is exactly the
    // cheap band above, drawn by the plain pass, paying nothing for a feature it is not using.

    public static readonly AdamantiumProperty TurbulenceProperty = AdamantiumProperty.Register(nameof(Turbulence),
        typeof(double), typeof(Aura), new PropertyMetadata(0.0, OnChanged));

    public static readonly AdamantiumProperty FlowProperty = AdamantiumProperty.Register(nameof(Flow),
        typeof(double), typeof(Aura), new PropertyMetadata(0.5, OnChanged));

    public static readonly AdamantiumProperty DetailProperty = AdamantiumProperty.Register(nameof(Detail),
        typeof(double), typeof(Aura), new PropertyMetadata(3.0, OnChanged));

    public static readonly AdamantiumProperty PaletteProperty = AdamantiumProperty.Register(nameof(Palette),
        typeof(GradientStopCollection), typeof(Aura), new PropertyMetadata(null, OnChanged));

    /// <summary>How far the reach WANDERS, as a fraction of <see cref="Radius"/>. 0 = a still, even band (and the cheap
    /// pass); 1 = tongues that reach out and fall back. This is the switch: everything else here only matters above 0.</summary>
    public double Turbulence
    {
        get => GetValue<double>(TurbulenceProperty);
        set
        {
            var wasLiving = IsLiving;
            SetValue(TurbulenceProperty, value);
            UpdateClock(wasLiving);
        }
    }

    // HOLD the shared flow clock while alive. It is reference-counted and only ticks for whoever asked - so without this
    // the aura would read a phase that never advances unless an animated noise brush happened to be on screen, and it
    // would breathe only by coincidence. Switched off it lets the clock go, rather than keeping a ticker for nothing.
    private void UpdateClock(bool wasLiving)
    {
        if (IsLiving == wasLiving) return;
        if (IsLiving) NoiseClock.Acquire();
        else NoiseClock.Release();
    }

    /// <summary>How fast it drifts. 0 holds the wander still - the same uneven glow, frozen.</summary>
    public double Flow
    {
        get => GetValue<double>(FlowProperty);
        set => SetValue(FlowProperty, value);
    }

    /// <summary>How many tongues run around the outline: a few broad ones, or many fine ones.</summary>
    public double Detail
    {
        get => GetValue<double>(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>The colours it travels through, sampled by the wander rather than by any direction - which is what makes
    /// it read as ALIVE rather than as a gradient. Empty (the default) means the single <see cref="Color"/>.</summary>
    public GradientStopCollection Palette
    {
        get => GetValue<GradientStopCollection>(PaletteProperty);
        set => SetValue(PaletteProperty, value);
    }

    /// <summary>Whether this aura needs the living pass at all - i.e. whether the reach wanders.</summary>
    public bool IsLiving => IsEnabled && Turbulence > 0.0;

    /// <summary>Raised when any value changes, so the element wearing it can re-record.</summary>
    public event EventHandler Changed;

    private static void OnChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
        => (d as Aura)?.Changed?.Invoke(d, EventArgs.Empty);
}

