using System;
using System.Collections.Generic;

namespace Adamantium.Graphics.Core.Models
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
