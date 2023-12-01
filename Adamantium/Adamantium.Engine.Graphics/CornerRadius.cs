using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public struct CornerRadius
    {
        public bool Equals(CornerRadius other)
        {
            return TopLeft.Equals(other.TopLeft) && TopRight.Equals(other.TopRight) && BottomRight.Equals(other.BottomRight) && BottomLeft.Equals(other.BottomLeft);
        }

        public override bool Equals(object obj)
        {
            return obj is CornerRadius other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(TopLeft, TopRight, BottomRight, BottomLeft);
        }

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

        public CornerRadius(IEnumerable<double> values)
        {
            var lst = values as List<double> ?? values.ToList();

            if (lst.Count < 4) throw new ArgumentOutOfRangeException($"Arguments count for Corner radius should be 4, but provided {lst.Count}");

            TopLeft = lst[0];
            TopRight = lst[1];
            BottomRight = lst[2];
            BottomLeft = lst[3];
        }
        
        public Double TopLeft;

        public Double TopRight;

        public Double BottomRight;

        public Double BottomLeft;
        
        public override string ToString()
        {
            return $"{nameof(TopLeft)}: {TopLeft} {nameof(TopRight)}: {TopRight} {nameof(BottomRight)}: {BottomRight} {nameof(BottomLeft)}: {BottomLeft}";
        }

        static CornerRadius()
        {
            Empty = new CornerRadius();
        }

        public static CornerRadius Empty { get; }

        public static bool operator ==(CornerRadius radius1, CornerRadius radius2)
        {
            return MathHelper.NearEqual(radius1.TopLeft, radius2.TopLeft) &&
                   MathHelper.NearEqual(radius1.TopRight, radius2.TopRight) &&
                   MathHelper.NearEqual(radius1.BottomRight, radius2.BottomRight) &&
                   MathHelper.NearEqual(radius1.BottomLeft, radius2.BottomLeft);
        }

        public static bool operator !=(CornerRadius radius1, CornerRadius radius2)
        {
            return !(radius1 == radius2);
        }


        public static CornerRadius Parse(string value)
        {
            var values = value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (values.Length == 1)
            {
                return new CornerRadius(double.Parse(values[0], CultureInfo.InvariantCulture));
            }

            var list = new List<double>();
            foreach (var v in values)
            {
                list.Add(double.Parse(v, CultureInfo.InvariantCulture));
            }

            return new CornerRadius(list);
        }
    }
}