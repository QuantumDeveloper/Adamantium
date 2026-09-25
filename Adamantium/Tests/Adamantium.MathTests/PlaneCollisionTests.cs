using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.MathTests;

[TestFixture]
public class PlaneCollisionTests
{
    [Test]
    public void DistanceToAPlane_IsPositiveOnTheSideItsNormalFaces()
    {
        var plane = new Plane(new Vector3F(0, 2, 0), Vector3F.UnitY);
        var point = new Vector3F(3, 5, -1);

        Assert.That(Collision.DistancePlanePoint(ref plane, ref point), Is.EqualTo(3f).Within(1e-5f));
    }

    [Test]
    public void ClosestPointOnAPlane_LiesOnIt_BelowThePoint()
    {
        var plane = new Plane(new Vector3F(0, 2, 0), Vector3F.UnitY);
        var point = new Vector3F(3, 5, -1);

        Collision.ClosestPointPlanePoint(ref plane, ref point, out var closest);

        Assert.That((closest - new Vector3F(3, 2, -1)).Length(), Is.LessThan(1e-5f));
    }

    [Test]
    public void TwoPlanes_MeetAlongALineOnBoth()
    {
        var first = new Plane(new Vector3F(1, 0, 0), Vector3F.UnitX);
        var second = new Plane(new Vector3F(0, 2, 0), Vector3F.Normalize(new Vector3F(1, 1, 0)));

        Assert.That(Collision.PlaneIntersectsPlane(ref first, ref second, out var line), Is.True);

        foreach (var along in new[] { -3f, 0f, 5f })
        {
            var point = line.Position + line.Direction * along;
            Assert.That(Collision.DistancePlanePoint(ref first, ref point), Is.EqualTo(0f).Within(1e-4f), $"first plane at {along}");
            Assert.That(Collision.DistancePlanePoint(ref second, ref point), Is.EqualTo(0f).Within(1e-4f), $"second plane at {along}");
        }
    }
}
