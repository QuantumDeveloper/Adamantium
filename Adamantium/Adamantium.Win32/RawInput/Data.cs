using System.Runtime.InteropServices;

namespace Adamantium.Win32.RawInput
{
   [StructLayout(LayoutKind.Explicit)]
   public struct Data
   {
      [FieldOffset(0)]
      public uint Buttons;
      /// <summary>
      /// If the mouse wheel is moved, this will contain the delta amount.
      /// </summary>
      [FieldOffset(2)]
      public ushort ButtonData;
      /// <summary>
      /// Flags for the event.
      /// </summary>
      [FieldOffset(0)]
      public RawMouseButtons ButtonFlags;
   }
}
