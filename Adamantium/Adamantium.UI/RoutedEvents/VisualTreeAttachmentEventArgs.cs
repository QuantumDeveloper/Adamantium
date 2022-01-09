using System;

namespace Adamantium.UI.RoutedEvents;

public class VisualTreeAttachmentEventArgs : EventArgs
{
   IRootVisualComponent Root { get; }

   public VisualTreeAttachmentEventArgs(IRootVisualComponent root)
   {
      Root = root;
   }

}