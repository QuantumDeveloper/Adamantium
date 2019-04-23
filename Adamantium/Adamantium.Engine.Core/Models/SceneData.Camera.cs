using System;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class Camera
      {
         public String ID { get; set; }

         public String Name { get; set; }

         public Single Fov { get; set; }

         public Single AspectRatio { get; set; }

         public Single ZNear { get; set; }

         public Single ZFar { get; set; }

         public FovType FovType { get; set; }

         public CameraProjectionType ProjectionType { get; set; }

         public Vector3F Translation { get; set; }

         public QuaternionF Rotation { get; set; }
      }
   }
}
