using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>A PROCEDURAL fractal-noise fill (fractional Brownian motion over texture-free simplex noise) evaluated per
/// fragment - grain, organic backgrounds, film-grain overlays; resolution-independent, no texture. The noise value maps
/// <see cref="Color1"/> (low) -> <see cref="Color2"/> (high). WPF has no procedural noise at all. Bakes into the shared
/// procedural pattern batch (a noise "pattern type"), so it costs one instanced draw like the pattern brushes.</summary>
public sealed class NoiseBrush : Brush
{
    public NoiseBrush() { }

    // A frozen render snapshot (CreateClone) copies Animate but must NOT touch the shared NoiseClock ref-count - only a LIVE
    // brush is a real owner. Set true FIRST in CreateClone, before the clone's Animate is assigned. Mirrors FractalBrush.
    private bool _suppressClock;

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

    // Optional MID colour: with a non-zero alpha the noise maps through a 3-colour gradient-map ramp (Color1 -> MidColor ->
    // Color2) instead of the plain two-colour duotone - terrain / heat-map / lava looks. Default transparent = off.
    public static readonly AdamantiumProperty MidColorProperty = AdamantiumProperty.Register(nameof(MidColor),
        typeof(Color), typeof(NoiseBrush), new PropertyMetadata(new Color(0, 0, 0, 0), PropertyMetadataOptions.AffectsPaint));

    // The base noise function FBM layers - swaps the whole look (smooth simplex/perlin vs blockier value vs cellular Worley).
    public static readonly AdamantiumProperty NoiseTypeProperty = AdamantiumProperty.Register(nameof(NoiseType),
        typeof(NoiseType), typeof(NoiseBrush), new PropertyMetadata(NoiseType.Simplex, PropertyMetadataOptions.AffectsPaint));

    // Flow animation: with Animate on, the Worley feature points orbit over the shared NoiseClock time so the cells "flow"
    // in place (the classic animated-Voronoi look). The other noise types slowly drift instead. No re-bake - the shader
    // reads the clock each frame while the retained draw replays (like FractalBrush.Animate).
    public static readonly AdamantiumProperty AnimateProperty = AdamantiumProperty.Register(nameof(Animate),
        typeof(bool), typeof(NoiseBrush), new PropertyMetadata(false, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty FlowSpeedProperty = AdamantiumProperty.Register(nameof(FlowSpeed),
        typeof(double), typeof(NoiseBrush), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsPaint));

    // CombustibleVoronoi only: true = the built-in blackbody FIRE palette; false = colour it through this brush's own
    // Color1 -> MidColor -> Color2 ramp (so any palette - ice, toxic, etc.). Ignored by every other noise type.
    public static readonly AdamantiumProperty UseFirePaletteProperty = AdamantiumProperty.Register(nameof(UseFirePalette),
        typeof(bool), typeof(NoiseBrush), new PropertyMetadata(true, PropertyMetadataOptions.AffectsPaint));

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

    /// <summary>Optional MID colour for a 3-colour gradient-map ramp (Color1 -> MidColor -> Color2). Transparent = off.</summary>
    public Color MidColor
    {
        get => GetValue<Color>(MidColorProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(MidColorProperty, value);
        }
    }

    /// <summary>Which base noise function FBM layers. Default Simplex.</summary>
    public NoiseType NoiseType
    {
        get => GetValue<NoiseType>(NoiseTypeProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(NoiseTypeProperty, value);
        }
    }

    /// <summary>The phase to bake when NOT animating: the shared clock value captured the moment Animate turned off, so the
    /// field HOLDS that exact frame instead of snapping back to phase 0. The render side reads this via the pattern record.
    /// While animating it's ignored (the shader uses the live clock, which - once the ticker holds on release - resumes from
    /// here, so re-enabling continues seamlessly).</summary>
    public double FrozenPhase { get; private set; }

    /// <summary>When true, the noise flows over time (Worley feature points orbit -> cells flow in place; other types drift).
    /// Ref-counts the shared <see cref="NoiseClock"/> so the render loop keeps presenting while it's on.</summary>
    public bool Animate
    {
        get => GetValue<bool>(AnimateProperty);
        set
        {
            if (IsFrozen) return;
            var was = GetValue<bool>(AnimateProperty);
            // Capture the frozen frame BEFORE SetValue fires the paint change (which triggers the re-bake that reads it).
            if (!_suppressClock && was && !value)
            {
                FrozenPhase = NoiseClock.Time;
            }
            SetValue(AnimateProperty, value);
            if (_suppressClock || value == was) return;
            if (value)
            {
                NoiseClock.Speed = FlowSpeed;   // seed the shared flow speed before the phase starts advancing
                NoiseClock.Acquire();
            }
            else
            {
                NoiseClock.Release();
            }
        }
    }

    /// <summary>How fast the flow advances. Default 1. Changing it while animating accelerates/decelerates the current flow
    /// (the phase keeps its value and just advances faster/slower) - it never resets the flow.</summary>
    public double FlowSpeed
    {
        get => GetValue<double>(FlowSpeedProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(FlowSpeedProperty, value);
            if (!_suppressClock && Animate) NoiseClock.Speed = value;
        }
    }

    /// <summary>CombustibleVoronoi only: use the built-in blackbody fire palette (true) or this brush's Color1/MidColor/Color2
    /// ramp (false). Default true. Ignored by other noise types.</summary>
    public bool UseFirePalette
    {
        get => GetValue<bool>(UseFirePaletteProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(UseFirePaletteProperty, value);
        }
    }

    protected override Brush CreateClone() =>
        new NoiseBrush
        {
            _suppressClock = true,   // the frozen snapshot must not ref-count the shared flow clock
            Scale = Scale,
            Octaves = Octaves,
            Seed = Seed,
            Lacunarity = Lacunarity,
            Gain = Gain,
            Color1 = Color1,
            Color2 = Color2,
            MidColor = MidColor,
            NoiseType = NoiseType,
            Animate = Animate,
            FlowSpeed = FlowSpeed,
            FrozenPhase = FrozenPhase,
            UseFirePalette = UseFirePalette,
            Opacity = Opacity
        };
}
