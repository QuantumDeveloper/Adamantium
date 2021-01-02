using System;

namespace Adamantium.Game
{

   public class GameWindowParametersEventArgs:EventArgs
   {
      public GameOutput Window { get; }
      public ChangeReason Reason { get; }

      public GameWindowParametersEventArgs(GameOutput window, ChangeReason reason)
      {
         Window = window;
         Reason = reason;
      }
   }
}
