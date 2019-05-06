using System.Runtime.InteropServices;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
   [StructLayout(LayoutKind.Sequential)]
   public struct VertexPositionColorNormalTexture
   {
      /// <summary>
      /// Create instace of <see cref="VertexPositionColorNormalTexture"/> 
      /// </summary>
      /// <param name="position"></param>
      /// <param name="textureCoordinate"></param>
      /// <param name="normal"></param>
      /// <param name="color"></param>
      public VertexPositionColorNormalTexture(Vector3F position, ColorRGBA color, Vector3F normal, Vector2F textureCoordinate)
      {
         Position = position;
         UV = textureCoordinate;
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
      public ColorRGBA Color;


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
      public static readonly int Size = Marshal.SizeOf<VertexPositionColorNormalTexture>();

      public bool Equals(VertexPositionColorNormalTexture other)
      {
         return Position.Equals(other.Position) && UV.Equals(other.UV)
            && Color.Equals(other.Color) && Normal.Equals(other.Normal);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         return obj is VertexPositionColorNormalTexture && Equals((VertexPositionColorNormalTexture)obj);
      }

      public override int GetHashCode()
      {
         unchecked
         {
            int hashCode = Position.GetHashCode();
            hashCode = (hashCode * 397) ^ Normal.GetHashCode();
            hashCode = (hashCode * 397) ^ UV.GetHashCode();
            hashCode = (hashCode * 397) ^ Color.GetHashCode();
            return hashCode;
         }
      }

      public static bool operator ==(VertexPositionColorNormalTexture left, VertexPositionColorNormalTexture right)
      {
         return left.Equals(right);
      }

      public static bool operator !=(VertexPositionColorNormalTexture left, VertexPositionColorNormalTexture right)
      {
         return !left.Equals(right);
      }

      public override string ToString()
      {
         return $"Position: {Position}, Normal {Normal}, UV: {UV}, Color: {Color}";
      }
   }
}
