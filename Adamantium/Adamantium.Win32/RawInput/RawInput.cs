using System.Runtime.InteropServices;

namespace Adamantium.Win32.RawInput
{
   //For x86 && x64
   /// <summary>
   /// Value type for raw input.
   /// </summary>
   [StructLayout(LayoutKind.Sequential)]
   public struct RawInputData
   {
      /// <summary>Header for the data.</summary>
      public RawInputHeader Header;

      public Union Data;

      [StructLayout(LayoutKind.Explicit)]
      public struct Union
      {
         /// <summary>Mouse raw input data.</summary>
         [FieldOffset(0)]
         public RawMouse Mouse;
         /// <summary>Keyboard raw input data.</summary>
         [FieldOffset(0)]
         public RawKeyboard Keyboard;
         /// <summary>HID raw input data.</summary>
         [FieldOffset(0)]
         public RawInputHid Hid;
      }
   }
}
