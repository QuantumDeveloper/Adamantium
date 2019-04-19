namespace Adamantium.Mathematics
{
    public struct Ray2D
    {
        public Vector2D Origin;
        public Vector2D Direction;

        public Ray2D(Vector2D origin, Vector2D direction)
        {
            Origin = origin;
            Direction = direction;
        }

        public override string ToString()
        {
            return $"Origin = {Origin}, Direction = {Direction}";
        }
    }
}
