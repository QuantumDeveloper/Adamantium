using Adamantium.Mathematics;

namespace Adamantium.ECS.Components.Extensions;

/// <summary>
/// Where a ray met something. The default value is a miss.
/// </summary>
public readonly struct PickHit
{
    public PickHit(Entity entity, Vector3F point, float depth)
    {
        Entity = entity;
        Point = point;
        Depth = depth;
        IsHit = true;
    }

    public bool IsHit { get; }

    public Entity Entity { get; }

    /// <summary>In the render space of the ray's camera.</summary>
    public Vector3F Point { get; }

    /// <summary>The distance along the ray.</summary>
    public float Depth { get; }

    public static PickHit Nearest(PickHit first, PickHit second)
    {
        if (!second.IsHit)
        {
            return first;
        }

        if (!first.IsHit || second.Depth < first.Depth)
        {
            return second;
        }

        return first;
    }

    public override string ToString()
    {
        return IsHit ? $"{Entity} at {Point}, depth {Depth}" : "Miss";
    }
}
