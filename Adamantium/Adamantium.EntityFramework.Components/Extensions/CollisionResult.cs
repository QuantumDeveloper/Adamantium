using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.Components.Extensions
{
    public enum CompareOrder
    {
        Less,
        Greater
    }

    public class CollisionResult
    {
        public float? Distance { get; private set; }
        public Entity Entity { get; set; }
        public Vector3F IntersectionPoint { get; set; }
        public bool Intersects { get; set; }

        public CompareOrder CompareOrder { get; set; }

        public CollisionResult()
        { }

        public CollisionResult(CompareOrder compareOrder)
        {
            CompareOrder = compareOrder;
        }

        public void ValidateAndSetValues(
            Entity entity,
            Vector3F interPoint,
            bool intersects)
        {
            if (!intersects)
                return;

            Intersects = intersects;
            var distance = Vector3F.DistanceSquared(Vector3F.Zero, interPoint);
            if (!Distance.HasValue)
            {
                Distance = distance;
                Entity = entity;
                IntersectionPoint = interPoint;
            }
            if (CompareOrder == CompareOrder.Less)
            {
                if (distance < Distance.Value)
                {
                    Distance = distance;
                    Entity = entity;
                    IntersectionPoint = interPoint;
                }
            }
            else
            {
                if (distance > Distance.Value)
                {
                    Distance = distance;
                    Entity = entity;
                    IntersectionPoint = interPoint;
                }
            }
        }

        public void ValidateAgainst(CollisionResult result)
        {
            if (!result.Intersects)
                return;

            ValidateAndSetValues(result.Entity, result.IntersectionPoint, true);
        }

        public override string ToString()
        {
            return $"Intersects: {Intersects}, IntersectionPoint {IntersectionPoint}, Entity: {Entity}";
        }
    }
}
