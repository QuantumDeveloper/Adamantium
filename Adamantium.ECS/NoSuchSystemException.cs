using System;

namespace Adamantium.ECS
{
   public class NoSuchSystemException : Exception
   {
      public NoSuchSystemException()
      {
      }

      public NoSuchSystemException(string message) : base(message)
      {
      }

      public NoSuchSystemException(string message, Exception innerException) : base(message, innerException)
      {
      }
   }
}
