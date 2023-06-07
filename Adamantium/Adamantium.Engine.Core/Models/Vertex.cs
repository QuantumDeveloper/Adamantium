using System;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Core.Models
{
    internal struct Vertex : IEquatable<Vertex>
    {
        public readonly Vector3F Position;
        public readonly Vector2F UV0;
        public readonly Vector2F UV1;
        public readonly Vector2F UV2;
        public readonly Vector2F UV3;
        public readonly Color Color;
        public readonly Vector4F JointIndex;
        public readonly Vector4F JointWeight;

        public Vertex(Vector3F position, Vector2F uv0, Vector2F uv1, Vector2F uv2, Vector2F uv3, Color color, Vector4F jointIndex, Vector4F jointWeight)
        {
            Position = position;
            UV0 = uv0;
            UV1 = uv1;
            UV2 = uv2;
            UV3 = uv3;
            Color = color;
            JointIndex = jointIndex;
            JointWeight = jointWeight;
        }

        public bool Equals(Vertex vertex)
        {
            return Position.Equals(vertex.Position) &&
                  UV0.Equals(vertex.UV0) &&
                  UV1.Equals(vertex.UV1) &&
                  UV2.Equals(vertex.UV2) &&
                  UV3.Equals(vertex.UV3) &&
                  Color.Equals(vertex.Color) &&
                  JointIndex.Equals(vertex.JointIndex) &&
                  JointWeight.Equals(vertex.JointWeight);
        }

        public static bool operator ==(Vertex left, Vertex right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vertex left, Vertex right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            int hashCode = Position.GetHashCode();
            hashCode = (hashCode * 25) ^ UV0.GetHashCode();
            hashCode = (hashCode * 25) ^ UV1.GetHashCode();
            hashCode = (hashCode * 25) ^ UV2.GetHashCode();
            hashCode = (hashCode * 25) ^ UV3.GetHashCode();
            hashCode = (hashCode * 25) ^ Color.GetHashCode();
            //hashCode = (hashCode * 25) ^ JointIndex.GetHashCode();
            //hashCode = (hashCode * 25) ^ JointWeight.GetHashCode();
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            return obj is Vertex && Equals((Vertex)obj);
        }
    }
}
