using System;

namespace Adamantium.Engine
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
