using System;

namespace Adamantium.Mathematics;

/// <summary>
/// Every point within <see cref="Radius"/> of the segment from <see cref="Start"/> to <see cref="End"/>.
/// </summary>
public struct BoundingCapsule : IConvexShape
{
    public Vector3F Start;
    public Vector3F End;
    public float Radius;

    public BoundingCapsule(Vector3F start, Vector3F end, float radius)
    {
        Start = start;
        End = end;
        Radius = radius;
    }

    public Vector3F Center => (Start + End) * 0.5f;

    /// <summary>The length of the segment, without the rounded ends.</summary>
    public float Length => (End - Start).Length();

    /// <summary>
    /// The capsule carried through an affine matrix. The radius takes the largest axis scale, so a non-uniform scale
    /// keeps it enclosing.
    /// </summary>
    public BoundingCapsule Transform(Matrix4x4F matrix)
    {
        var scale = Math.Max(matrix.Right.Length(), Math.Max(matrix.Up.Length(), matrix.Forward.Length()));
        return new BoundingCapsule(Vector3F.TransformCoordinate(Start, matrix), Vector3F.TransformCoordinate(End, matrix), Radius * scale);
    }

    public Vector3F Support(Vector3F direction)
    {
        var end = Vector3F.Dot(direction, End - Start) >= 0 ? End : Start;
        var length = direction.Length();
        return length > 0 ? end + direction * (Radius / length) : end + new Vector3F(Radius, 0, 0);
    }

    public ContainmentType Contains(ref Vector3F point)
    {
        var closest = Collision.ClosestPointSegmentPoint(Start, End, point);
        return Vector3F.DistanceSquared(closest, point) <= Radius * Radius ? ContainmentType.Contains : ContainmentType.Disjoint;
    }

    /// <summary>Where a ray with a unit direction first meets the capsule; a ray starting inside meets it at 0.</summary>
    public bool Intersects(ref Ray ray, out float distance)
    {
        return Collision.RayIntersectsCapsule(ref ray, ref this, out distance);
    }

    /// <summary>Where a ray with a unit direction first meets the capsule; a ray starting inside meets it at its origin.</summary>
    public bool Intersects(ref Ray ray, out Vector3F point)
    {
        if (!Collision.RayIntersectsCapsule(ref ray, ref this, out var distance))
        {
            point = Vector3F.Zero;
            return false;
        }

        point = ray.Position + ray.Direction * distance;
        return true;
    }

    public bool Intersects(ref BoundingSphere sphere)
    {
        return Collision.CapsuleIntersectsSphere(ref this, ref sphere);
    }

    public bool Intersects(ref BoundingCapsule capsule)
    {
        return Collision.CapsuleIntersectsCapsule(ref this, ref capsule);
    }

    public bool Intersects(ref OrientedBoundingBox box)
    {
        return Gjk.Intersects(this, box);
    }

    public bool Intersects(ref BoundingBox box)
    {
        return Gjk.Intersects(this, box);
    }

    public bool Intersects(ConvexHull hull)
    {
        return Gjk.Intersects(this, hull);
    }

    /// <summary>On which side of a plane the capsule lies, or whether the plane cuts it.</summary>
    public PlaneIntersectionType Intersects(ref Plane plane)
    {
        var start = Plane.DotCoordinate(plane, Start);
        var end = Plane.DotCoordinate(plane, End);
        if (Math.Min(start, end) > Radius)
        {
            return PlaneIntersectionType.Front;
        }

        return Math.Max(start, end) < -Radius ? PlaneIntersectionType.Back : PlaneIntersectionType.Intersecting;
    }

    public BoundingBox GetBoundingBox()
    {
        var radius = new Vector3F(Radius);
        return new BoundingBox(Vector3F.Min(Start, End) - radius, Vector3F.Max(Start, End) + radius);
    }

    public override string ToString()
    {
        return $"{{Start:{Start} End:{End} Radius:{Radius}}}";
    }
}
