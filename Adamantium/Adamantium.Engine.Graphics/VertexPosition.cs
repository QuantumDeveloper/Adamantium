using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
   /// <summary>
   /// Vertex structure for position only
   /// </summary>
   [StructLayout(LayoutKind.Sequential)]
   public struct VertexPosition : IEquatable<VertexPosition>
   {
      /// <summary>
      /// Initializes a new <see cref="VertexPositionColor"/> instance.
      /// </summary>
      /// <param name="position">position of the vertex</param>
      public VertexPosition(Vector3F position):this()
      {
         Position = position;
      }

      /// <summary>
      /// XYZ position.
      /// </summary>
      [VertexInputElement("SV_Position")]
      public Vector3F Position;

      /// <summary>
      /// Defines structure byte size.
      /// </summary>
      public static readonly int Size = Marshal.SizeOf<VertexPosition>();

      public bool Equals(VertexPosition other)
      {
         return Position.Equals(other.Position);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         return obj is VertexPosition && Equals((VertexPosition)obj);
      }

      public override int GetHashCode()
      {
         unchecked
         {
            int hashCode = Position.GetHashCode();
            return hashCode;
         }
      }

      public static bool operator ==(VertexPosition left, VertexPosition right)
      {
         return left.Equals(right);
      }

      public static bool operator !=(VertexPosition left, VertexPosition right)
      {
         return !left.Equals(right);
      }

      public override string ToString()
      {
         return $"Position: {Position}";
      }
   }
}
