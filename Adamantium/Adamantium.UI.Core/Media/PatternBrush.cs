using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>A PROCEDURAL two-colour pattern fill (checkerboard, stripes, dots, grid) evaluated per fragment in the SDF
/// batch - resolution-independent (crisp at any zoom, no tiled texture). <see cref="CellSize"/> is the cell edge in
/// logical px (it scales with the element). The transparency-preview backdrop is a <see cref="PatternType.Checkerboard"/>.
/// Unlike WPF (which has no procedural brush and tiles a baked DrawingBrush), this is one instanced draw and stays sharp.</summary>
public sealed class PatternBrush : Brush
{
    public PatternBrush() { }

    // PAINT, all of them: a pattern's geometry is fill-relative, so changing the type/colours/cell re-colours the same
    // pixels - never the element's shape or its layout (see Brush.Opacity).
    public static readonly AdamantiumProperty PatternProperty = AdamantiumProperty.Register(nameof(Pattern),
        typeof(PatternType), typeof(PatternBrush), new PropertyMetadata(PatternType.Checkerboard, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty Color1Property = AdamantiumProperty.Register(nameof(Color1),
        typeof(Color), typeof(PatternBrush), new PropertyMetadata(new Color(255, 255, 255, 255), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty Color2Property = AdamantiumProperty.Register(nameof(Color2),
        typeof(Color), typeof(PatternBrush), new PropertyMetadata(new Color(191, 191, 191, 255), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty CellSizeProperty = AdamantiumProperty.Register(nameof(CellSize),
        typeof(double), typeof(PatternBrush), new PropertyMetadata(8.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Which procedural pattern to tile. Default <see cref="PatternType.Checkerboard"/>.</summary>
    public PatternType Pattern
    {
        get => GetValue<PatternType>(PatternProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(PatternProperty, value);
        }
    }

    /// <summary>The primary (background) colour.</summary>
    public Color Color1
    {
        get => GetValue<Color>(Color1Property);
        set
        {
            if (IsFrozen) return;
            SetValue(Color1Property, value);
        }
    }

    /// <summary>The secondary (feature) colour - the alternate square, the dot, the grid line.</summary>
    public Color Color2
    {
        get => GetValue<Color>(Color2Property);
        set
        {
            if (IsFrozen) return;
            SetValue(Color2Property, value);
        }
    }

    /// <summary>The cell edge in logical px (scales with the element). Default 8.</summary>
    public double CellSize
    {
        get => GetValue<double>(CellSizeProperty);
        set
        {
            if (IsFrozen) return;
            SetValue(CellSizeProperty, value);
        }
    }

    protected override Brush CreateClone() =>
        new PatternBrush
        {
            Pattern = Pattern,
            Color1 = Color1,
            Color2 = Color2,
            CellSize = CellSize,
            Opacity = Opacity
        };
}
