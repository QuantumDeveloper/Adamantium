using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>How <see cref="CanvasGridBrush"/> marks the plane.</summary>
public enum CanvasGridMarks
{
    None,
    Dots,
    Lines
}

/// <summary>A PROCEDURAL grid: the fragment shader decides from the world coordinate under each pixel whether it is on a
/// mark. Nothing is generated for it - one rectangle is drawn, and an unbounded grid never exists as geometry.
/// <para>It is a fill, and so a brush - not an effect laid over what a control drew. The camera rides in it because the
/// grid IS the camera made visible: <see cref="Offset"/> is where the world's origin sits on screen and
/// <see cref="Scale"/> is screen pixels per world unit, which is all the shader needs to place every mark.</para>
/// <para>The step shown is <see cref="Spacing"/> taken up or down by whole powers of <see cref="Coarsening"/> until the
/// marks are at least <see cref="MinPitch"/> apart on screen - and the two neighbouring levels are CROSS-FADED, so
/// pulling the camera back dissolves one into the other instead of the whole grid jumping from tens to hundreds.</para>
/// </summary>
public sealed class CanvasGridBrush : Brush
{
    public static readonly AdamantiumProperty MarksProperty = AdamantiumProperty.Register(nameof(Marks),
        typeof(CanvasGridMarks), typeof(CanvasGridBrush),
        new PropertyMetadata(CanvasGridMarks.Dots, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Where the world's origin sits on screen, in logical pixels.</summary>
    public static readonly AdamantiumProperty OffsetProperty = AdamantiumProperty.Register(nameof(Offset),
        typeof(Vector2), typeof(CanvasGridBrush),
        new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsPaint));

    /// <summary>Screen pixels per world unit.</summary>
    public static readonly AdamantiumProperty ScaleProperty = AdamantiumProperty.Register(nameof(Scale),
        typeof(double), typeof(CanvasGridBrush), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>The step to draw, in WORLD units - ALREADY coarsened for the camera. Worked out where the camera is,
    /// not in the shader: a ruler and a snap have to agree with what is drawn, so the step exists on that side anyway,
    /// and computing it twice is how the two come to disagree.</summary>
    public static readonly AdamantiumProperty SpacingProperty = AdamantiumProperty.Register(nameof(Spacing),
        typeof(double), typeof(CanvasGridBrush), new PropertyMetadata(20.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>What the step is multiplied by for the ACCENT level - the sparser marks drawn over the step, and the
    /// level the step is about to become when the camera pulls back far enough.</summary>
    public static readonly AdamantiumProperty CoarseningProperty = AdamantiumProperty.Register(nameof(Coarsening),
        typeof(double), typeof(CanvasGridBrush), new PropertyMetadata(10.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>How close two marks may come on SCREEN before the step must be taken up a level. What the main level
    /// fades against, so the handover to the accent has nothing to jump over.</summary>
    public static readonly AdamantiumProperty MinPitchProperty = AdamantiumProperty.Register(nameof(MinPitch),
        typeof(double), typeof(CanvasGridBrush), new PropertyMetadata(12.0, PropertyMetadataOptions.AffectsPaint));

    /// <summary>The ground the marks are laid on. The grid carries it rather than the canvas painting a rectangle of
    /// its own underneath, because the grid is flushed FIRST of everything in its clip group - that is what makes it a
    /// ground for whatever is drawn on the canvas - and a separate background would then land on top of it.</summary>
    public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background),
        typeof(Color), typeof(CanvasGridBrush),
        new PropertyMetadata(new Color(0, 0, 0, 0), PropertyMetadataOptions.AffectsPaint));

    public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Color),
        typeof(Color), typeof(CanvasGridBrush),
        new PropertyMetadata(new Color(255, 255, 255, 56), PropertyMetadataOptions.AffectsPaint));

    /// <summary>The world's own axes, where they cross the viewport. Alpha 0 leaves them undrawn.</summary>
    public static readonly AdamantiumProperty AxisColorProperty = AdamantiumProperty.Register(nameof(AxisColor),
        typeof(Color), typeof(CanvasGridBrush),
        new PropertyMetadata(new Color(255, 255, 255, 122), PropertyMetadataOptions.AffectsPaint));

    /// <summary>The width of a line, or the side of a dot, in SCREEN pixels - the grid is a ruler laid over the drawing,
    /// not part of it, so it keeps its size however far the camera is zoomed.</summary>
    public static readonly AdamantiumProperty MarkSizeProperty = AdamantiumProperty.Register(nameof(MarkSize),
        typeof(double), typeof(CanvasGridBrush), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsPaint));

    public CanvasGridMarks Marks
    {
        get => GetValue<CanvasGridMarks>(MarksProperty);
        set => SetValue(MarksProperty, value);
    }

    public Vector2 Offset
    {
        get => GetValue<Vector2>(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    public double Scale
    {
        get => GetValue<double>(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public double Spacing
    {
        get => GetValue<double>(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double Coarsening
    {
        get => GetValue<double>(CoarseningProperty);
        set => SetValue(CoarseningProperty, value);
    }

    public double MinPitch
    {
        get => GetValue<double>(MinPitchProperty);
        set => SetValue(MinPitchProperty, value);
    }

    public Color Background
    {
        get => GetValue<Color>(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color Color
    {
        get => GetValue<Color>(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public Color AxisColor
    {
        get => GetValue<Color>(AxisColorProperty);
        set => SetValue(AxisColorProperty, value);
    }

    public double MarkSize
    {
        get => GetValue<double>(MarkSizeProperty);
        set => SetValue(MarkSizeProperty, value);
    }

    protected override Brush CreateClone() => new CanvasGridBrush
    {
        Marks = Marks,
        Offset = Offset,
        Scale = Scale,
        Spacing = Spacing,
        Coarsening = Coarsening,
        MinPitch = MinPitch,
        Background = Background,
        Color = Color,
        AxisColor = AxisColor,
        MarkSize = MarkSize,
        Opacity = Opacity
    };
}
