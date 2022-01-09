using System;

namespace Adamantium.UI.Media.Imaging;

public class ExceptionEventArgs:EventArgs
{
   public Exception ErrorException { get; }

   public ExceptionEventArgs(Exception exception)
   {
      ErrorException = exception;
   }
}