using System;

namespace Adamantium.Win32.RawInput
{
   public class HidInfo:DeviceInfo
   {
      public HidInfo(ref RawDeviceInfo rawDeviceInfo, string deviceName, IntPtr deviceHandle) : base(ref rawDeviceInfo, deviceName, deviceHandle)
      {
         VendorId = rawDeviceInfo.Hid.VendorId;
         ProductId = rawDeviceInfo.Hid.ProductId;
         VersionNumber = rawDeviceInfo.Hid.VersionNumber;
         UsagePage = (HIDUsagePage)rawDeviceInfo.Hid.UsagePage;
         UsageId = (HIDUsageId) rawDeviceInfo.Hid.Usage;
      }

      public int VendorId { get; private set; }

      public int ProductId { get; private set; }

      public int VersionNumber { get; private set; }

      /// <summary>
      /// Gets or sets the usage page.
      /// </summary>
      /// <value>
      /// The usage page.
      /// </value>
      /// <unmanaged>HID_USAGE_PAGE usUsagePage</unmanaged>
      public HIDUsagePage UsagePage { get; private set; }

      /// <summary>
      /// Gets or sets the usage.
      /// </summary>
      /// <value>
      /// The usage.
      /// </value>
      /// <unmanaged>HID_USAGE_ID usUsage</unmanaged>
      public HIDUsageId UsageId { get; private set; }
   }
}
