using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adamantium.Mathematics
{
   public struct BoundingCylinder
   {
      public float Radius;

      public Vector3F Center;

      public Vector3F HalfExtent;

      public float Length => Center.Y + HalfExtent.Y;

      public QuaternionF Rotation;
   }
}
