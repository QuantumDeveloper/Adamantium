using System;

namespace Adamantium.Engine.Core.Content
{
   public interface IContentResolver
   {
      bool Exists(String assetPath);

      String Resolve(String assetPath);

   }
}
