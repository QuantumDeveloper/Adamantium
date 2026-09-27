namespace Adamantium.Mathematics;

/// <summary>
/// A convex volume known through its support function, which is all <see cref="Gjk"/> needs to test any two of them.
/// </summary>
public interface IConvexShape
{
    /// <summary>The point of the shape farthest along <paramref name="direction"/>.</summary>
    Vector3F Support(Vector3F direction);
}
