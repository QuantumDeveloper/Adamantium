using Adamantium.Mathematics;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Media;

public class Transform : AnimatableUIComponent
{
    public static readonly AdamantiumProperty ScaleXProperty = AdamantiumProperty.Register(nameof(ScaleX),
        typeof (Double), typeof (Transform), new PropertyMetadata(1.0, TransformPropertyChangedCallback));
        
    public static readonly AdamantiumProperty ScaleYProperty = AdamantiumProperty.Register(nameof(ScaleY),
        typeof (Double), typeof (Transform), new PropertyMetadata(1.0, TransformPropertyChangedCallback));
        
    public static readonly AdamantiumProperty RotationAngleProperty = AdamantiumProperty.Register(nameof(RotationAngle),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));

    // 3D rotations (degrees) around the X / Y axes through the rotation centre - the flip/tilt-tile effects. They fold
    // into the same single matrix (the render's transform table applies full 4x4s, so a 3D-rotated element STAYS in the
    // instanced batches). Perspective adds the depth foreshortening (see PerspectiveProperty).
    public static readonly AdamantiumProperty RotationXProperty = AdamantiumProperty.Register(nameof(RotationX),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));

    public static readonly AdamantiumProperty RotationYProperty = AdamantiumProperty.Register(nameof(RotationY),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));

    /// <summary>Camera distance (logical px) for 3D depth foreshortening; 0 (default) = no perspective (affine). Applied
    /// around the rotation centre, so a tile flips "in place" like WPF's classic 3D tile demos.</summary>
    public static readonly AdamantiumProperty PerspectiveProperty = AdamantiumProperty.Register(nameof(Perspective),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));
        
    public static readonly AdamantiumProperty RotationCenterXProperty = AdamantiumProperty.Register(nameof(RotationCenterX),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));
        
    public static readonly AdamantiumProperty RotationCenterYProperty = AdamantiumProperty.Register(nameof(RotationCenterY),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));
        
    public static readonly AdamantiumProperty TranslateXProperty = AdamantiumProperty.Register(nameof(TranslateX),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));
        
    public static readonly AdamantiumProperty TranslateYProperty = AdamantiumProperty.Register(nameof(TranslateY),
        typeof (Double), typeof (Transform), new PropertyMetadata(default(Double), TransformPropertyChangedCallback));

    private static void TransformPropertyChangedCallback(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is Transform transform)
        {
            transform.UpdateTransform();
        }
    }

    public Double ScaleX
    {
        get => GetValue<Double>(ScaleXProperty);
        set => SetValue(ScaleXProperty, value);
    }
        
    public Double ScaleY
    {
        get => GetValue<Double>(ScaleYProperty);
        set => SetValue(ScaleYProperty, value);
    }
        
    public Double RotationAngle
    {
        get => GetValue<Double>(RotationAngleProperty);
        set => SetValue(RotationAngleProperty, value);
    }

    public Double RotationX
    {
        get => GetValue<Double>(RotationXProperty);
        set => SetValue(RotationXProperty, value);
    }

    public Double RotationY
    {
        get => GetValue<Double>(RotationYProperty);
        set => SetValue(RotationYProperty, value);
    }

    public Double Perspective
    {
        get => GetValue<Double>(PerspectiveProperty);
        set => SetValue(PerspectiveProperty, value);
    }
        
    public Double TranslateX
    {
        get => GetValue<Double>(TranslateXProperty);
        set => SetValue(TranslateXProperty, value);
    }

    public Double TranslateY
    {
        get => GetValue<Double>(TranslateYProperty);
        set => SetValue(TranslateYProperty, value);
    }
        
    public Double RotationCenterX
    {
        get => GetValue<Double>(RotationCenterXProperty);
        set => SetValue(RotationCenterXProperty, value);
    }
        
    public Double RotationCenterY
    {
        get => GetValue<Double>(RotationCenterYProperty);
        set => SetValue(RotationCenterYProperty, value);
    }
        
    public Matrix4x4 Matrix { get; private set; } = Matrix4x4.Identity;

    /// <summary>The element this transform is assigned to as RenderTransform (set by the owner). Lets a transform tick
    /// mark ONLY its owner when the owner is a render MOTION NODE (its slot matrix rewrites - the O(1) tilt/flip path)
    /// instead of the global transform flag that re-bakes the whole scene.</summary>
    internal IUIComponent Owner { get; set; }

    /// <summary>True when this transform is an element's LayoutTransform (not its RenderTransform): a value change then
    /// re-runs the owner's LAYOUT, because it reshapes the footprint - not just the render.</summary>
    internal bool IsLayoutTransform { get; set; }

    private void UpdateTransform()
    {
        Matrix = CalculateFinalTransform();

        // A LayoutTransform reshapes the owner's FOOTPRINT, so a value change (AUML setting ScaleX after the property, an
        // animated zoom, ...) must re-run LAYOUT: measure re-cascades into arrange and the render. A RenderTransform only
        // moves an already-laid-out element, so it falls through to the render mark below.
        if (IsLayoutTransform)
        {
            if (Owner is IMeasurableComponent measurable) measurable.InvalidateMeasure();
            return;
        }

        // The RENDER thread IS drawing this transform's animated matrix (see Compositor) - so re-baking it here is pure
        // double work (the fps cost). This loop write is only the MIRROR that keeps hit-testing and bindings in step.
        // "Recently" and not just "owns": if the compositor holds the entry but is NOT applying it - its owner isn't
        // recorded, e.g. a spinner the theme swap just re-templated - the picture would freeze, so fall through and let the
        // loop thread re-bake it, exactly as for an uncomposited transform.
        if (Media.Animation.Compositor.EntryFor(this) is { AppliedRecently: true }) return;

        // A transform change MOVES the owning element; the recorded geometry is unchanged. When the owner is a MOTION
        // NODE its instances reference its transform-table slot, so only that node is marked (one matrix rewrite +
        // replay); otherwise the conservative global Transform mark re-bakes world transforms as before. Neither is a
        // STRUCTURAL mark, so a held theme swap still reaches layout quiescence and completes. Transform's inner properties
        // carry no AffectsRender, so without this they'd self-mark nothing and the animation heartbeat had to fall back to
        // MarkStructural every tick (a full-window walk = the tab-drag lag).
        // Owner is null only when this transform isn't assigned as anyone's RenderTransform (so it moves nothing yet) -
        // MarkTransform then records an UNNAMEABLE move and the recorder re-captures the whole layout snapshot for that
        // frame rather than silently keeping a stale entry.
        if (Owner is { IsRenderMotionNode: true } node) RenderDirty.MarkNodeTransform(node);
        else RenderDirty.MarkTransform(Owner);
    }

    /// <summary>This transform's values as plain data - what the compositor captures so it can compose the matrix on the
    /// render thread without touching the property system. Read on the loop thread, at handoff.</summary>
    public TransformValues Values => new()
    {
        ScaleX = ScaleX,
        ScaleY = ScaleY,
        RotationAngle = RotationAngle,
        RotationX = RotationX,
        RotationY = RotationY,
        Perspective = Perspective,
        RotationCenterX = RotationCenterX,
        RotationCenterY = RotationCenterY,
        TranslateX = TranslateX,
        TranslateY = TranslateY
    };

    // The matrix arithmetic itself lives in TransformValues, so the compositor composes the SAME matrix from the SAME code -
    // two implementations of this would be two chances to disagree about where an element is.
    private Matrix4x4 CalculateFinalTransform() => Values.ToMatrix();
}