using System;

namespace Adamantium.UI.RoutedEvents;

public class LogicalTreeAttachmentEventArgs:EventArgs
{
   public FrameworkComponent LogicalRoot { get; }

   public LogicalTreeAttachmentEventArgs(FrameworkComponent logicalRoot)
   {
      LogicalRoot = logicalRoot;
   }
}