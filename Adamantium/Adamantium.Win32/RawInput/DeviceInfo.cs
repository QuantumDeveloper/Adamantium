using System;

namespace Adamantium.Win32.RawInput
{
   /// <summary>
   /// Raw input device info
   /// </summary>
   public class DeviceInfo
   {
      internal DeviceInfo(ref RawDeviceInfo rawDeviceInfo, string deviceName, IntPtr deviceHandle)
      {
         DeviceName = deviceName;
         Handle = deviceHandle;
         DeviceType = (DeviceType)rawDeviceInfo.DeviceType;
      }

      /// <summary>
      /// Device name
      /// </summary>
      public string DeviceName { get; set; }

      /// <summary>
      /// Device handle
      /// </summary>
      public IntPtr Handle { get; set; }

      /// <summary>
      /// Raw input device type
      /// </summary>
      public DeviceType DeviceType { get; set; }

      internal static DeviceInfo Convert(ref RawDeviceInfo rawDeviceInfo, string deviceName, IntPtr deviceHandle)
      {
         DeviceInfo info;
         switch (rawDeviceInfo.DeviceType)
         {
            case DeviceType.HID:
               info = new HidInfo(ref rawDeviceInfo, deviceName, deviceHandle);
               break;
            case DeviceType.Mouse:
               info = new MouseInfo(ref rawDeviceInfo, deviceName, deviceHandle);
               break;
            case DeviceType.Keyboard:
               info = new KeyboardInfo(ref rawDeviceInfo, deviceName, deviceHandle);
               break;
            default:
               throw new InvalidOperationException($"Unsupported device type {rawDeviceInfo.DeviceType}");
         }
         return info;
      }
   }
}
