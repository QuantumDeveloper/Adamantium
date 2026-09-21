using System;
using System.Runtime.Serialization;

namespace Adamantium.Engine.Compiler.Models.ConversionUtils
{
   public class TopologyNotSupportedException : Exception
   {
      public TopologyNotSupportedException()
      {
      }

      public TopologyNotSupportedException(string message) : base(message)
      {
      }

      public TopologyNotSupportedException(string message, Exception innerException) : base(message, innerException)
      {
      }

      protected TopologyNotSupportedException(SerializationInfo info, StreamingContext context) : base(info, context)
      {
      }
   }
}
