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
            Opacity = Opacity   // the frozen snapshot paints at the same strength the live brush did
        };
        return clone;
    }
}
