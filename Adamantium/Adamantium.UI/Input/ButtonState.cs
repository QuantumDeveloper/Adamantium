using System;

namespace Adamantium.UI.Input;

internal struct ButtonState
{
   public TimeSpan PressTime;
   public bool IsKeyAlreadyChecked;

   public ButtonState(TimeSpan pressTime)
   {
      PressTime = pressTime;
      IsKeyAlreadyChecked = false;
   }
}