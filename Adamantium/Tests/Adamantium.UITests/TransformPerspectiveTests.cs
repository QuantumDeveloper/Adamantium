using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The 3D tile look: a Transform with <see cref="Transform.Perspective"/> must produce a w-divide term (M34)
/// once rotated, and that term must survive into the owner's <see cref="Adamantium.UI.Core.IUIComponent.LocalTransform"/>
/// (the render pass composes world matrices from it - a lost M34 renders tilts flat, no depth).</summary>
[TestFixture]
public class TransformPerspectiveTests
{
    [Test]
    public void RotatedPerspectiveTransform_CarriesM34()
    {
        var transform = new Transform { Perspective = 900, RotationCenterX = 42.5, RotationCenterY = 42.5 };
        transform.RotationY = 30;

        Assert.That(transform.Matrix.M34, Is.Not.EqualTo(0).Within(1e-9),
            $"Perspective term lost: M34={transform.Matrix.M34}");
        // -cos(30°)/900 ≈ -0.000962 (row-vector composition: affine rotation, then the centre-anchored w-divide).
        Assert.That(transform.Matrix.M34, Is.EqualTo(-0.000962).Within(1e-4));
    }

    [Test]
    public void LocalTransform_PreservesPerspective()
    {
        var transform = new Transform { Perspective = 900, RotationCenterX = 42.5, RotationCenterY = 42.5 };
        var element = new Border { RenderTransform = transform };
        transform.RotationY = 30;

        Assert.That(element.LocalTransform.M34, Is.Not.EqualTo(0f),
            $"LocalTransform dropped the perspective term: M34={element.LocalTransform.M34}");
    }
}
