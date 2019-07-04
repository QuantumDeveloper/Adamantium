using System;
using System.Runtime.InteropServices;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
   /// <summary>
   /// Vertex structure for position and color
   /// </summary>
   [StructLayout(LayoutKind.Sequential)]
   public struct VertexPositionColor: IEquatable<VertexPositionColor>
   {
      /// <summary>
      /// Initializes a new <see cref="VertexPositionColor"/> instance.
      /// </summary>
      /// <param name="position">position of the vertex</param>
      /// <param name="color">vertex color</param>
      public VertexPositionColor(Vector3F position, Color color)
      {
         Position = position;
         Color = color;
      }

      /// <summary>
      /// XYZ position.
      /// </summary>
      [VertexInputElement("SV_Position")]
      public Vector3F Position;

      /// <summary>
      /// UV texture coordinates.
      /// </summary>
      [VertexInputElement("COLOR0")]
      public Color Color;

      /// <summary>
      /// Defines structure byte size.
      /// </summary>
      public static readonly int Size = Marshal.SizeOf<VertexPositionColor>();

      public bool Equals(VertexPositionColor other)
      {
         return Position.Equals(other.Position) && Color.Equals(other.Color);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         return obj is VertexPositionColor && Equals((VertexPositionColor)obj);
      }

      public override int GetHashCode()
      {
         unchecked
         {
            int hashCode = Position.GetHashCode();
            hashCode = (hashCode * 397) ^ Color.GetHashCode();
            return hashCode;
         }
      }

      public static bool operator ==(VertexPositionColor left, VertexPositionColor right)
      {
         return left.Equals(right);
      }

      public static bool operator !=(VertexPositionColor left, VertexPositionColor right)
      {
         return !left.Equals(right);
      }

      public override string ToString()
      {
         return $"Position: {Position}, Color: {Color}";
      }
   }
}
