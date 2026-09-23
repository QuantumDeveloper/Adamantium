using System;
using System.IO;

namespace Adamantium.Game.Core
{
   public abstract class UniversePlatformDesktop : UniversePlatform
   {
      public UniversePlatformDesktop(IUniverse universe) : base(universe)
      {
      }

      public override string DefaultAppDirectory
      {
         get
         {
            var assemblyUri = new Uri(Universe.GetType().Assembly.CodeBase);
            return Path.GetDirectoryName(assemblyUri.LocalPath);
         }
      }
   }
}
