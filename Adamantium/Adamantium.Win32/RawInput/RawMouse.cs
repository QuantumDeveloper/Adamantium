using System.Runtime.InteropServices;

namespace Adamantium.Win32.RawInput
{
   /// <summary>
   /// Contains information about the state of the mouse.
   /// </summary>
   [StructLayout(LayoutKind.Sequential)]
   public struct RawMouse
   {
      /// <summary>
      /// The mouse state.
      /// </summary>
      public MouseMode Mode;

      /// <summary>
      /// Additional data
      /// </summary>
      public Data Data;

      /// <summary>
      /// Raw button data.
      /// </summary>
      public uint RawButtons;
      /// <summary>
      /// The motion in the X direction. This is signed relative motion or 
      /// absolute motion, depending on the value of usFlags. 
      /// </summary>
      public int LastX;
      /// <summary>
      /// The motion in the Y direction. This is signed relative motion or absolute motion, 
      /// depending on the value of usFlags. 
      /// </summary>
      public int LastY;
      /// <summary>
      /// The device-specific additional information for the event. 
      /// </summary>
      public uint ExtraInformation;
   }
}
