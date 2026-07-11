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

    private void UpdateTransform()
    {
        Matrix = CalculateFinalTransform();
        // A transform change MOVES the owning element; the recorded geometry is unchanged. When the owner is a MOTION
        // NODE its instances reference its transform-table slot, so only that node is marked (one matrix rewrite +
        // replay); otherwise the conservative global Transform mark re-bakes world transforms as before. Transform's
        // inner properties carry no AffectsRender, so without this they'd self-mark nothing and the animation heartbeat
        // had to fall back to MarkStructural every tick (a full-window walk = the tab-drag lag).
        if (Owner is { IsRenderMotionNode: true } node) RenderDirty.MarkNodeTransform(node);
        else RenderDirty.MarkTransform();
    }

    private Matrix4x4 CalculateFinalTransform()
    {
        // Z scale MUST be 1: the two-arg ctor left it 0, which zeroed the matrix's Z row (M33 = 0) - harmless for the
        // flat z=0 quads' positions, but the perspective sandwich below computes its w-term THROUGH M33
        // (M34 = M33 * -1/d), so the whole 3D depth silently collapsed to an affine tilt.
        var scaling = new Vector3(ScaleX, ScaleY, 1);
        var translation = new Vector3((float)TranslateX, (float)TranslateY, 0);
        var rotationCenter = new Vector3((float)RotationCenterX, (float)RotationCenterY, 0);
        var scalingCenter = Vector3.Zero;
        var scalingRotation = Quaternion.Identity;

        // Z spin (the classic 2D angle) composed with the 3D X/Y rotations (pitch/yaw) - one quaternion, rotated around
        // the same centre, so the 2D-only case is bit-identical to before (RotationX/Y = 0 -> pure UnitZ rotation).
        var rotation = Quaternion.RotationYawPitchRoll(
            MathHelper.DegreesToRadians(RotationY),
            MathHelper.DegreesToRadians(RotationX),
            MathHelper.DegreesToRadians(RotationAngle));

        Matrix4x4.Transformation(
            ref scalingCenter,
            ref scalingRotation,
            ref scaling,
            ref rotationCenter,
            ref rotation,
            ref translation,
            out var matrix);

        // Perspective foreshortening around the rotation centre: w' = 1 - z/d, so points rotated toward the viewer
        // (negative z) grow and away shrink - the WPF-3D-tile look. Off (affine) when Perspective is 0. Composed as
        // T(-c) * M34(-1/d) * T(c) AFTER the affine transform: depth produced by the rotation feeds the divide.
        var d = Perspective;
        if (d > 0)
        {
            var persp = Matrix4x4.Identity;
            persp.M34 = -1.0 / d;
            var toCenter = Matrix4x4.Translation(-(float)RotationCenterX, -(float)RotationCenterY, 0);
            var fromCenter = Matrix4x4.Translation((float)RotationCenterX, (float)RotationCenterY, 0);
            matrix = matrix * toCenter * persp * fromCenter;
        }

        return matrix;
    }
}