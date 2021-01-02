using System;

namespace Adamantium.Game
{
   public class GameWindowEventArgs:EventArgs
   {
      public GameOutput Window { get; }

      public GameWindowEventArgs(GameOutput window)
      {
         Window = window;
      }

   }
}
