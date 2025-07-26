namespace Adamantium.UI.Core.RoutedEvents;

public class VisualTreeAttachmentEventArgs : EventArgs
{
   public IUIComponent Component { get; }
   public IRootVisualComponent Root { get; }

   public VisualTreeAttachmentEventArgs(IRootVisualComponent root, IUIComponent component)
   {
      Root = root;
      Component = component;
   }

}