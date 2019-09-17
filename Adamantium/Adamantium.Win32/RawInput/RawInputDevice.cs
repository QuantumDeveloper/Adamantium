using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Core;

namespace Adamantium.Win32.RawInput
{
   /// <summary>
   /// Provide common service for registering <see cref="RawInputDevice"/>, get all registered devices and provice events for all raw input devices
   /// </summary>
   public static class RawInputDevice
   {
      /// <summary>
      /// Occurs when [keyboard input].
      /// </summary>
      public static event EventHandler<RawKeyboardInputEventArgs> KeyboardInput;

      /// <summary>
      /// Occurs when [mouse input].
      /// </summary>
      public static event EventHandler<RawMouseInputEventArgs> MouseInput;

      /// <summary>
      /// Occurs when [raw HID input].
      /// </summary>
      public static event EventHandler<RawHidInputEventArgs> HidInput;

        /// <summary>
        /// Registeres <see cref="InputDevice"/> and returns instance of that devie
        /// </summary>
        /// <param name="usagePage">A HID usage page</param>
        /// <param name="usageId">A HID usage id</param>
        /// <param name="flags">An input device flags</param>
        /// /// <returns>Registered <see cref="InputDevice"/></returns>
        public static InputDevice RegisterDevice(HIDUsagePage usagePage, HIDUsageId usageId, InputDeviceFlags flags)
      {
         return RegisterDevice(usagePage, usageId, flags, IntPtr.Zero);
      }

        /// <summary>
        /// Registeres <see cref="InputDevice"/> and returns instance of that devie
        /// </summary>
        /// <param name="usagePage">A HID usage page</param>
        /// <param name="usageId">A HID usage id</param>
        /// <param name="flags">An input device flags</param>
        /// <param name="target">Win32 window handle</param>
        /// <returns>Registered <see cref="InputDevice"/></returns>
        public static InputDevice RegisterDevice(HIDUsagePage usagePage, HIDUsageId usageId, InputDeviceFlags flags, IntPtr target)
      {
         InputDevice device = new InputDevice
         {
            UsagePage = usagePage,
            UsageId = usageId,
            Flags = flags,
            WindowHandle = target
         };
         Win32Interop.RegisterRawInputDevices(new[] { device }, 1, Marshal.SizeOf(device));
         return device;
      }

       /// <summary>
       /// Returns a list of registered devices
       /// </summary>
       public static List<DeviceInfo> GetDevices()
       {
           int deviceCount = 0;
           Win32Interop.GetRawInputDeviceList(null, ref deviceCount, Utilities.SizeOf<RawInputDeviceList>());
           if (deviceCount == 0)
           {
               return null;
           }

           var rawInputDeviceList = new RawInputDeviceList[deviceCount];
           Win32Interop.GetRawInputDeviceList(rawInputDeviceList, ref deviceCount, Utilities.SizeOf<RawInputDeviceList>());

           var deviceList = new List<DeviceInfo>();

           for (int i = 0; i < deviceCount; i++)
           {
               var deviceHandle = rawInputDeviceList[i].Device;

               int countDeviceNameChars = 0;
               Win32Interop.GetRawInputDeviceInfo(deviceHandle, RawInputDeviceInfoCommand.DeviceName, IntPtr.Zero,
                   ref countDeviceNameChars);
               IntPtr pData = Marshal.AllocHGlobal(countDeviceNameChars);
               Win32Interop.GetRawInputDeviceInfo(deviceHandle, RawInputDeviceInfoCommand.DeviceName, pData,
                   ref countDeviceNameChars);
               string name = Marshal.PtrToStringAnsi(pData);
               int structsize = Marshal.SizeOf(typeof (RawDeviceInfo));
               RawDeviceInfo rawDeviceInfo;
               rawDeviceInfo.Size = structsize;
               pData = Marshal.AllocHGlobal(structsize);
               Win32Interop.GetRawInputDeviceInfo(deviceHandle, RawInputDeviceInfoCommand.DeviceInfo, pData, ref structsize);
               rawDeviceInfo = (RawDeviceInfo) Marshal.PtrToStructure(pData, typeof (RawDeviceInfo));

               deviceList.Add(DeviceInfo.Convert(ref rawDeviceInfo, name, deviceHandle));
           }
           return deviceList;
       }

       /// <summary>
       /// Process raw input data
       /// </summary>
       /// <param name="rawInputMessagePointer">LParam from Win32 message</param>
       /// <param name="hwnd">Handler to the Win32 window</param>
       public static void HandleMessage(IntPtr rawInputMessagePointer, IntPtr hwnd)
       {
           // Get the size of the RawInput structure
           RawInputData inputData;
           int outSize = 0;
           int size = Marshal.SizeOf(typeof (RawInputData));
           outSize = Win32Interop.GetRawInputData(rawInputMessagePointer, RawInputCommand.Input, out inputData, ref size,
               Marshal.SizeOf(typeof (RawInputHeader)));
           if (outSize != -1)
           {
               switch (inputData.Header.DeviceType)
               {
                   case DeviceType.Mouse:
                       MouseInput?.Invoke(null,
                           new RawMouseInputEventArgs(ref inputData.Data.Mouse, inputData.Header.Device, hwnd));
                       break;

                   case DeviceType.Keyboard:
                       KeyboardInput?.Invoke(null,
                           new RawKeyboardInputEventArgs(ref inputData.Data.Keyboard, inputData.Header.Device, hwnd));
                       break;

                   case DeviceType.HID:
                       HidInput?.Invoke(null,
                           new RawHidInputEventArgs(ref inputData.Data.Hid, inputData.Header.Device, hwnd));
                       break;
               }
           }
       }
   }
}
