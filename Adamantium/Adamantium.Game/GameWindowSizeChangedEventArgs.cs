using System;

namespace Adamantium.Engine
{
   public class GameWindowSizeChangedEventArgs:EventArgs
   {
      public Size Size { get; }

      public GameWindow Window { get; }

      public GameWindowSizeChangedEventArgs(GameWindow window, Size2 size)
      {
         Size = size;
         Window = window;
      }
   }
}
