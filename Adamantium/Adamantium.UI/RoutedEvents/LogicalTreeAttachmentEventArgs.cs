using System;

namespace Adamantium.UI.RoutedEvents;

public class LogicalTreeAttachmentEventArgs:EventArgs
{
   public MeasurableUIComponent LogicalRoot { get; }

   public LogicalTreeAttachmentEventArgs(MeasurableUIComponent logicalRoot)
   {
      LogicalRoot = logicalRoot;
   }
}