using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32.RawInput
{
   [StructLayout(LayoutKind.Sequential)]
   public struct RawInputDeviceList
   {
      public IntPtr Device;
      public DeviceType DeviceType;
   }
}
