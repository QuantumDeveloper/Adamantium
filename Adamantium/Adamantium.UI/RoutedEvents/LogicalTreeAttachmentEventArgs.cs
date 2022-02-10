using System;

namespace Adamantium.UI.RoutedEvents;

public class LogicalTreeAttachmentEventArgs:EventArgs
{
   public MeasurableComponent LogicalRoot { get; }

   public LogicalTreeAttachmentEventArgs(MeasurableComponent logicalRoot)
   {
      LogicalRoot = logicalRoot;
   }
}