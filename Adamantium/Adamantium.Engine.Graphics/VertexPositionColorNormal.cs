using System.Runtime.InteropServices;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
   [StructLayout(LayoutKind.Sequential)]
   public struct VertexPositionColorNormal
   {
      /// <summary>
      /// Create instace of <see cref="Adamantium.Engine.Graphics.VertexPositionColorNormal"/> 
      /// </summary>
      /// <param name="position"></param>
      /// <param name="normal"></param>
      /// <param name="color"></param>
      public VertexPositionColorNormal(Vector3F position, Vector3F normal, Color color)
      {
         Position = position;
         Normal = normal;
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
      /// The vertex color.
      /// </summary>
      [VertexInputElement("NORMAL")]
      public Vector3F Normal;

      /// <summary>
      /// Defines structure byte size.
      /// </summary>
      public static readonly int Size = Marshal.SizeOf<VertexPositionColorNormal>();

      public bool Equals(VertexPositionColorNormal other)
      {
         return Position.Equals(other.Position) && Color.Equals(other.Color) && Normal.Equals(other.Normal);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         return obj is VertexPositionColorNormal && Equals((VertexPositionColorNormal)obj);
      }

      public override int GetHashCode()
      {
         unchecked
         {
            int hashCode = Position.GetHashCode();
            hashCode = (hashCode * 397) ^ Normal.GetHashCode();
            hashCode = (hashCode * 397) ^ Color.GetHashCode();
            return hashCode;
         }
      }

      public static bool operator ==(VertexPositionColorNormal left, VertexPositionColorNormal right)
      {
         return left.Equals(right);
      }

      public static bool operator !=(VertexPositionColorNormal left, VertexPositionColorNormal right)
      {
         return !left.Equals(right);
      }

      public override string ToString()
      {
         return $"Position: {Position}, Normal {Normal}, Color: {Color}";
      }
   }
}
