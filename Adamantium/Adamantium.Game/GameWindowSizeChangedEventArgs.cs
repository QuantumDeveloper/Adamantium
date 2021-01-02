using System;
using Adamantium.UI;

namespace Adamantium.Game
{
   public class GameWindowSizeChangedEventArgs:EventArgs
   {
      public Size Size { get; }

      public GameOutput Window { get; }

      public GameWindowSizeChangedEventArgs(GameOutput window, Size size)
      {
         Size = size;
         Window = window;
      }
   }
}
