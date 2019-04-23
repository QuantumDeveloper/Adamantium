using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      /// <summary>
      /// Class containing all needed information for animation
      /// <remarks>Inherits frame information for each joint</remarks>
      /// </summary>
      public class AnimationCollection:Dictionary<String, FrameCollection>
      {
         /// <summary>
         /// Default AnimationData constructor
         /// </summary>
         public AnimationCollection()
         {}

         /// <summary>
         /// Copy constructor for AnimationData class
         /// </summary>
         /// <param name="copy"></param>
         public AnimationCollection(AnimationCollection copy):base(copy)
         {}
      }
   }
}
