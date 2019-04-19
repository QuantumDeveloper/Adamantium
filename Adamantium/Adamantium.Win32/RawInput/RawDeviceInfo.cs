using System.Runtime.InteropServices;

namespace Adamantium.Win32.RawInput
{
   [StructLayout(LayoutKind.Explicit)]
   public struct RawDeviceInfo
   {
      /// <summary>
      /// The size, in bytes, of the RID_DEVICE_INFO structure. 
      /// </summary>
      [FieldOffset(0)]
      public int Size;
      /// <summary>
      /// The type of raw input data.
      /// </summary>
      [FieldOffset(4)]
      public DeviceType DeviceType;
      /// <summary>
      /// If dwType is RIM_TYPEMOUSE, this is the RID_DEVICE_INFO_MOUSE structure that defines the mouse. 
      /// </summary>
      [FieldOffset(8)]
      public RawDeviceInfoMouse Mouse;
      /// <summary>
      /// If dwType is RIM_TYPEKEYBOARD, this is the RID_DEVICE_INFO_KEYBOARD structure that defines the keyboard. 
      /// </summary>
      [FieldOffset(8)]
      public RawDeviceInfoKeyboard Keyboard;
      /// <summary>
      /// If dwType is RIM_TYPEHID, this is the RID_DEVICE_INFO_HID structure that defines the HID device. 
      /// </summary>
      [FieldOffset(8)]
      public RawDeviceInfoHid Hid;
   }
}
