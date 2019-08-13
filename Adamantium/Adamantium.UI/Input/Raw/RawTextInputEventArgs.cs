using System;

namespace Adamantium.UI.Input.Raw
{
   public class RawTextInputEventArgs:RawInputEventArgs
   {
      public string Text { get; }

      public RawTextInputEventArgs(String text, InputModifiers modifiers, uint timeStep) : base(modifiers, timeStep)
      {
         Text = text;
      }
   }
}
