using System.Runtime.InteropServices;

namespace Adamantium.Win32.RawInput
{
   /// <summary>
   /// Value type for raw input from a keyboard.
   /// </summary>    
   [StructLayout(LayoutKind.Sequential)]
   public struct RawKeyboard
   {
      /// <summary>Scan code for key depression.</summary>
      public short MakeCode;
      /// <summary>Scan code information.</summary>
      public ScanCodeFlags Flags;
      /// <summary>Reserved.</summary>
      public short Reserved;
      /// <summary>Virtual key code.</summary>
      public uint VirtualKey;
      /// <summary>Corresponding window message.</summary>
      public WindowMessages Message;
      /// <summary>Extra information.</summary>
      public int ExtraInformation;
   }
}
