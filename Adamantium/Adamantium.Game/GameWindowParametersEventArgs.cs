using System;

namespace Adamantium.Engine
{

   public class GameWindowParametersEventArgs:EventArgs
   {
      public GameWindow Window { get; }
      public ChangeReason Reason { get; }

      public GameWindowParametersEventArgs(GameWindow window, ChangeReason reason)
      {
         Window = window;
         Reason = reason;
      }
   }
}
