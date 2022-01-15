using System;

namespace Adamantium.Engine.Graphics
{
    public struct CornerRadius
    {
        public CornerRadius(Double value)
        {
            TopLeft = TopRight = BottomRight = BottomLeft = value;
        }

        public CornerRadius(Double topLeft, Double topRight, Double bottomRight, Double bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
        }
        
        public Double TopLeft;

        public Double TopRight;

        public Double BottomRight;

        public Double BottomLeft;
        
        public override string ToString()
        {
            return $"{nameof(TopLeft)}: {TopLeft} {nameof(TopRight)}: {TopRight} {nameof(BottomRight)}: {BottomRight} {nameof(BottomLeft)}: {BottomLeft}";
        }
    }
}