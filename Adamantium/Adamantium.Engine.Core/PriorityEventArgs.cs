using System;

namespace Adamantium.Engine.Core
{
   public class PriorityEventArgs : EventArgs
   {
      public int PreviousPriority { get; }

      public int CurrentPriority { get; }

      public PriorityEventArgs(int previousPriority, int currentPriority)
      {
         PreviousPriority = previousPriority;
         CurrentPriority = currentPriority;
      }
   }
}
