namespace Adamantium.Mathematics
{
    public struct Ray2D
    {
        public Vector2 Origin;
        public Vector2 Direction;

        public Ray2D(Vector2 origin, Vector2 direction)
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
