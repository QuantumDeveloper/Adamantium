using System;

namespace Adamantium.UI
{
    public struct CornerRadius
    {
        public Double TopLeft { get; set; }
      
        public Double TopRight { get; set; }
      
        public Double BottomRight { get; set; }
      
        public Double BottomLeft { get; set; }

        public CornerRadius(Double topLeft, Double topRight, Double bottomRight, Double bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
        }

        public CornerRadius(Double uniformValue)
        {
            TopLeft = TopRight = BottomRight = BottomLeft = uniformValue;
        }

        public static implicit operator Engine.Graphics.CornerRadius(CornerRadius corners)
        {
            return new Engine.Graphics.CornerRadius(
                (float)corners.TopLeft, 
                (float)corners.TopRight,
                (float)corners.BottomRight, 
                (float)corners.BottomLeft);
        }

        public override string ToString()
        {
            return $"{nameof(TopLeft)}: {TopLeft} {nameof(TopRight)}: {TopRight} {nameof(BottomRight)}: {BottomRight} {nameof(BottomLeft)}: {BottomLeft}";
        }
    }
}