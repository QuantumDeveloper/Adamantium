using System;

namespace Adamantium.Engine.GameInput
{
   /// <summary>
   /// Describes possible button states
   /// </summary>
   [Flags]
   public enum ButtonStateFlags: byte
   {
      /// <summary>
      /// Button has no state
      /// </summary>
      None = 0,

      /// <summary>
      /// Button is down
      /// </summary>
      Down = 1,

      /// <summary>
      /// Button in pressed in first time
      /// </summary>
      Pressed = 2,

      /// <summary>
      /// Button released
      /// </summary>
      Released = 4
   }
}
