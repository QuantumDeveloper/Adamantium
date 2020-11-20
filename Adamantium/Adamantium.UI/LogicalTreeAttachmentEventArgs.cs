using System;

namespace Adamantium.UI
{
   public class LogicalTreeAttachmentEventArgs:EventArgs
   {
      public FrameworkComponent LogicalRoot { get; }

      public LogicalTreeAttachmentEventArgs(FrameworkComponent logicalRoot)
      {
         LogicalRoot = logicalRoot;
      }
   }
}
