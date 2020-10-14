using System;
using System.Runtime.InteropServices;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;

namespace Adamantium.Engine
{
   [StructLayout(LayoutKind.Sequential)]
   public struct SVPosition : IEquatable<SVPosition>
   {
      /// <summary>
      /// Initializes a new <see cref="SVPositionColor"/> instance.
      /// </summary>
      /// <param name="position">position of the vertex</param>
      public SVPosition(Vector4F position):this()
      {
         this.position = position;
      }
      /// <summary>
      /// XYZ position.
      /// </summary>
      [VertexInputElement("SV_Position")]
      public Vector4F position;

      /// <summary>
      /// Defines structure byte size.
      /// </summary>
      public static readonly int Size = Marshal.SizeOf<SVPosition>();

      public bool Equals(SVPosition other)
      {
         return position.Equals(other.position);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         return obj is SVPosition && Equals((SVPosition)obj);
      }

      public override int GetHashCode()
      {
         unchecked
         {
            int hashCode = position.GetHashCode();
            return hashCode;
         }
      }

      public static bool operator ==(SVPosition left, SVPosition right)
      {
         return left.Equals(right);
      }

      public static bool operator !=(SVPosition left, SVPosition right)
      {
         return !left.Equals(right);
      }

      public override string ToString()
      {
         return $"Position: {position}";
      }

   }
}
