using System;
using System.Runtime.InteropServices;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
   [StructLayout(LayoutKind.Sequential)]
   public struct VertexPositionNormalTexture:IEquatable<VertexPositionNormalTexture>
   {
      /// <summary>
      /// Create instace of <see cref="VertexPositionNormalTexture"/> 
      /// </summary>
      /// <param name="position"></param>
      /// <param name="uv"></param>
      /// <param name="normal"></param>
      public VertexPositionNormalTexture(Vector3F position, Vector3F normal, Vector2F uv) : this()
      {
         Position = position;
         UV = uv;
         Normal = normal;
      }
      /// <summary>
      /// XYZ position.
      /// </summary>
      [VertexInputElement("SV_Position")]
      public Vector3F Position;

      /// <summary>
      /// The vertex color.
      /// </summary>
      [VertexInputElement("NORMAL")]
      public Vector3F Normal;

      /// <summary>
      /// UV texture coordinates.
      /// </summary>
      [VertexInputElement("TEXCOORD0")]
      public Vector2F UV;


      /// <summary>
      /// Defines structure byte size.
      /// </summary>
      public static readonly int Size = Marshal.SizeOf<VertexPositionNormalTexture>();

      public bool Equals(VertexPositionNormalTexture other)
      {
         return Position.Equals(other.Position) && UV.Equals(other.UV) && Normal.Equals(other.Normal);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         return obj is VertexPositionNormalTexture && Equals((VertexPositionNormalTexture)obj);
      }

      public override int GetHashCode()
      {
         unchecked
         {
            int hashCode = Position.GetHashCode();
            hashCode = (hashCode * 397) ^ Normal.GetHashCode();
            hashCode = (hashCode * 397) ^ UV.GetHashCode();
            return hashCode;
         }
      }

      public static bool operator ==(VertexPositionNormalTexture left, VertexPositionNormalTexture right)
      {
         return left.Equals(right);
      }

      public static bool operator !=(VertexPositionNormalTexture left, VertexPositionNormalTexture right)
      {
         return !left.Equals(right);
      }

      public override string ToString()
      {
         return $"Position: {Position}, Normal {Normal}, UV: {UV}";
      }

      
   }
}
