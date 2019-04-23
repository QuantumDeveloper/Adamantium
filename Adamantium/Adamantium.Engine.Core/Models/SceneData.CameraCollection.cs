using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class CameraCollection : Dictionary<String, SceneData.Camera>
      {
         public CameraCollection()
         {
         }

         public CameraCollection(CameraCollection cameras) : base(cameras)
         {
         }
      }
   }
}
