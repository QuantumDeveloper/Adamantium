using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>A brush that paints with CONTENT laid out as tiles - a picture (<see cref="ImageBrush"/>), a drawing, a
/// live visual. WPF's <c>TileBrush</c>, and the four independent mechanisms are worth keeping apart:
/// <list type="bullet">
/// <item><see cref="Viewbox"/> - WHICH part of the content one tile shows.</item>
/// <item><see cref="Viewport"/> - WHERE one tile lands in the shape, and therefore how big it is.</item>
/// <item><see cref="Stretch"/> + <see cref="AlignmentX"/>/<see cref="AlignmentY"/> - how the content fits its tile.</item>
/// <item><see cref="TileMode"/> - whether that tile repeats, and mirrored or not.</item>
/// </list>
/// They compose in that order, and each is meaningless without the others being pinned down - which is why they live
/// here together rather than being invented per brush.</summary>
public abstract class TileBrush : Brush
{
    /// <summary>Whether the picture is laid down ONCE (fitted by <see cref="Stretch"/>) or REPEATED across the shape.
    /// Tiling is what lets a small texture dress a large surface without being stretched into mush; the Flip modes
    /// mirror every other copy, which makes even a picture that was never drawn to tile meet itself seamlessly.</summary>
    public static readonly AdamantiumProperty TileModeProperty = AdamantiumProperty.Register(nameof(TileMode),
        typeof(TileMode), typeof(TileBrush), new PropertyMetadata(TileMode.None, PropertyMetadataOptions.AffectsPaint));

    /// <summary>How the content is fitted to its tile. <see cref="Media.Stretch.Fill"/> takes the whole tile and ignores
    /// the aspect ratio; <c>Uniform</c> fits the content inside and leaves the rest of the tile empty;
    /// <c>UniformToFill</c> covers the tile and crops the overflow; <c>None</c> uses the content's own size.</summary>
    public static readonly AdamantiumProperty StretchProperty = AdamantiumProperty.Register(nameof(Stretch),
        typeof(Stretch), typeof(TileBrush), new PropertyMetadata(Stretch.Fill, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Where the content sits horizontally in its tile when <see cref="Stretch"/> leaves room.</summary>
    public static readonly AdamantiumProperty AlignmentXProperty = AdamantiumProperty.Register(nameof(AlignmentX),
        typeof(AlignmentX), typeof(TileBrush), new PropertyMetadata(AlignmentX.Center, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Where the content sits vertically in its tile when <see cref="Stretch"/> leaves room.</summary>
    public static readonly AdamantiumProperty AlignmentYProperty = AdamantiumProperty.Register(nameof(AlignmentY),
        typeof(AlignmentY), typeof(TileBrush), new PropertyMetadata(AlignmentY.Center, PropertyMetadataOptions.AffectsPaint));

    /// <summary>One tile's rectangle in the FILLED shape. The default (0,0,1,1) relative is one tile covering the whole
    /// shape, so a brush that says nothing behaves as a single stretched copy.</summary>
    public static readonly AdamantiumProperty ViewportProperty = AdamantiumProperty.Register(nameof(Viewport),
        typeof(Rect), typeof(TileBrush), new PropertyMetadata(new Rect(0, 0, 1, 1), PropertyMetadataOptions.AffectsPaint));

    /// <summary>Whether <see cref="Viewport"/> is a fraction of the shape or logical pixels.</summary>
    public static readonly AdamantiumProperty ViewportUnitsProperty = AdamantiumProperty.Register(nameof(ViewportUnits),
        typeof(BrushMappingMode), typeof(TileBrush),
        new PropertyMetadata(BrushMappingMode.RelativeToBoundingBox, PropertyMetadataOptions.AffectsPaint));

    /// <summary>The part of the CONTENT one tile shows. The default (0,0,1,1) relative is the whole thing; a smaller
    /// rectangle is how one sprite is cut out of a sheet without slicing the file.</summary>
    public static readonly AdamantiumProperty ViewboxProperty = AdamantiumProperty.Register(nameof(Viewbox),
        typeof(Rect), typeof(TileBrush), new PropertyMetadata(new Rect(0, 0, 1, 1), PropertyMetadataOptions.AffectsPaint));

    /// <summary>Whether <see cref="Viewbox"/> is a fraction of the content or its own units (pixels for a picture).</summary>
    public static readonly AdamantiumProperty ViewboxUnitsProperty = AdamantiumProperty.Register(nameof(ViewboxUnits),
        typeof(BrushMappingMode), typeof(TileBrush),
        new PropertyMetadata(BrushMappingMode.RelativeToBoundingBox, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Multiplied into every sampled pixel. White (the default) draws the content as it is; a colour tints it,
    /// which is how one greyscale skin serves several themes.</summary>
    public static readonly AdamantiumProperty TintProperty = AdamantiumProperty.Register(nameof(Tint),
        typeof(Color), typeof(TileBrush), new PropertyMetadata(Colors.White, PropertyMetadataOptions.AffectsPaint));

    public TileMode TileMode
    {
        get => GetValue<TileMode>(TileModeProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(TileModeProperty, value);
        }
    }

    public Stretch Stretch
    {
        get => GetValue<Stretch>(StretchProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(StretchProperty, value);
        }
    }

    public AlignmentX AlignmentX
    {
        get => GetValue<AlignmentX>(AlignmentXProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(AlignmentXProperty, value);
        }
    }

    public AlignmentY AlignmentY
    {
        get => GetValue<AlignmentY>(AlignmentYProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(AlignmentYProperty, value);
        }
    }

    public Rect Viewport
    {
        get => GetValue<Rect>(ViewportProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(ViewportProperty, value);
        }
    }

    public BrushMappingMode ViewportUnits
    {
        get => GetValue<BrushMappingMode>(ViewportUnitsProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(ViewportUnitsProperty, value);
        }
    }

    public Rect Viewbox
    {
        get => GetValue<Rect>(ViewboxProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(ViewboxProperty, value);
        }
    }

    public BrushMappingMode ViewboxUnits
    {
        get => GetValue<BrushMappingMode>(ViewboxUnitsProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(ViewboxUnitsProperty, value);
        }
    }

    public Color Tint
    {
        get => GetValue<Color>(TintProperty);
        set
        {
            if (IsFrozen)
            {
                return;
            }

            SetValue(TintProperty, value);
        }
    }

    /// <summary>The content's own size, in its own units - a picture's pixels, a drawing's bounds. What
    /// <see cref="Stretch"/> and an absolute <see cref="Viewbox"/> are measured against; zero when there is no content
    /// yet (still decoding), which every consumer must read as "nothing to draw".</summary>
    public abstract Size ContentSize { get; }

    /// <summary>Copy this brush's tiling onto <paramref name="clone"/>. Subclasses call it from their own clone so the
    /// eight properties above are never half-copied - the frozen snapshot is what the render path reads, and a missing
    /// one there is a brush that paints differently on screen than it does in markup.</summary>
    protected void CopyTilingTo(TileBrush clone)
    {
        clone.TileMode = TileMode;
        clone.Stretch = Stretch;
        clone.AlignmentX = AlignmentX;
        clone.AlignmentY = AlignmentY;
        clone.Viewport = Viewport;
        clone.ViewportUnits = ViewportUnits;
        clone.Viewbox = Viewbox;
        clone.ViewboxUnits = ViewboxUnits;
        clone.Tint = Tint;
        clone.Opacity = Opacity;
    }
}
