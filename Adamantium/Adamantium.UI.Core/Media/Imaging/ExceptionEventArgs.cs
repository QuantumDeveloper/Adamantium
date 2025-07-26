namespace Adamantium.UI.Core.Media.Imaging;

public class ExceptionEventArgs:EventArgs
{
   public Exception ErrorException { get; }

   public ExceptionEventArgs(Exception exception)
   {
      ErrorException = exception;
   }
}