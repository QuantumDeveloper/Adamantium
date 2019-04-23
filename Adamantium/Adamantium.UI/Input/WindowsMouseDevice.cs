namespace Adamantium.UI.Input
{
   internal class WindowsMouseDevice:MouseDevice
   {
      public static WindowsMouseDevice Instance { get; } = new WindowsMouseDevice();

      private WindowsMouseDevice()
      { }
   }
}
