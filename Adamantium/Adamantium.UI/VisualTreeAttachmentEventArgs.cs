using System;

namespace Adamantium.UI
{
   public class VisualTreeAttachmentEventArgs : EventArgs
   {
      IRootVisual Root { get; }

      public VisualTreeAttachmentEventArgs(IRootVisual root)
      {
         Root = root;
      }

   }
}
