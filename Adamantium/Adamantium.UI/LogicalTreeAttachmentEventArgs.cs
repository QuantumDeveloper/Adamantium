using System;

namespace Adamantium.UI
{
   public class LogicalTreeAttachmentEventArgs:EventArgs
   {
      public FrameworkElement LogicalRoot { get; }

      public LogicalTreeAttachmentEventArgs(FrameworkElement logicalRoot)
      {
         LogicalRoot = logicalRoot;
      }
   }
}
