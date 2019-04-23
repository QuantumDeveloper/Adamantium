using System;

namespace Adamantium.Engine.Core.Content
{
   public class AssetNotFoundException:Exception
   {
      public AssetNotFoundException() { }

      public AssetNotFoundException(string message) : base(message) { }

      public AssetNotFoundException(string messsage, Exception innerException) :
         base(messsage, innerException)
      {
      }
   }
}
