using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class ImageCollection : Dictionary<String, Image>
      {
         public ImageCollection()
         {
         }

         public ImageCollection(ImageCollection images):base(images)
         { }
      }
   }
}
