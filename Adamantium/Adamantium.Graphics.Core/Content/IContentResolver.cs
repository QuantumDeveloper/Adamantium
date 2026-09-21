using System;

namespace Adamantium.Graphics.Core.Content
{
   public interface IContentResolver
   {
      bool Exists(String assetPath);

      String Resolve(String assetPath);

   }
}
