namespace Adamantium.Mathematics
{
    public struct Ray2F
    {
        public Vector2F Origin;
        public Vector2F Direction;

        public Ray2F(Vector2F origin, Vector2F direction)
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