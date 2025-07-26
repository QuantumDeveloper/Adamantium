namespace Adamantium.UI.Core.RoutedEvents;

public class LogicalTreeAttachmentEventArgs:EventArgs
{
   public IFundamentalUIComponent LogicalRoot { get; }

   public LogicalTreeAttachmentEventArgs(IFundamentalUIComponent logicalRoot)
   {
      LogicalRoot = logicalRoot;
   }
}