using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.MathTests;

[TestFixture]
public class BoundingSphereTests
{
    private static readonly OrientedBoundingBox Turned =
        new(Vector3F.Zero, Vector3F.One, QuaternionF.RotationAxis(Vector3F.UnitZ, MathHelper.PiOverFour));

    [Test]
    public void ASphereInATurnedBoxCorner_Intersects()
    {
        var box = Turned;
        var sphere = new BoundingSphere(new Vector3F(1.3f, 0, 0), 0.05f);

        Assert.That(sphere.Intersects(ref box), Is.True);
        Assert.That(Collision.BoxIntersectsSphere(ref box, ref sphere), Is.True);
    }

    [Test]
    public void ASphereOffATurnedBoxEdge_IsDisjoint()
    {
        var box = Turned;
        var sphere = new BoundingSphere(new Vector3F(1.2f, 1.2f, 0), 0.2f);

        Assert.That(sphere.Intersects(ref box), Is.False);
        Assert.That(Collision.BoxIntersectsSphere(ref box, ref sphere), Is.False);
    }

    [Test]
    public void ASphereRoundATurnedBox_ContainsIt()
    {
        var box = Turned;
        var sphere = new BoundingSphere(Vector3F.Zero, 1.8f);

        Assert.That(sphere.Contains(ref box), Is.EqualTo(ContainmentType.Contains));
    }
}
