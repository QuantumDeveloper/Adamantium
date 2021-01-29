using System;
using System.IO;

namespace Adamantium.Game
{
   public abstract class GamePlatformDesktop : GamePlatform
   {
      public GamePlatformDesktop(GameBase gameBase) : base(gameBase)
      {
      }

      public override string DefaultAppDirectory
      {
         get
         {
            var assemblyUri = new Uri(gameBase.GetType().Assembly.CodeBase);
            return Path.GetDirectoryName(assemblyUri.LocalPath);
         }
      }
   }
}
