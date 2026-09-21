using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>A run of INK: the fill of one stroke, drawn as a capsule per segment by a pass of its own.
/// <para>It carries the POINTS because it is the parameter block that reaches the shader, the way the backdrop
/// materials and the fractal fill carry theirs. They are in the drawing element's OWN coordinates and the width is in
/// the same units, so whoever draws the stroke decides what those units mean - a canvas hands over screen positions and
/// a screen width, which is what keeps the numbers reaching the GPU small however far the stroke is from the world's
/// origin.</para>
/// <para>The array is BORROWED, not copied: a stroke being drawn hands the same one over every frame and simply says a
/// higher count. That is also why <see cref="Count"/> has to be a real property and not a field - the paint is re-baked
/// because a PROPERTY changed, and a stroke whose points were swapped behind the property system's back stayed on
/// screen as the single dot it was first recorded as.</para></summary>
public sealed class InkBrush : Brush
{
    /// <summary>The points, in the drawing element's own coordinates. Only the first <see cref="Count"/> are used.
    /// </summary>
    public static readonly AdamantiumProperty PointsProperty = AdamantiumProperty.Register(nameof(Points),
        typeof(Vector2F[]), typeof(InkBrush), new PropertyMetadata(null, PropertyMetadataOptions.AffectsPaint));

    /// <summary>How many of <see cref="Points"/> are the stroke. A stroke being drawn grows this and nothing else.</summary>
    public static readonly AdamantiumProperty CountProperty = AdamantiumProperty.Register(nameof(Count),
        typeof(int), typeof(InkBrush), new PropertyMetadata(0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>How wide the ink is, in the same units as <see cref="Points"/>.</summary>
    public static readonly AdamantiumProperty ThicknessProperty = AdamantiumProperty.Register(nameof(Thickness),
        typeof(double), typeof(InkBrush), new PropertyMetadata(2.0, PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Color),
        typeof(Color), typeof(InkBrush), new PropertyMetadata(Colors.White, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Bumped by whoever fills <see cref="Points"/> to say the CONTENTS changed.
    /// <para>Needed because the array is borrowed: its contents are rewritten in place every time the camera moves, and
    /// the reference does not change - so the property system has nothing to notice and the ink stays baked as it was.
    /// One number said out loud is better than allocating a new array per stroke per frame to make the reference
    /// differ, which is the cost this whole pass exists to remove.</para></summary>
    public static readonly AdamantiumProperty RevisionProperty = AdamantiumProperty.Register(nameof(Revision),
        typeof(int), typeof(InkBrush), new PropertyMetadata(0, PropertyMetadataOptions.AffectsPaint));

    public Vector2F[] Points
    {
        get => GetValue<Vector2F[]>(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public int Count
    {
        get => GetValue<int>(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public double Thickness
    {
        get => GetValue<double>(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    public Color Color
    {
        get => GetValue<Color>(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public int Revision
    {
        get => GetValue<int>(RevisionProperty);
        set => SetValue(RevisionProperty, value);
    }

    protected override Brush CreateClone() => new InkBrush
    {
        Points = Points,
        Count = Count,
        Revision = Revision,
        Thickness = Thickness,
        Color = Color,
        Opacity = Opacity
    };
}
