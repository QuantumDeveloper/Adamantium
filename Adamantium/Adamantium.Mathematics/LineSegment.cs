using System;

namespace Adamantium.Mathematics
{
    public struct LineSegment : IEquatable<LineSegment>
    {
        public Vector2D Start { get; set; }

        public Vector2D End { get; set; }

        public Vector2D Direction { get; }

        public Vector2D DirectionNormalized { get; }

        public LineSegment(Vector2D start, Vector2D end)
        {
            Start = start;
            End = end;
            Direction = end - start;
            DirectionNormalized = Vector2D.Normalize(Direction);
        }

        public bool Equals(LineSegment other)
        {
            return MathHelper.WithinEpsilon(Start, other.Start, Polygon.Epsilon) && MathHelper.WithinEpsilon(End, other.End, Polygon.Epsilon);
        }

        public bool EqualsInvariant(LineSegment other)
        {
            if ((Start == other.Start && End == other.End) ||
                (Start == other.End && End == other.Start))
            {
                return true;
            }
            return false;
        }

        public static bool operator ==(LineSegment segment1, LineSegment segment2)
        {
            return segment1.Equals(segment2);
        }

        public static bool operator !=(LineSegment segment1, LineSegment segment2)
        {
            return !segment1.Equals(segment2);
        }

        public override string ToString()
        {
            return $"Start = {Start} End = {End}";
        }
    }
}
