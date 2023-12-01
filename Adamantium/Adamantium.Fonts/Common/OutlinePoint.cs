using Adamantium.Mathematics;
using MessagePack;

namespace Adamantium.Fonts.Common
{
    [MessagePackObject]
    internal struct OutlinePoint
    {
        [Key(0)]
        public double X { get; set; }
        [Key(1)]
        public double Y { get; set; }
        [Key(2)]
        public bool IsControl { get; set; }

        public OutlinePoint(double x, double y, bool control = false)
        {
            X = x;
            Y = y;
            IsControl = control;
        }

        public override string ToString()
        {
            return $"X: {X}, Y: {Y}, IsControl: {IsControl}";
        }

        public static implicit  operator Vector2(OutlinePoint point)
        {
            return new(point.X, point.Y);
        }
    }
}
