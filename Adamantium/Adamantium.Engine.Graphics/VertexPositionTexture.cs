using System;
using System.Runtime.InteropServices;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
   /// <summary>
   /// Vertex structure containing position and texture coordinates
   /// </summary>
   [StructLayout(LayoutKind.Sequential)]
   public struct VertexPositionTexture : IEquatable<VertexPositionTexture>
   {
      /// <summary>
      /// Initializes a new <see cref="VertexPositionTexture"/> instance.
      /// </summary>
      /// <param name="position"></param>
      /// <param name="uv"></param>
      public VertexPositionTexture(Vector3F position, Vector2F uv)
      {
         Position = position;
         UV = uv;
      }

      /// <summary>
      /// XYZ position.
      /// </summary>
      [VertexInputElement("SV_Position")]
      public Vector3F Position;

      /// <summary>
      /// UV texture coordinates.
      /// </summary>
      [VertexInputElement("TEXCOORD0")]
      public Vector2F UV;

      /// <summary>
      /// Defines structure byte size.
      /// </summary>
      public static readonly int Size = Marshal.OffsetOf(typeof(VertexPositionTexture), "UV").ToInt32();

      public bool Equals(VertexPositionTexture other)
      {
         return Position.Equals(other.Position) && UV.Equals(other.UV);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         return obj is VertexPositionTexture && Equals((VertexPositionTexture)obj);
      }

      public override int GetHashCode()
      {
         unchecked
         {
            int hashCode = Position.GetHashCode();
            hashCode = (hashCode * 397) ^ UV.GetHashCode();
            return hashCode;
         }
      }

      public static bool operator ==(VertexPositionTexture left, VertexPositionTexture right)
      {
         return left.Equals(right);
      }

      public static bool operator !=(VertexPositionTexture left, VertexPositionTexture right)
      {
         return !left.Equals(right);
      }

      public override string ToString()
      {
         return $"Position: {Position}, UV: {UV}";
      }
   }
}
