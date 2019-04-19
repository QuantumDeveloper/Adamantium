namespace Adamantium.Win32.RawInput
{
   /// <summary>
   /// Enum provides available raw input device information commands 
   /// </summary>
   public enum RawInputDeviceInfoCommand:uint
   {
      /// <summary>
      /// pData points to a string that contains the device name. For this uiCommand only, the value in pcbSize is the character count (not the byte count).
      /// </summary>
      DeviceName = 0x20000007,

      /// <summary>
      /// pData points to an <see cref="DeviceInfo"/> structure.
      /// </summary>
      DeviceInfo = 0x2000000b,

      /// <summary>
      /// pData points to the previously parsed data.
      /// </summary>
      PreparsedData = 0x20000005
   }
}
