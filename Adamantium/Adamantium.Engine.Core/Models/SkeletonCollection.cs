using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class SkeletonCollection : Dictionary<String, List<SceneData.Joint>>
      {
         public SkeletonCollection():base()
         { }

         public SkeletonCollection(SkeletonCollection collection) : base(collection)
         {
            
         }
      }
   }
}
