using System;

namespace Adamantium.Engine.Core.Content
{
   public class AssetLoadException:Exception
   {
      public AssetLoadException() { }

      public AssetLoadException(string message) : base(message) { }

      public AssetLoadException(string messsage, Exception innerException) :
         base(messsage, innerException)
      {
      }
   }
}
