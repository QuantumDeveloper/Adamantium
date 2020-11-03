using System;

namespace Adamantium.Game
{
   public class GameWindowEventArgs:EventArgs
   {
      public GameWindow Window { get; }

      public GameWindowEventArgs(GameWindow window)
      {
         Window = window;
      }

   }
}
