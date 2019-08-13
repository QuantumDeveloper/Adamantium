namespace Adamantium.UI.Input
{
   public class WindowsMouseDevice:MouseDevice
   {
      public static MouseDevice Instance { get; } = new WindowsMouseDevice();

      private WindowsMouseDevice()
      { }
   }
}
