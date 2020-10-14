using System;
using System.Runtime.InteropServices;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Vertex struct containing position, color, normal, texture, tangent, butangent, jointIndices and JointWeights
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MeshVertex : IEquatable<MeshVertex>
    {
       /// <summary>
       /// Initializes a new <see cref="SkinnedMeshVertex"/> instance.
       /// </summary>
       /// <param name="position">The position of this vertex.</param>
       /// <param name="color">The vertex color.</param>
       /// <param name="normal">The vertex normal.</param>
       /// <param name="uv0">UV texture coordinates.</param>
       /// <param name="uv1">UV texture coordinates.</param>
       /// <param name="uv2">UV texture coordinates.</param>
       /// <param name="uv3">UV texture coordinates.</param>
       /// <param name="tangent">tangent.</param>
       /// <param name="bitangent">tangent.</param>
       public MeshVertex(
          Vector3F position,
          Vector3F normal,
          Color color,
          Vector2F uv0,
          Vector2F uv1,
          Vector2F uv2,
          Vector2F uv3,
          Vector4F tangent,
          Vector3F bitangent)
          : this()
       {
          Position = position;
          Color = color;
          Normal = normal;
          UV0 = uv0;
          UV1 = uv1;
          UV2 = uv2;
          UV3 = uv3;
          Tangent = tangent;
          BiTangent = bitangent;
       }

       /// <summary>
        /// XYZ position.
        /// </summary>
        [VertexInputElement("SV_POSITION")] public Vector3F Position;

        /// <summary>
        /// The vertex color.
        /// </summary>
        [VertexInputElement("COLOR")] public Color Color;

        /// <summary>
        /// The vertex normal.
        /// </summary>
        [VertexInputElement("NORMAL")] public Vector3F Normal;

        /// <summary>
        /// UV texture coordinates.
        /// </summary>
        [VertexInputElement("TEXCOORD0")] public Vector2F UV0;

        /// <summary>
        /// UV texture coordinates.
        /// </summary>
        [VertexInputElement("TEXCOORD1")]
        public Vector2F UV1;

        /// <summary>
        /// UV texture coordinates.
        /// </summary>
        [VertexInputElement("TEXCOORD2")]
        public Vector2F UV2;

        /// <summary>
        /// UV texture coordinates.
        /// </summary>
        [VertexInputElement("TEXCOORD3")]
        public Vector2F UV3;

        /// <summary>
        /// Tangent.
        /// </summary>
        [VertexInputElement("TANGENT")] public Vector4F Tangent;

        /// <summary>
        /// Tangent.
        /// </summary>
        [VertexInputElement("BINORMAL")] public Vector3F BiTangent;

        /// <summary>
        /// Defines structure byte size.
        /// </summary>
        public static readonly int Size = Marshal.SizeOf<MeshVertex>();

        public bool Equals(MeshVertex other)
        {
            return Position.Equals(other.Position) && Normal.Equals(other.Normal) && UV0.Equals(other.UV0) && UV1.Equals(other.UV1) && UV2.Equals(other.UV2)
                && UV3.Equals(other.UV3) && Color.Equals(other.Color) && Tangent.Equals(other.Tangent) && BiTangent.Equals(other.BiTangent);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is MeshVertex && Equals((MeshVertex) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Position.GetHashCode();
                hashCode = (hashCode*397) ^ Normal.GetHashCode();
                hashCode = (hashCode*397) ^ UV0.GetHashCode();
                hashCode = (hashCode * 397) ^ UV1.GetHashCode();
                hashCode = (hashCode * 397) ^ UV2.GetHashCode();
                hashCode = (hashCode * 397) ^ UV3.GetHashCode();
                hashCode = (hashCode*397) ^ Color.GetHashCode();
                hashCode = (hashCode*397) ^ Tangent.GetHashCode();
                hashCode = (hashCode*397) ^ BiTangent.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Comparing operator for two FullVertex structs
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator ==(MeshVertex left, MeshVertex right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Comparing operator for two FullVertex structs
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static bool operator !=(MeshVertex left, MeshVertex right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return
                $"Position: {Position}, Normal: {Normal}, UV0: {UV0}, UV1: {UV1}, UV2: {UV2}, UV3: {UV3}, Color: {Color}, Tangent: {Tangent}, BiTangent: {BiTangent}";
        }
    }
}
