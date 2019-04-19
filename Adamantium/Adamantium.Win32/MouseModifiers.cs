using System;

namespace Adamantium.Win32
{
   /// <summary>
   /// Virtual Mouse Key codes
   /// </summary>
   [Flags]
   public enum MouseModifiers : uint
   {
      /// <summary>
      /// The CTRL key is down.
      /// </summary>
      Control = 0x0008,

      /// <summary>
      /// Left mouse button
      /// </summary>
      LeftButton = 0x0001,

      /// <summary>
      /// Right mouse button
      /// </summary>
      RightButton = 0x0002,

      /// <summary>
      /// Middle mouse button (three-button mouse)
      /// </summary>
      MiddleButton = 0x0010,

      /// <summary>
      /// X1 mouse button
      /// </summary>
      XButton1 = 0x0020,

      /// <summary>
      /// X2 mouse button
      /// </summary>
      XButton2 = 0x0040,

   }
}
