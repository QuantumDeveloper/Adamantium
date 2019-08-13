namespace Adamantium.UI.Input.Raw
{
   public enum RawMouseEventType : uint
   {
      MouseMove = 0x0200,
      EnterWindow,
      LeaveWindow = 0x02A3,
      LeftButtonDown = 0x0201,
      LeftButtonUp = 0x0202,
      RightButtonDown = 0x0204,
      RightButtonUp = 0x0205,
      MiddleButtonDown = 0x0207,
      MiddleButtonUp = 0x0208,
      LeftButtonDoubleClick = 0x0203,
      RightButtonDoubleClick = 0x0206,
      MiddleButtonDoubleClick = 0x0209,
      MouseWheel = 0x020A,
      X1ButtonDown,
      X1ButtonUp,
      X2ButtonDown,
      X2ButtonUp,
      RawMouseMove,
      RawLeftButtonDown,
      RawRightButtonDown,
      RawMiddleButtonDown,
      RawLeftButtonUp,
      RawRightButtonUp,
      RawMiddleButtonUp
   }
}
