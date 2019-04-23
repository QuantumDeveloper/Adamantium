using System;
using System.IO;

namespace Adamantium.Engine.Core.Content
{
   public class FileSystemContentResolver: IContentResolver
   {
      public bool Exists(string assetPath)
      {
         return File.Exists(NormalizeAssetPath(assetPath));
      }

      public String Resolve(string assetPath)
      {
         if (Exists(assetPath))
         {
            return NormalizeAssetPath(assetPath);
         }
         else
         {
            return null;
         }
      }

      private String NormalizeAssetPath(String path)
      {
         return path.Replace("/", "\\");
      }
   }
}
