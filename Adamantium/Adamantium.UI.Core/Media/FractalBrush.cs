using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>A PROCEDURAL escape-time fractal fill (Julia / Mandelbrot) iterated per fragment - resolution-independent, no
/// texture. The fragment maps to the complex plane (<see cref="Center"/> + <see cref="Zoom"/>), z = z²+C is iterated up to
/// <see cref="Iterations"/>, and the smooth escape count maps <see cref="Color1"/> -> <see cref="Color2"/> (the interior
/// is black). With <see cref="Animate"/> on, <see cref="C"/> drifts on its own each frame (<see cref="MorphSpeed"/>) so the
/// Julia set morphs in real time. Zoom is limited by float32 precision (~1e5) - true "infinite" zoom needs double/perturbation.</summary>
public sealed class FractalBrush : Brush
{
    public FractalBrush() { }

    // A frozen render snapshot (CreateClone) copies Animate but must NOT touch the shared FractalClock ref-count - only a
    // LIVE brush is a real owner. Set true FIRST in CreateClone, before the clone's Animate is assigned.
    private bool _suppressClock;

    // PAINT, all of them: the fractal is fill-relative, so changing any of these re-colours the same pixels - never a
    // shape or layout change (see Brush.Opacity).
    public static readonly AdamantiumProperty FractalProperty = AdamantiumProperty.Register(nameof(Fractal),
        typeof(FractalType), typeof(FractalBrush), new PropertyMetadata(FractalType.Julia, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty CenterProperty = AdamantiumProperty.Register(nameof(Center),
        typeof(Vector2), typeof(FractalBrush), new PropertyMetadata(new Vector2(0, 0), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty ZoomProperty = AdamantiumProperty.Register(nameof(Zoom),
        typeof(double), typeof(FractalBrush), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty IterationsProperty = AdamantiumProperty.Register(nameof(Iterations),
        typeof(int), typeof(FractalBrush), new PropertyMetadata(120, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty CProperty = AdamantiumProperty.Register(nameof(C),
        typeof(Vector2), typeof(FractalBrush), new PropertyMetadata(new Vector2(-0.8f, 0.156f), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty Color1Property = AdamantiumProperty.Register(nameof(Color1),
        typeof(Color), typeof(FractalBrush), new PropertyMetadata(new Color(11, 18, 43, 255), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty Color2Property = AdamantiumProperty.Register(nameof(Color2),
        typeof(Color), typeof(FractalBrush), new PropertyMetadata(new Color(34, 211, 238, 255), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty AnimateProperty = AdamantiumProperty.Register(nameof(Animate),
        typeof(bool), typeof(FractalBrush), new PropertyMetadata(false, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty MorphSpeedProperty = AdamantiumProperty.Register(nameof(MorphSpeed),
        typeof(double), typeof(FractalBrush), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty FormulaProperty = AdamantiumProperty.Register(nameof(Formula),
        typeof(FractalFormula), typeof(FractalBrush), new PropertyMetadata(FractalFormula.Quadratic, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty PowerProperty = AdamantiumProperty.Register(nameof(Power),
        typeof(double), typeof(FractalBrush), new PropertyMetadata(2.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>The C-mode: Julia (C = a constant, the fragment is z0) or Mandelbrot (C = the fragment, z0 = 0). Orthogonal
    /// to <see cref="Formula"/>. Default Julia (the one that morphs). Ignored by <see cref="FractalFormula.Newton"/>.</summary>
    public FractalType Fractal
    {
        get => GetValue<FractalType>(FractalProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(FractalProperty, value);
        }
    }

    /// <summary>The iteration formula (Quadratic z²+c, Burning Ship, Tricorn, Celtic, Multibrot, Newton). Orthogonal to
    /// the <see cref="Fractal"/> C-mode, so each formula has a Julia and a Mandelbrot form. Default Quadratic.</summary>
    public FractalFormula Formula
    {
        get => GetValue<FractalFormula>(FormulaProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(FormulaProperty, value);
        }
    }

    /// <summary>The exponent d for <see cref="FractalFormula.Multibrot"/> (z^d+c): d=2 is the ordinary set, higher d gives
    /// (d−1)-fold "snowflake" symmetry. Ignored by the other formulas. Under <see cref="Animate"/> the Multibrot power
    /// breathes around this value instead of drifting C. Default 2.</summary>
    public double Power
    {
        get => GetValue<double>(PowerProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(PowerProperty, value);
        }
    }

    /// <summary>Complex-plane point at the centre of the fill (pan). Default origin.</summary>
    public Vector2 Center
    {
        get => GetValue<Vector2>(CenterProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(CenterProperty, value);
        }
    }

    /// <summary>Magnification. 1 shows the whole set; higher zooms in. Limited by float32 (~1e5).</summary>
    public double Zoom
    {
        get => GetValue<double>(ZoomProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(ZoomProperty, value);
        }
    }

    /// <summary>Max iterations before a point is deemed "inside" (detail + cost). Default 120.</summary>
    public int Iterations
    {
        get => GetValue<int>(IterationsProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(IterationsProperty, value);
        }
    }

    /// <summary>The Julia constant C (ignored for Mandelbrot). Small changes reshape the whole set - what auto-morph drifts.</summary>
    public Vector2 C
    {
        get => GetValue<Vector2>(CProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(CProperty, value);
        }
    }

    /// <summary>Colour at low escape counts (near/inside the set edge).</summary>
    public Color Color1
    {
        get => GetValue<Color>(Color1Property);
        set
        {
            if (IsFrozen) return;
            SetValue(Color1Property, value);
        }
    }

    /// <summary>Colour at high escape counts (the fast-escaping outside).</summary>
    public Color Color2
    {
        get => GetValue<Color>(Color2Property);
        set
        {
            if (IsFrozen) return;
            SetValue(Color2Property, value);
        }
    }

    /// <summary>When true, <see cref="C"/> drifts on its own every frame so the (Julia) set morphs in real time. Ref-counts
    /// the shared <see cref="FractalClock"/> so the render loop keeps presenting while it's on.</summary>
    public bool Animate
    {
        get => GetValue<bool>(AnimateProperty);
        set
        {
            if (IsFrozen) return;
            var was = GetValue<bool>(AnimateProperty);
            SetValue(AnimateProperty, value);
            if (_suppressClock || value == was) return;
            if (value)
            {
                FractalClock.Speed = MorphSpeed;   // seed the shared morph speed before the phase starts advancing
                FractalClock.Acquire();
            }
            else
            {
                FractalClock.Release();
            }
        }
    }

    /// <summary>How fast the auto-morph drifts C. Default 1. Changing it while morphing accelerates/decelerates the current
    /// drift (the phase keeps its value and just advances faster/slower) - it never resets the morph.</summary>
    public double MorphSpeed
    {
        get => GetValue<double>(MorphSpeedProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(MorphSpeedProperty, value);
            if (!_suppressClock && Animate) FractalClock.Speed = value;
        }
    }

    protected override Brush CreateClone() =>
        new FractalBrush
        {
            _suppressClock = true,   // the frozen snapshot must not ref-count the shared morph clock
            Fractal = Fractal,
            Formula = Formula,
            Power = Power,
            Center = Center,
            Zoom = Zoom,
            Iterations = Iterations,
            C = C,
            Color1 = Color1,
            Color2 = Color2,
            Animate = Animate,
            MorphSpeed = MorphSpeed,
            Opacity = Opacity
        };
}
