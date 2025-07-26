using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

public class ContentControl : Control, IContentControl
{
   public static readonly AdamantiumProperty ContentProperty = AdamantiumProperty.Register(nameof(Content),
      typeof(object), typeof(ContentControl), new PropertyMetadata(null, ContentChangedCallback));

   [Content]
   public object Content
   {
      get => GetValue(ContentProperty);
      set => SetValue(ContentProperty, value);
   }

   private static void ContentChangedCallback(AdamantiumComponent adamantiumAdamantiumComponent,
      AdamantiumPropertyChangedEventArgs e)
   {
      if (adamantiumAdamantiumComponent is ContentControl o)
      {
         if (e.OldValue != null && e.OldValue != AdamantiumProperty.UnsetValue)
         {
            o.LogicalChildrenCollection.Remove((IUIComponent)e.OldValue);
            o.VisualChildrenCollection.Remove((IUIComponent)e.OldValue);
         }

         if (e.NewValue != null)
         {
            o.LogicalChildrenCollection.Add((IUIComponent)e.NewValue);
            o.VisualChildrenCollection.Add((IUIComponent)e.NewValue);
         }
      }
   }

   void IContainer.AddOrSetChildComponent(object component)
   {
      Content = component;
   }

   void IContainer.RemoveAllChildComponents()
   {
      Content = null;
   }
}