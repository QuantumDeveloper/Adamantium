using System;
using System.Runtime.Serialization;

namespace Adamantium.EntityFramework
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

      protected NoSuchSystemException(SerializationInfo info, StreamingContext context) : base(info, context)
      {
      }
   }
}
