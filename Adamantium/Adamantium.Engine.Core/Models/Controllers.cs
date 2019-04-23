using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Common.Models
{
   public partial class SceneData
   {
      public class Controllers:Dictionary<String, Core.Models.SceneData.Controller>
      {
         public Controllers()
         { }

         public Controllers(Controllers controllers):base(controllers)
         {
            
         }
      }
   }
   
}
