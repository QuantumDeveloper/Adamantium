using System;

namespace Adamantium.Mathematics
{
    public struct LineSegment2D : IEquatable<LineSegment2D>
    {
        public override bool Equals(object obj)
        {
            return obj is LineSegment2D other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Start, End, Direction, DirectionNormalized);
        }

        public Vector2D Start { get; }

        public Vector2D End { get; }

        public Vector2D Direction { get; }

        public Vector2D DirectionNormalized { get; }

        public Color StartInnerColor;
        public Color StartOuterColor;
        public Color EndInnerColor;
        public Color EndOuterColor;

        public Color OuterColor;
        
        public Color InnerColor;

        public Color MsdfColor;

        public LineSegment2D(Vector2D start, Vector2D end)
        {
            Start = start;
            End = end;
            Direction = end - start;
            DirectionNormalized = Vector2D.Normalize(Direction);
            StartOuterColor = EndOuterColor = OuterColor = Colors.Black;
            StartInnerColor = EndInnerColor = InnerColor = Colors.White;

            MsdfColor = Colors.White;
        }

        public bool Equals(LineSegment2D other)
        {
            return MathHelper.WithinEpsilon(Start, other.Start, Polygon.Epsilon) && MathHelper.WithinEpsilon(End, other.End, Polygon.Epsilon);
        }

        public bool EqualsInvariant(ref LineSegment2D other)
        {
            return (Start == other.Start && End == other.End) ||
                   (Start == other.End && End == other.Start);
        }

        public static bool operator ==(LineSegment2D segment1, LineSegment2D segment2)
        {
            return segment1.Equals(segment2);
        }

        public static bool operator !=(LineSegment2D segment1, LineSegment2D segment2)
        {
            return !segment1.Equals(segment2);
        }

        public override string ToString()
        {
            return $"Start = {Start} End = {End}";
        }
    }
}
