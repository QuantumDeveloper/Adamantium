using System;

namespace Adamantium.Win32.RawInput
{
   public class MouseInfo:DeviceInfo
   {
      public MouseInfo(ref RawDeviceInfo rawDeviceInfo, string deviceName, IntPtr deviceHandle) : base(ref rawDeviceInfo, deviceName, deviceHandle)
      {
         Id = rawDeviceInfo.Mouse.Id;
         ButtonCount = rawDeviceInfo.Mouse.NumberOfButtons;
         SampleRate = rawDeviceInfo.Mouse.SampleRate;
         HasHorizontalWheel = rawDeviceInfo.Mouse.HasHorizontalWheel;

      }

      public int Id { get; private set; }

      public int ButtonCount { get; private set; }

      public int SampleRate { get; private set; }

      public bool HasHorizontalWheel { get; private set; }

   }
}
