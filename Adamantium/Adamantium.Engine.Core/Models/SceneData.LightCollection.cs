using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class LightCollection : Dictionary<String, SceneData.Light>
      {
         public LightCollection()
         { }

         public LightCollection(LightCollection collection):base(collection)
         { }

      }
   }
}
