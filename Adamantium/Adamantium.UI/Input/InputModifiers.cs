using System;

namespace Adamantium.UI.Input
{
   [Flags]
   public enum InputModifiers
   {
      None = 0,
      LeftAlt = 1,
      RightAlt = 2,
      LeftControl = 4,
      RightControl = 8,
      LeftShift = 16,
      RightShift = 32,
      LeftWindows = 64,
      RightWindows = 128,
      LeftMouseButton = 256,
      RightMouseButton = 512,
      MiddleMouseButton = 1024,
      X1MouseButton = 2048,
      X2MouseButton = 4096
   }
}
