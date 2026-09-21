using System;
using System.IO;

namespace Adamantium.Game.Core
{
   public abstract class GamePlatformDesktop : GamePlatform
   {
      public GamePlatformDesktop(IGame game) : base(game)
      {
      }

      public override string DefaultAppDirectory
      {
         get
         {
            var assemblyUri = new Uri(Game.GetType().Assembly.CodeBase);
            return Path.GetDirectoryName(assemblyUri.LocalPath);
         }
      }
   }
}
