using System;
using System.Runtime.InteropServices;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
   [StructLayout(LayoutKind.Sequential)]
   public struct VertexPositionColorTexture : IEquatable<VertexPositionColorTexture>
   {
      /// <summary>
      /// Create instace of <see cref="VertexPositionColorTexture"/> 
      /// </summary>
      /// <param name="position"></param>
      /// <param name="uv"></param>
      /// <param name="color"></param>
      public VertexPositionColorTexture(Vector3F position, Vector2F uv, Color color)
      {
         Position = position;
         UV = uv;
         Color = color;
      }
      /// <summary>
      /// XYZ position.
      /// </summary>
      [VertexInputElement("SV_Position")]
      public Vector3F Position;

      /// <summary>
      /// The vertex color.
      /// </summary>
      [VertexInputElement("COLOR")]
      public Color Color;

      /// <summary>
      /// UV texture coordinates.
      /// </summary>
      [VertexInputElement("TEXCOORD0")]
      public Vector2F UV;

      /// <summary>
      /// Defines structure byte size.
      /// </summary>
      public static readonly int Size = Marshal.SizeOf<VertexPositionColorTexture>();

      public bool Equals(VertexPositionColorTexture other)
      {
         return Position.Equals(other.Position) && UV.Equals(other.UV)
            && Color.Equals(other.Color);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         return obj is VertexPositionColorTexture && Equals((VertexPositionColorTexture)obj);
      }

      public override int GetHashCode()
      {
         unchecked
         {
            int hashCode = Position.GetHashCode();
            hashCode = (hashCode * 397) ^ UV.GetHashCode();
            hashCode = (hashCode * 397) ^ Color.GetHashCode();
            return hashCode;
         }
      }

      public static bool operator ==(VertexPositionColorTexture left, VertexPositionColorTexture right)
      {
         return left.Equals(right);
      }

      public static bool operator !=(VertexPositionColorTexture left, VertexPositionColorTexture right)
      {
         return !left.Equals(right);
      }

      public override string ToString()
      {
         return string.Format("Position: {0}, Texcoord: {1}, Color: {2}",
            Position, UV, Color);
      }
   }
}
