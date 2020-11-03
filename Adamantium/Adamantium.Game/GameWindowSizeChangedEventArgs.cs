using System;
using Adamantium.UI;

namespace Adamantium.Game
{
   public class GameWindowSizeChangedEventArgs:EventArgs
   {
      public Size Size { get; }

      public GameWindow Window { get; }

      public GameWindowSizeChangedEventArgs(GameWindow window, Size size)
      {
         Size = size;
         Window = window;
      }
   }
}
