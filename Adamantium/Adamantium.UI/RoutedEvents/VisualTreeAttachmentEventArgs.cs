using System;

namespace Adamantium.UI
{
   public class VisualTreeAttachmentEventArgs : EventArgs
   {
      IRootVisualComponent Root { get; }

      public VisualTreeAttachmentEventArgs(IRootVisualComponent root)
      {
         Root = root;
      }

   }
}
