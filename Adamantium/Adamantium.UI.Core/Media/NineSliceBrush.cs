using Adamantium.UI.Core.RoutedEvents;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Core.Media;

/// <summary>A picture used as a FRAME that survives being resized: the source is cut by <see cref="Slice"/> into nine
/// pieces, the four corners are drawn at their own size and never distort, the four edges stretch or repeat along their
/// own axis, and the centre fills what is left. This is CSS <c>border-image</c> / the 9-patch of every game UI, and it
/// is how a button or panel skin is drawn at any size from one small image.
/// <para>WPF has no such brush - there people build a 3x3 <c>Grid</c> of <c>ImageBrush</c>es by hand. Here it is one
/// brush that bakes into nine instances of one batch, so the whole frame is still a single draw.</para></summary>
public sealed class NineSliceBrush : Brush
{
    public NineSliceBrush() { }

    public NineSliceBrush(ImageSource source) => Source = source;

    // PAINT, all of them: a nine-slice fills the shape it is given, so changing any of this re-colours the same pixels.
    public static readonly AdamantiumProperty SourceProperty = AdamantiumProperty.Register(nameof(Source),
        typeof(ImageSource), typeof(NineSliceBrush), new PropertyMetadata(null, PropertyMetadataOptions.AffectsPaint, OnSourceChanged));

    private static void OnSourceChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender is NineSliceBrush brush)
        {
            TexturedBrushSource.RepaintWhenLoaded(e.NewValue as ImageSource, brush.RaiseChanged);
        }
    }

    /// <summary>Where the source is cut, as FRACTIONS of its size (0..1), not pixels: left, top, right, bottom. Fractions
    /// so one brush serves sources of several resolutions - the same skin at 1x and 2x cuts in the same place, and a
    /// pixel count would be wrong for one of them.</summary>
    public static readonly AdamantiumProperty SliceProperty = AdamantiumProperty.Register(nameof(Slice),
        typeof(Thickness), typeof(NineSliceBrush),
        new PropertyMetadata(new Thickness(0.25), PropertyMetadataOptions.AffectsPaint));

    /// <summary>How wide the corners are DRAWN, in logical px. Unset (0) draws them at the size the slice fractions give
    /// against the source's own pixel size - the 1:1 case. Setting it scales the frame without touching the source, which
    /// is what a skin needs when the same picture dresses a 24px button and a 96px panel.</summary>
    public static readonly AdamantiumProperty BorderProperty = AdamantiumProperty.Register(nameof(Border),
        typeof(Thickness), typeof(NineSliceBrush),
        new PropertyMetadata(new Thickness(0), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty EdgeModeProperty = AdamantiumProperty.Register(nameof(EdgeMode),
        typeof(NineSliceEdgeMode), typeof(NineSliceBrush),
        new PropertyMetadata(NineSliceEdgeMode.Stretch, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Whether the MIDDLE piece tiles too, or is stretched like a picture. Off by default, and deliberately
    /// separate from <see cref="EdgeMode"/>: an edge has a rhythm to preserve, while a middle tiled at the same pitch
    /// turns into a grid - the smaller the <see cref="Slice"/>, the denser. CSS <c>border-image</c> and the classic
    /// 9-patch both repeat the EDGES only and scale the middle; this is that, with the other option kept for a game
    /// panel whose inside is a repeating texture.</summary>
    public static readonly AdamantiumProperty TileCenterProperty = AdamantiumProperty.Register(nameof(TileCenter),
        typeof(bool), typeof(NineSliceBrush), new PropertyMetadata(false, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Whether the middle piece is drawn at all. False leaves the inside untouched - the frame is then a border
    /// over whatever the element already paints there, which is the usual want for a skin that dresses live content.</summary>
    public static readonly AdamantiumProperty DrawCenterProperty = AdamantiumProperty.Register(nameof(DrawCenter),
        typeof(bool), typeof(NineSliceBrush), new PropertyMetadata(true, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Multiplied into every sampled pixel; white draws the source as it is.</summary>
    public static readonly AdamantiumProperty TintProperty = AdamantiumProperty.Register(nameof(Tint),
        typeof(Color), typeof(NineSliceBrush), new PropertyMetadata(Colors.White, PropertyMetadataOptions.AffectsPaint));

    public ImageSource Source
    {
        get => GetValue<ImageSource>(SourceProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(SourceProperty, value);
        }
    }

    public Thickness Slice
    {
        get => GetValue<Thickness>(SliceProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(SliceProperty, value);
        }
    }

    public Thickness Border
    {
        get => GetValue<Thickness>(BorderProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(BorderProperty, value);
        }
    }

    public NineSliceEdgeMode EdgeMode
    {
        get => GetValue<NineSliceEdgeMode>(EdgeModeProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(EdgeModeProperty, value);
        }
    }

    public bool TileCenter
    {
        get => GetValue<bool>(TileCenterProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(TileCenterProperty, value);
        }
    }

    public bool DrawCenter
    {
        get => GetValue<bool>(DrawCenterProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(DrawCenterProperty, value);
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
        new NineSliceBrush
        {
            Source = Source,
            Slice = Slice,
            Border = Border,
            EdgeMode = EdgeMode,
            DrawCenter = DrawCenter,
            TileCenter = TileCenter,
            Tint = Tint,
            Opacity = Opacity
        };
}
