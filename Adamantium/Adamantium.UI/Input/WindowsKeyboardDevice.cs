namespace Adamantium.UI.Input
{
   internal class WindowsKeyboardDevice:KeyboardDevice
   {
      public static WindowsKeyboardDevice Instance { get; } = new WindowsKeyboardDevice();

      private WindowsKeyboardDevice() { }
   }
}
