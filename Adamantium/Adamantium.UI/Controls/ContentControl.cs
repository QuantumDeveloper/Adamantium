namespace Adamantium.UI.Controls
{
   public class ContentControl:Control
   {
      public static readonly AdamantiumProperty ContentProperty = AdamantiumProperty.Register(nameof(Content),
         typeof(object), typeof(ContentControl), new PropertyMetadata(null, ContentChangedCallback));

      public object Content
      {
         get { return GetValue(ContentProperty); }
         set { SetValue(ContentProperty, value);}
      }


      private static void ContentChangedCallback(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
      {
         var o = adamantiumObject as ContentControl;
         if (o != null)
         {
            if (e.OldValue != null && e.OldValue != AdamantiumProperty.UnsetValue)
            {
               o.LogicalChildren.Remove((FrameworkComponent)e.OldValue);
               o.VisualChildrenCollection.Remove((FrameworkComponent)e.OldValue);
            }

            if (e.NewValue != null)
            {
               o.LogicalChildren.Add((FrameworkComponent)e.NewValue);
               o.VisualChildrenCollection.Add((FrameworkComponent)e.NewValue);
            }
         }
      }
   }
}
