using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32.RawInput
{
   /// <summary>
   /// Value type for raw input from a HID.
   /// </summary>    
   [StructLayout(LayoutKind.Sequential)]
   public struct RawInputHid
   {
      /// <summary>Size of the HID data in bytes.</summary>
      public int Size;
      /// <summary>Number of HID in Data.</summary>
      public int Count;
      /// <summary>Data for the HID.</summary>
      public IntPtr Data;
   }
}
