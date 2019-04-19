using System;
using System.Runtime.InteropServices;

namespace Adamantium.Win32.RawInput
{
   [StructLayout((LayoutKind.Sequential))]
   public struct InputDevice
   {
      /// <summary>Top level collection Usage page for the raw input device.</summary>
      public HIDUsagePage UsagePage;
      /// <summary>Top level collection Usage for the raw input device. </summary>
      public HIDUsageId UsageId;
      /// <summary>Mode flag that specifies how to interpret the information provided by UsagePage and Usage.</summary>
      public InputDeviceFlags Flags;
      /// <summary>Handle to the target device. If NULL, it follows the keyboard focus.</summary>
      public IntPtr WindowHandle;
   }
}
