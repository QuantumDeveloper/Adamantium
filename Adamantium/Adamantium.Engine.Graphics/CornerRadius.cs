namespace Adamantium.Engine.Graphics
{
    public struct CornerRadius
    {
        public CornerRadius(double value)
        {
            TopLeft = TopRight = BottomRight = BottomLeft = value;
        }

        public CornerRadius(double topLeft, double topRight, double bottomRight, double bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
        }
        
        public double TopLeft;

        public double TopRight;

        public double BottomRight;

        public double BottomLeft;
        
        public override string ToString()
        {
            return $"{nameof(TopLeft)}: {TopLeft} {nameof(TopRight)}: {TopRight} {nameof(BottomRight)}: {BottomRight} {nameof(BottomLeft)}: {BottomLeft}";
        }
    }
}