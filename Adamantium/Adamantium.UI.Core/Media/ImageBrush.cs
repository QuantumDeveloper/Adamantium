using Adamantium.UI.Core.RoutedEvents;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Core.Media;

/// <summary>Fills a shape with an IMAGE. The first brush of the engine whose colour is SAMPLED rather than computed -
/// everything before it (solid, gradients, patterns, noise, fractal) evaluates a formula per fragment.
/// <para>The image is stretched across the shape. Corners, tiling and the viewport/viewbox machinery of WPF's
/// <c>TileBrush</c> are not here yet - see docs/TILE_BRUSH_PLAN.md; a frame that must not distort its corners is a
/// <see cref="NineSliceBrush"/>, which is what this was built for.</para></summary>
public sealed class ImageBrush : Brush
{
    public ImageBrush() { }

    public ImageBrush(ImageSource source) => Source = source;

    // PAINT: the image fills the shape it is given, so swapping it re-colours the same pixels and never touches layout.
    public static readonly AdamantiumProperty SourceProperty = AdamantiumProperty.Register(nameof(Source),
        typeof(ImageSource), typeof(ImageBrush), new PropertyMetadata(null, PropertyMetadataOptions.AffectsPaint, OnSourceChanged));

    private static void OnSourceChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender is ImageBrush brush)
        {
            TexturedBrushSource.RepaintWhenLoaded(e.NewValue as ImageSource, brush.RaiseChanged);
        }
    }

    /// <summary>Whether the picture is laid down ONCE (and fitted by <see cref="Stretch"/>) or REPEATED across the
    /// shape. Tiling is what lets a small texture dress a large surface without being stretched into mush; the Flip
    /// modes mirror every other copy, which makes even a picture that was never drawn to tile meet itself seamlessly.</summary>
    public static readonly AdamantiumProperty TileModeProperty = AdamantiumProperty.Register(nameof(TileMode),
        typeof(TileMode), typeof(ImageBrush), new PropertyMetadata(TileMode.None, PropertyMetadataOptions.AffectsPaint));

    /// <summary>How ONE copy is fitted to the shape (<see cref="TileMode.None"/>), or how big one TILE is when tiling.
    /// <see cref="Media.Stretch.Fill"/> takes the whole shape and ignores the aspect ratio; <c>Uniform</c> fits the
    /// picture inside and leaves the rest untouched; <c>UniformToFill</c> covers the shape and crops the overflow;
    /// <c>None</c> draws it at its own pixel size.</summary>
    public static readonly AdamantiumProperty StretchProperty = AdamantiumProperty.Register(nameof(Stretch),
        typeof(Stretch), typeof(ImageBrush), new PropertyMetadata(Stretch.Fill, PropertyMetadataOptions.AffectsPaint));

    /// <summary>How big one TILE is, in logical px. Zero (the default) uses the source's own pixel size, which is the
    /// 1:1 case a texture is usually drawn for. Ignored unless <see cref="TileMode"/> repeats.</summary>
    public static readonly AdamantiumProperty TileSizeProperty = AdamantiumProperty.Register(nameof(TileSize),
        typeof(Size), typeof(ImageBrush), new PropertyMetadata(default(Size), PropertyMetadataOptions.AffectsPaint));

    /// <summary>Multiplied into every sampled pixel. White (the default) draws the image as it is; a colour tints it,
    /// which is how one greyscale skin serves several themes.</summary>
    public static readonly AdamantiumProperty TintProperty = AdamantiumProperty.Register(nameof(Tint),
        typeof(Color), typeof(ImageBrush), new PropertyMetadata(Colors.White, PropertyMetadataOptions.AffectsPaint));

    public ImageSource Source
    {
        get => GetValue<ImageSource>(SourceProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(SourceProperty, value);
        }
    }

    public TileMode TileMode
    {
        get => GetValue<TileMode>(TileModeProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(TileModeProperty, value);
        }
    }

    public Stretch Stretch
    {
        get => GetValue<Stretch>(StretchProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(StretchProperty, value);
        }
    }

    public Size TileSize
    {
        get => GetValue<Size>(TileSizeProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(TileSizeProperty, value);
        }
    }

    public Color Tint
    {
        get => GetValue<Color>(TintProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(TintProperty, value);
        }
    }

    protected override Brush CreateClone() =>
        new ImageBrush
        {
            Source = Source,
            TileMode = TileMode,
            Stretch = Stretch,
            TileSize = TileSize,
            Tint = Tint,
            Opacity = Opacity
        };
}
