using System;
using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>The ten numbers a <see cref="Transform"/> is made of, as plain data - and the matrix they compose to.</summary>
/// <remarks>
/// A <see cref="Transform"/> is an AdamantiumComponent: reading its properties means reading the property system, which the
/// render thread must not do (see <see cref="Animation.AnimationChannels"/>). So the values are lifted OUT of it here. The
/// compositor captures this struct once on the loop thread, overrides only the members its curve animates, and composes the
/// matrix itself - the same arithmetic, on the render thread, touching nothing shared.
///
/// <see cref="Transform"/> composes its own matrix through this too, so the two can never drift apart.
/// </remarks>
public struct TransformValues
{
    public double ScaleX;
    public double ScaleY;
    public double RotationAngle;
    public double RotationX;
    public double RotationY;
    public double Perspective;
    public double RotationCenterX;
    public double RotationCenterY;
    public double TranslateX;
    public double TranslateY;
    public double SkewX;
    public double SkewY;

    public static TransformValues Identity => new() { ScaleX = 1.0, ScaleY = 1.0 };

    /// <summary>Override the member <paramref name="property"/> names. This is how the compositor applies one animated track
    /// to its captured base values, each frame, without a property-system write. Unknown properties are ignored - a curve
    /// only reaches here once <see cref="Animation.AnimationChannels"/> has said every track is a Transform property.</summary>
    public void Set(AdamantiumProperty property, double value)
    {
        if (property == Transform.ScaleXProperty) ScaleX = value;
        else if (property == Transform.ScaleYProperty) ScaleY = value;
        else if (property == Transform.RotationAngleProperty) RotationAngle = value;
        else if (property == Transform.RotationXProperty) RotationX = value;
        else if (property == Transform.RotationYProperty) RotationY = value;
        else if (property == Transform.PerspectiveProperty) Perspective = value;
        else if (property == Transform.RotationCenterXProperty) RotationCenterX = value;
        else if (property == Transform.RotationCenterYProperty) RotationCenterY = value;
        else if (property == Transform.TranslateXProperty) TranslateX = value;
        else if (property == Transform.TranslateYProperty) TranslateY = value;
        else if (property == Transform.SkewXProperty) SkewX = value;
        else if (property == Transform.SkewYProperty) SkewY = value;
    }

    /// <summary>Compose these values into the transform matrix. Pure: no property reads, no shared state.</summary>
    public readonly Matrix4x4 ToMatrix()
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

        var toCenter = Matrix4x4.Translation(-(float)RotationCenterX, -(float)RotationCenterY, 0);
        var fromCenter = Matrix4x4.Translation((float)RotationCenterX, (float)RotationCenterY, 0);

        // SHEAR, around the same centre and applied FIRST - in the element's own space, before scale/rotate/translate.
        // Row-vector convention (v * M), so x' = x + y*tan(SkewX) is M21 and y' = y + x*tan(SkewY) is M12. WPF splits this
        // into a separate SkewTransform whose place is whatever a TransformGroup says; here the order is fixed, and
        // innermost is the one that reads as "italicise this element" rather than "shear the world it sits in".
        if (SkewX != 0 || SkewY != 0)
        {
            var skew = Matrix4x4.Identity;
            skew.M21 = Math.Tan(MathHelper.DegreesToRadians(SkewX));
            skew.M12 = Math.Tan(MathHelper.DegreesToRadians(SkewY));
            matrix = toCenter * skew * fromCenter * matrix;
        }

        // Perspective foreshortening around the rotation centre: w' = 1 - z/d, so points rotated toward the viewer
        // (negative z) grow and away shrink - the WPF-3D-tile look. Off (affine) when Perspective is 0. Composed as
        // T(-c) * M34(-1/d) * T(c) AFTER the affine transform: depth produced by the rotation feeds the divide.
        var d = Perspective;
        if (d > 0)
        {
            var persp = Matrix4x4.Identity;
            persp.M34 = -1.0 / d;
            matrix = matrix * toCenter * persp * fromCenter;
        }

        return matrix;
    }
}
