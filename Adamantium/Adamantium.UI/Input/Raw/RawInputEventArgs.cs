using System;

namespace Adamantium.UI.Input.Raw
{
   internal class RawInputEventArgs:EventArgs
   {
      public RawInputEventArgs(InputModifiers modifiers, UInt32 timeStep)
      {
         InputModifiers = modifiers;
         Timestamp = timeStep;
      }

      public InputModifiers InputModifiers { get; private set; }
      public UInt32 Timestamp { get; private set; }
   }
}
