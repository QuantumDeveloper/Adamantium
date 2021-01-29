namespace Adamantium.EntityFramework.Components.Extensions
{
    /// <summary>
    /// Collision mode for checking entity collisions
    /// </summary>
    public enum CollisionMode
    {
        /// <summary>
        /// Only current entity bounding box will be tested for intersection
        /// </summary>
        FirstAvailableCollider = 0,

        /// <summary>
        /// Will look for intersection only in geometry components
        /// </summary>
        IgnoreNonGeometryParts = 2,

        /// <summary>
        /// Will do intersection test per triangle/line segment (the most ineffecient way, but the most precise)
        /// </summary>
        HighestPrecision = 3,

        /// <summary>
        /// Works as <see cref="IgnoreNonGeometryParts"/>, but if Mesh does not have a collider - it will check collision with HighestPrecision mode
        /// Meshes without colliders will have higher priority 
        /// </summary>
        Mixed = 4,

        /// <summary>
        /// Works as <see cref="Mixed"/>, but stops if found Mesh that have a collider 
        /// </summary>
        MixedPreferCollider = 5,

        /// <summary>
        /// Ignore geometry without <see cref="Collider"/>
        /// </summary>
        CollidersOnly = 6
    }
}
