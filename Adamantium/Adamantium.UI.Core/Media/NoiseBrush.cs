using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>A PROCEDURAL fractal-noise fill (fractional Brownian motion over texture-free simplex noise) evaluated per
/// fragment - grain, organic backgrounds, film-grain overlays; resolution-independent, no texture. The noise value maps
/// <see cref="Color1"/> (low) -> <see cref="Color2"/> (high). WPF has no procedural noise at all. Bakes into the shared
/// procedural pattern batch (a noise "pattern type"), so it costs one instanced draw like the pattern brushes.</summary>
public sealed class NoiseBrush : Brush
{
    public NoiseBrush() { }

    // PAINT, all of them: the noise field is fill-relative, so changing any of these re-colours the same pixels - never the
    // element's shape or its layout (see Brush.Opacity).
    public static readonly AdamantiumProperty ScaleProperty = AdamantiumProperty.Register(nameof(Scale),
        typeof(double), typeof(NoiseBrush), new PropertyMetadata(40.0, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty OctavesProperty = AdamantiumProperty.Register(nameof(Octaves),
        typeof(int), typeof(NoiseBrush), new PropertyMetadata(4, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty SeedProperty = AdamantiumProperty.Register(nameof(Seed),
        typeof(double), typeof(NoiseBrush), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty LacunarityProperty = AdamantiumProperty.Register(nameof(Lacunarity),
        typeof(double), typeof(NoiseBrush), new PropertyMetadata(2.0, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty GainProperty = AdamantiumProperty.Register(nameof(Gain),
        typeof(double), typeof(NoiseBrush), new PropertyMetadata(0.5, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty Color1Property = AdamantiumProperty.Register(nameof(Color1),
        typeof(Color), typeof(NoiseBrush), new PropertyMetadata(new Color(15, 23, 42, 255), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty Color2Property = AdamantiumProperty.Register(nameof(Color2),
        typeof(Color), typeof(NoiseBrush), new PropertyMetadata(new Color(148, 163, 184, 255), PropertyMetadataOptions.AffectsPaint));

    /// <summary>The base noise cell in logical px (bigger = coarser). Default 40.</summary>
    public double Scale
    {
        get => GetValue<double>(ScaleProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(ScaleProperty, value);
        }
    }

    /// <summary>Number of FBM octaves summed (more = finer detail). Capped at 8 in the shader. Default 4.</summary>
    public int Octaves
    {
        get => GetValue<int>(OctavesProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(OctavesProperty, value);
        }
    }

    /// <summary>Shifts the noise field so two otherwise-identical brushes differ. Default 0.</summary>
    public double Seed
    {
        get => GetValue<double>(SeedProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(SeedProperty, value);
        }
    }

    /// <summary>Frequency multiplier per octave. Default 2.</summary>
    public double Lacunarity
    {
        get => GetValue<double>(LacunarityProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(LacunarityProperty, value);
        }
    }

    /// <summary>Amplitude multiplier per octave. Default 0.5.</summary>
    public double Gain
    {
        get => GetValue<double>(GainProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(GainProperty, value);
        }
    }

    /// <summary>The colour at low noise values.</summary>
    public Color Color1
    {
        get => GetValue<Color>(Color1Property);
        set
        {
            if (IsFrozen) return;
            SetValue(Color1Property, value);
        }
    }

    /// <summary>The colour at high noise values.</summary>
    public Color Color2
    {
        get => GetValue<Color>(Color2Property);
        set
        {
            if (IsFrozen) return;
            SetValue(Color2Property, value);
        }
    }

    protected override Brush CreateClone() =>
        new NoiseBrush
        {
            Scale = Scale,
            Octaves = Octaves,
            Seed = Seed,
            Lacunarity = Lacunarity,
            Gain = Gain,
            Color1 = Color1,
            Color2 = Color2,
            Opacity = Opacity
        };
}
