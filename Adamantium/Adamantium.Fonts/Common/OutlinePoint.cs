using Adamantium.Mathematics;

namespace Adamantium.Fonts.Common
{
    internal struct OutlinePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
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

        public static implicit  operator Vector2D(OutlinePoint point)
        {
            return new(point.X, point.Y);
        }
    }
}
