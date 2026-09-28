using System;

namespace Adamantium.Win32
{
   /// <summary>
   /// Specifies how messages are to be handled by PeekMessage()
   /// </summary>
   [Flags]
   public enum PeekMessageFlag : uint
   {
      /// <summary>
      /// Messages are not removed from the queue after processing by PeekMessage.
      /// </summary>
      NoRemove,

      /// <summary>
      /// Messages are removed from the queue after processing by PeekMessage.
      /// </summary>
      Remove,

      /// <summary>
      /// Prevents the system from releasing any thread that is waiting for the caller to go idle (see WaitForInputIdle).
      /// Combine this value with either NoRemove or Remove.
      /// </summary>
      NoYield
   }
}
