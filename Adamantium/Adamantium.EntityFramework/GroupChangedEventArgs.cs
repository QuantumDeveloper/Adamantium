using System;

namespace Adamantium.EntityFramework
{
   public class GroupChangedEventArgs:EventArgs
   {
      public Entity[] Entities { get; }

      public Entity[] PreviousEntities { get; }

      public GroupState GroupState { get; }

      public GroupChangedEventArgs(GroupState state, Entity[] previous, params Entity[] current)
      {
         GroupState = state;
         PreviousEntities = previous;
         Entities = current;
      }
   }
}
