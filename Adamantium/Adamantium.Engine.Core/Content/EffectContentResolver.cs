using System;
using System.IO;

namespace Adamantium.Engine.Core.Content
{
   public class EffectContentResolver : IContentResolver
   {
        public static readonly string DefaultExtension = "fx";
      //public static readonly string DefaultExtension = "."+EffectData.CompiledExtension;

      public bool Exists(string assetPath)
      {
         return File.Exists(GetAssetPath(assetPath));
      }

      public String Resolve(string assetPath)
      {
         if (Exists(assetPath))
         {
            return GetAssetPath(assetPath);
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

      protected string GetAssetPath(string assetName)
      {
         if (string.IsNullOrEmpty(Path.GetExtension(assetName)))
         {
            assetName += DefaultExtension;
         }

         return NormalizeAssetPath(assetName);
      }
   }
}
