using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      /// <summary>
      /// All data needed for animation per one joint
      /// </summary>
      public class FrameCollection:List<KeyFrame>
      {
         public String JointId { get; set; }
         public String FullName => ControllerId + JointId;
         public String ControllerId { get; set; }

         public FrameCollection()
         {
            JointId = String.Empty;
            ControllerId = String.Empty;
         }

      }
   }
}
