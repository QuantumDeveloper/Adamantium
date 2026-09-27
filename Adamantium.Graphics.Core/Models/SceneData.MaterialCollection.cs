using System;
using System.Collections.Generic;

namespace Adamantium.Graphics.Core.Models
{
   public partial class SceneData
   {
      public class MaterialCollection:Dictionary<String, Material>
      {
         public MaterialCollection()
         { }

         public MaterialCollection(MaterialCollection collection):base(collection)
         { }
      }
   }
}
