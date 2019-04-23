using System;

namespace Adamantium.EntityFramework
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
