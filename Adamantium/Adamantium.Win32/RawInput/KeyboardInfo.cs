using System;

namespace Adamantium.Win32.RawInput
{
   public class KeyboardInfo:DeviceInfo
   {
      public KeyboardInfo(ref RawDeviceInfo rawDeviceInfo, string deviceName, IntPtr deviceHandle) : base(ref rawDeviceInfo, deviceName, deviceHandle)
      {
         KeyboardType = rawDeviceInfo.Keyboard.Type;
         Subtype = rawDeviceInfo.Keyboard.SubType;
         KeyboardMode = rawDeviceInfo.Keyboard.KeyboardMode;
         FunctionKeyCount = rawDeviceInfo.Keyboard.NumberOfFunctionKeys;
         IndicatorCount = rawDeviceInfo.Keyboard.NumberOfIndicators;
         TotalKeyCount = rawDeviceInfo.Keyboard.NumberOfKeysTotal;
      }

      /// <summary>
      /// Gets or sets the type of the keyboard.
      /// </summary>
      /// <value>
      /// The type of the keyboard.
      /// </value>
      /// <unmanaged>unsigned int dwType</unmanaged>	
      public int KeyboardType { get; private set; }

      /// <summary>
      /// Gets or sets the subtype.
      /// </summary>
      /// <value>
      /// The subtype.
      /// </value>
      /// <unmanaged>unsigned int dwSubType</unmanaged>	
      public int Subtype { get; private set; }

      /// <summary>
      /// Gets or sets the keyboard mode.
      /// </summary>
      /// <value>
      /// The keyboard mode.
      /// </value>
      /// <unmanaged>unsigned int dwKeyboardMode</unmanaged>	
      public int KeyboardMode { get; private set; }

      /// <summary>
      /// Gets or sets the function key count.
      /// </summary>
      /// <value>
      /// The function key count.
      /// </value>
      /// <unmanaged>unsigned int dwNumberOfFunctionKeys</unmanaged>	
      public int FunctionKeyCount { get; private set; }

      /// <summary>
      /// Gets or sets the indicator count.
      /// </summary>
      /// <value>
      /// The indicator count.
      /// </value>
      /// <unmanaged>unsigned int dwNumberOfIndicators</unmanaged>	
      public int IndicatorCount { get; private set; }

      /// <summary>
      /// Gets or sets the total key count.
      /// </summary>
      /// <value>
      /// The total key count.
      /// </value>
      /// <unmanaged>unsigned int dwNumberOfKeysTotal</unmanaged>	
      public int TotalKeyCount { get; private set; }
   }
}
