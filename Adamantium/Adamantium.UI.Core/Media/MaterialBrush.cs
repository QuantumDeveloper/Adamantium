using Adamantium.Mathematics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Media;

/// <summary>A BACKDROP MATERIAL: a fill made from what is already drawn behind the element - frosted glass, a tinted
/// pane, a lens. WinUI calls the first two Acrylic and Mica; the third is the material Apple introduced as Liquid Glass.
///
/// <para>A BRUSH, not an effect, deliberately: it goes wherever a brush goes - any Background, any Fill, any themed
/// resource - and it takes part in the theme's variant model like every other brush, instead of being a separate thing
/// bolted onto an element.</para>
///
/// <para>What separates the kinds is the SOURCE, not the numbers. Acrylic and LiquidGlass read the frame behind the
/// element and therefore follow whatever moves under them; Mica reads the window's background and therefore does not.
/// Give a LiquidGlass zero <see cref="Refraction"/> and it becomes acrylic - those two are one range - but no setting
/// turns either into Mica, because Mica is looking at something else.</para>
///
/// <para>Costs a capture of the region behind the element per frame for the first two (a downscaling blit - see
/// BackdropCapture), and nothing per frame for Mica.</para></summary>
public sealed class MaterialBrush : Brush
{
    public MaterialBrush() { }

    /// <summary>Which material. Changes the SOURCE and the pass, so unlike the numbers below it is not a paint-only
    /// tweak: a switch to or from Mica changes what is captured at all.</summary>
    public static readonly AdamantiumProperty MaterialProperty = AdamantiumProperty.Register(nameof(Material),
        typeof(MaterialType), typeof(MaterialBrush), new PropertyMetadata(MaterialType.Acrylic, PropertyMetadataOptions.AffectsPaint));

    // PAINT, all of them: they re-colour the same pixels and never touch shape or layout (see Brush.Opacity).

    /// <summary>Colour laid over the blurred capture. Without it a material is just a smeared copy of the wall behind
    /// it - the tint is what gives the pane its own identity and keeps text on top readable.</summary>
    public static readonly AdamantiumProperty TintColorProperty = AdamantiumProperty.Register(nameof(TintColor),
        typeof(Color), typeof(MaterialBrush), new PropertyMetadata(new Color(32, 34, 40, 255), PropertyMetadataOptions.AffectsPaint));

    /// <summary>How strongly the tint covers the capture, 0..1. Low values read as glass, high as painted plastic.
    /// </summary>
    public static readonly AdamantiumProperty TintOpacityProperty = AdamantiumProperty.Register(nameof(TintOpacity),
        typeof(double), typeof(MaterialBrush), new PropertyMetadata(0.6, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Extra blur on top of what the capture's downscale already gives, in device pixels. 0 keeps the capture
    /// as-is, which for Smoked/Tinted looks is exactly right.</summary>
    public static readonly AdamantiumProperty BlurAmountProperty = AdamantiumProperty.Register(nameof(BlurAmount),
        typeof(double), typeof(MaterialBrush), new PropertyMetadata(8.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Film grain over the result, 0..1. A tiny amount, around 0.03, is what stops a large blurred pane from
    /// banding - the same reason acrylic has noise in it at all.</summary>
    public static readonly AdamantiumProperty NoiseAmountProperty = AdamantiumProperty.Register(nameof(NoiseAmount),
        typeof(double), typeof(MaterialBrush), new PropertyMetadata(0.03, PropertyMetadataOptions.AffectsPaint));

    /// <summary>How far the capture is displaced at the shape's edge, in device pixels - the LENS strength. Ignored
    /// unless <see cref="Material"/> is LiquidGlass; zero there gives plain frosting.</summary>
    public static readonly AdamantiumProperty RefractionProperty = AdamantiumProperty.Register(nameof(Refraction),
        typeof(double), typeof(MaterialBrush), new PropertyMetadata(12.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>A picture for MICA to use instead of the desktop wallpaper; null leaves it reading the real one.
    ///
    /// <para>MICA ONLY, ignored by the other two: acrylic and liquid glass ARE what is directly beneath them, so a
    /// picture from elsewhere would not make them a variant of themselves. Needs no platform support - the picture is
    /// yours, so nothing is asked of the desktop.</para></summary>
    public static readonly AdamantiumProperty SourceProperty = AdamantiumProperty.Register(nameof(Source),
        typeof(Imaging.ImageSource), typeof(MaterialBrush), new PropertyMetadata(null, PropertyMetadataOptions.AffectsPaint, OnSourceChanged));

    /// <summary>What <see cref="Source"/> is pinned to. Ignored without one - the real wallpaper is pinned to the
    /// desktop by nature.</summary>
    public static readonly AdamantiumProperty AnchorProperty = AdamantiumProperty.Register(nameof(Anchor),
        typeof(MaterialAnchor), typeof(MaterialBrush), new PropertyMetadata(MaterialAnchor.Desktop, PropertyMetadataOptions.AffectsPaint));

    // ---- THE NAP: velvet only, and the first material properties that describe a SURFACE rather than a capture ----

    /// <summary>The cloth's own colour - what velvet looks like where no light is grazing it. Deep and desaturated is
    /// what reads as fabric; the sheen supplies all the brightness.</summary>
    public static readonly AdamantiumProperty NapColorProperty = AdamantiumProperty.Register(nameof(NapColor),
        typeof(Color), typeof(MaterialBrush), new PropertyMetadata(new Color(38, 20, 54, 255), PropertyMetadataOptions.AffectsPaint));

    /// <summary>The colour of the grazing-angle sheen - the light caught on the tips of the fibres. Keeping it apart
    /// from <see cref="NapColor"/> is what separates dyed silk velvet from wool: the same cloth lit differently.
    /// </summary>
    public static readonly AdamantiumProperty SheenColorProperty = AdamantiumProperty.Register(nameof(SheenColor),
        typeof(Color), typeof(MaterialBrush), new PropertyMetadata(new Color(228, 214, 255, 255), PropertyMetadataOptions.AffectsPaint));

    /// <summary>How coarse the surface's grain is, in device pixels. For velvet it is the fibre clump - small is silk,
    /// large is wool; for brushed metal it is the width of the grinding. One property for the whole surface branch,
    /// because it is one thing: the scale of the noise field whose gradient becomes the normal.</summary>
    public static readonly AdamantiumProperty GrainScaleProperty = AdamantiumProperty.Register(nameof(GrainScale),
        typeof(double), typeof(MaterialBrush), new PropertyMetadata(6.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Which way the grain runs, in degrees - the pile combed for velvet, the grinding for metal. Velvet
    /// brushed one way is darker than the same cloth brushed the other, and a brushed metal's highlight stretches
    /// ACROSS its grinding: the same asymmetry, and most of what makes either look real.</summary>
    public static readonly AdamantiumProperty GrainDirectionProperty = AdamantiumProperty.Register(nameof(GrainDirection),
        typeof(double), typeof(MaterialBrush), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>How rough the surface is, 0..1. On velvet it sets how far the sheen spreads (low over the whole fold,
    /// high in a band along the rim); on metal it is roughness in the ordinary sense - 0 is a mirror, high is cast
    /// iron. Defaulted to a POLISHED face, which is the plainest thing a metal can be.</summary>
    public static readonly AdamantiumProperty RoughnessProperty = AdamantiumProperty.Register(nameof(Roughness),
        typeof(double), typeof(MaterialBrush), new PropertyMetadata(0.08, PropertyMetadataOptions.AffectsPaint));

    /// <summary>How far the highlight is STRETCHED along the grain, 0..1. Zero is an isotropic surface (cast, or
    /// polished); high is the long smear a brushed finish drags across its grinding, which is the whole look of
    /// stainless steel. Metal only.</summary>
    public static readonly AdamantiumProperty AnisotropyProperty = AdamantiumProperty.Register(nameof(Anisotropy),
        typeof(double), typeof(MaterialBrush), new PropertyMetadata(0.7, PropertyMetadataOptions.AffectsPaint));

    /// <summary>The metal itself, as the colour it reflects at face-on incidence (F0). Grey is steel and aluminium;
    /// warm yellows are gold and brass; pink-orange is copper. Metal only.</summary>
    public static readonly AdamantiumProperty MetalColorProperty = AdamantiumProperty.Register(nameof(MetalColor),
        typeof(Color), typeof(MaterialBrush), new PropertyMetadata(new Color(196, 200, 208, 255), PropertyMetadataOptions.AffectsPaint));

    /// <summary>What the metal has to reflect. PROCEDURAL and not a capture, deliberately: behind a UI there is no
    /// world, and capturing the frame would give a mirror of the window rather than of a room. One colour - the "sky"
    /// of a studio gradient, darkened towards the floor - is cheap, controllable, and enough to read as metal. Metal
    /// only.
    ///
    /// <para>NEUTRAL by default, and that matters more than it looks: a metal shows the room MULTIPLIED by its own
    /// reflectance, so a tinted room stains every metal towards its own hue and gold, copper and steel converge into
    /// one another. Colour the room deliberately, or not at all.</para></summary>
    public static readonly AdamantiumProperty EnvironmentColorProperty = AdamantiumProperty.Register(nameof(EnvironmentColor),
        typeof(Color), typeof(MaterialBrush), new PropertyMetadata(new Color(226, 228, 231, 255), PropertyMetadataOptions.AffectsPaint));

    /// <summary>Where the light comes from, in degrees around the surface. A brush property and not a scene light on
    /// purpose: the view here is fixed and orthographic, so a whole lighting rig would be a pretence - one direction
    /// is the honest amount of state.</summary>
    public static readonly AdamantiumProperty LightAngleProperty = AdamantiumProperty.Register(nameof(LightAngle),
        typeof(double), typeof(MaterialBrush), new PropertyMetadata(315.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>How high the light sits, 0 (grazing) to 1 (straight on). Grazing is what lights the nap; overhead
    /// flattens it, which is a useful thing to be able to see.</summary>
    public static readonly AdamantiumProperty LightElevationProperty = AdamantiumProperty.Register(nameof(LightElevation),
        typeof(double), typeof(MaterialBrush), new PropertyMetadata(0.45, PropertyMetadataOptions.AffectsPaint));

    public MaterialType Material
    {
        get => GetValue<MaterialType>(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

    public Color TintColor
    {
        get => GetValue<Color>(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    public double TintOpacity
    {
        get => GetValue<double>(TintOpacityProperty);
        set => SetValue(TintOpacityProperty, value);
    }

    public double BlurAmount
    {
        get => GetValue<double>(BlurAmountProperty);
        set => SetValue(BlurAmountProperty, value);
    }

    public double NoiseAmount
    {
        get => GetValue<double>(NoiseAmountProperty);
        set => SetValue(NoiseAmountProperty, value);
    }

    public double Refraction
    {
        get => GetValue<double>(RefractionProperty);
        set => SetValue(RefractionProperty, value);
    }

    public Color NapColor
    {
        get => GetValue<Color>(NapColorProperty);
        set => SetValue(NapColorProperty, value);
    }

    public Color SheenColor
    {
        get => GetValue<Color>(SheenColorProperty);
        set => SetValue(SheenColorProperty, value);
    }

    public double GrainScale
    {
        get => GetValue<double>(GrainScaleProperty);
        set => SetValue(GrainScaleProperty, value);
    }

    public double GrainDirection
    {
        get => GetValue<double>(GrainDirectionProperty);
        set => SetValue(GrainDirectionProperty, value);
    }

    public double Roughness
    {
        get => GetValue<double>(RoughnessProperty);
        set => SetValue(RoughnessProperty, value);
    }

    public double Anisotropy
    {
        get => GetValue<double>(AnisotropyProperty);
        set => SetValue(AnisotropyProperty, value);
    }

    public Color MetalColor
    {
        get => GetValue<Color>(MetalColorProperty);
        set => SetValue(MetalColorProperty, value);
    }

    public Color EnvironmentColor
    {
        get => GetValue<Color>(EnvironmentColorProperty);
        set => SetValue(EnvironmentColorProperty, value);
    }

    public double LightAngle
    {
        get => GetValue<double>(LightAngleProperty);
        set => SetValue(LightAngleProperty, value);
    }

    public double LightElevation
    {
        get => GetValue<double>(LightElevationProperty);
        set => SetValue(LightElevationProperty, value);
    }

    public Imaging.ImageSource Source
    {
        get => GetValue<Imaging.ImageSource>(SourceProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(SourceProperty, value);
        }
    }

    public MaterialAnchor Anchor
    {
        get => GetValue<MaterialAnchor>(AnchorProperty);
        set => SetValue(AnchorProperty, value);
    }

    // A picture that is still decoding has nothing to sample yet, so the material draws its built-in source this frame
    // and repaints when the file arrives - the same answer ImageBrush gives.
    private static void OnSourceChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender is MaterialBrush brush)
        {
            TexturedBrushSource.RepaintWhenLoaded(e.NewValue as Imaging.ImageSource, brush.RaiseChanged);
        }
    }

    protected override Brush CreateClone()
    {
        var clone = new MaterialBrush
        {
            Material = Material,
            TintColor = TintColor,
            TintOpacity = TintOpacity,
            BlurAmount = BlurAmount,
            NoiseAmount = NoiseAmount,
            Refraction = Refraction,
            Source = Source,
            Anchor = Anchor,
            NapColor = NapColor,
            SheenColor = SheenColor,
            MetalColor = MetalColor,
            EnvironmentColor = EnvironmentColor,
            GrainScale = GrainScale,
            GrainDirection = GrainDirection,
            Roughness = Roughness,
            Anisotropy = Anisotropy,
            LightAngle = LightAngle,
            LightElevation = LightElevation,
            Opacity = Opacity   // the frozen snapshot paints at the same strength the live brush did
        };
        return clone;
    }
}
