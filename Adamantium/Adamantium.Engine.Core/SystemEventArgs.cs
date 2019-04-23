using System;

namespace Adamantium.Engine.Core
{
   public class SystemEventArgs : EventArgs
   {
      public ISystem System { get; }

      public SystemEventArgs(ISystem system)
      {
         System = system;
      }
   }

   public class ExecutionTypeEventArgs : EventArgs
   {
      public ExecutionType PreviousExecutionType { get; }

      public ExecutionType CurrentExecutionType { get; }

      public ExecutionTypeEventArgs(ExecutionType previousExecutionType, ExecutionType currentExecutionType)
      {
         PreviousExecutionType = previousExecutionType;
         CurrentExecutionType = currentExecutionType;
      }
   }

   public class StateEventArgs : EventArgs
   {
      public bool State { get; }

      public StateEventArgs(bool state)
      {
         State = state;
      }
   }
}
