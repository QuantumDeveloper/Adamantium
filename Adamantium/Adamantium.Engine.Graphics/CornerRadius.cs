using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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