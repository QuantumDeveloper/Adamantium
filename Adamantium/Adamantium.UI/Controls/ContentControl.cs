using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public class ContentControl:Control
{
   public static readonly AdamantiumProperty ContentProperty = AdamantiumProperty.Register(nameof(Content),
      typeof(object), typeof(ContentControl), new PropertyMetadata(null, ContentChangedCallback));

   public object Content
   {
      get => GetValue(ContentProperty);
      set => SetValue(ContentProperty, value);
   }


   private static void ContentChangedCallback(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
   {
      if (adamantiumObject is ContentControl o)
      {
         if (e.OldValue != null && e.OldValue != AdamantiumProperty.UnsetValue)
         {
            o.LogicalChildrenCollection.Remove((MeasurableComponent)e.OldValue);
         }

         if (e.NewValue != null)
         {
            o.LogicalChildrenCollection.Add((MeasurableComponent)e.NewValue);
         }
      }
   }
}