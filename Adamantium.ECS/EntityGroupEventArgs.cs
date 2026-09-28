using System;

namespace Adamantium.ECS
{
   public class EntityGroupEventArgs:EventArgs
   {
      public EntityGroup Group { get; }

      public EntityGroupEventArgs(EntityGroup group)
      {
         Group = group;
      }
   }
}
