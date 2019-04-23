using System;

namespace Adamantium.UI
{
   public class UnhandledExceptionEventArgs:EventArgs
   {
      public Exception Exception { get; }

      public UnhandledExceptionEventArgs(Exception exception)
      {
         Exception = exception;
      }
   }
}
