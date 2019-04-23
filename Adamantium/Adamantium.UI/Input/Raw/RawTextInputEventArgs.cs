using System;

namespace Adamantium.UI.Input.Raw
{
   class RawTextInputEventArgs:RawInputEventArgs
   {
      public string Text { get; }

      public RawTextInputEventArgs(String text, InputModifiers modifiers, uint timeStep) : base(modifiers, timeStep)
      {
         Text = text;
      }
   }
}
