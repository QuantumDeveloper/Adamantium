using System;

namespace Adamantium.Win32.RawInput
{
   [Flags]
   public enum MouseMode : ushort
   {
      /// <summary>
      /// Mouse attributes changed; application needs to query the mouse attributes.
      /// </summary>
      AttributesChanged = 0x04,

      /// <summary>
      /// Mouse movement data is relative to the last mouse position.
      /// </summary>
      MoveRelative = 0,

      /// <summary>
      /// Mouse movement data is based on absolute position.
      /// </summary>
      MoveAbsolute = 1,

      /// <summary>
      /// Mouse coordinates are mapped to the virtual desktop (for a multiple monitor system)
      /// </summary>
      VirtualDesktop = 0x02
   }
}
