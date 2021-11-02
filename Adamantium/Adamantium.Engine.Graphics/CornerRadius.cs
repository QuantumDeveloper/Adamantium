namespace Adamantium.Engine.Graphics
{
    public struct CornerRadius
    {
        public CornerRadius(float value)
        {
            TopLeft = TopRight = BottomRight = BottomLeft = value;
        }

        public CornerRadius(float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
        }
        
        public float TopLeft;

        public float TopRight;

        public float BottomRight;

        public float BottomLeft;
        
        public override string ToString()
        {
            return $"{nameof(TopLeft)}: {TopLeft} {nameof(TopRight)}: {TopRight} {nameof(BottomRight)}: {BottomRight} {nameof(BottomLeft)}: {BottomLeft}";
        }
    }
}