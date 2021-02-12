using System;

namespace Adamantium.Mathematics
{
    public struct LineSegmentF : IEquatable<LineSegmentF>
    {
        public Vector3F Start { get; set; }

        public Vector3F End { get; set; }

        public Vector3F Direction { get; }

        public Vector3F DirectionNormalized { get; }

        public LineSegmentF(Vector3F start, Vector3F end)
        {
            Start = start;
            End = end;
            Direction = end - start;
            DirectionNormalized = Vector3D.Normalize(Direction);
        }

        public bool Equals(LineSegmentF other)
        {
            return MathHelper.WithinEpsilon(Start, other.Start, Polygon.Epsilon) && MathHelper.WithinEpsilon(End, other.End, Polygon.Epsilon);
        }

        public bool EqualsInvariant(LineSegmentF other)
        {
            if ((Start == other.Start && End == other.End) ||
                (Start == other.End && End == other.Start))
            {
                return true;
            }
            return false;
        }

        public static bool operator ==(LineSegmentF segment1, LineSegmentF segment2)
        {
            return segment1.Equals(segment2);
        }

        public static bool operator !=(LineSegmentF segment1, LineSegmentF segment2)
        {
            return !segment1.Equals(segment2);
        }

        public override string ToString()
        {
            return $"Start = {Start} End = {End}";
        }
    }
}