namespace Adamantium.UI.Controls
{
   public abstract class Decorator:FrameworkComponent
   {
      public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
         typeof (Thickness), typeof (Decorator),
         new PropertyMetadata(default(Thickness),
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

      public static readonly AdamantiumProperty ChildProperty = AdamantiumProperty.Register(nameof(Child),
         typeof(UIComponent), typeof(Decorator),
         new PropertyMetadata(null,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange, ChildChanged));

      private static void ChildChanged(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
      {
         var o = adamantiumObject as Decorator;
         if (o != null)
         {
            if (e.OldValue != null && e.OldValue != AdamantiumProperty.UnsetValue)
            {
               o.LogicalChildren.Remove((FrameworkComponent)e.OldValue);
               o.VisualChildrenCollection.Remove((UIComponent)e.OldValue);
            }

            if (e.NewValue != null)
            {
               o.LogicalChildren.Add((FrameworkComponent)e.NewValue);
               o.VisualChildrenCollection.Add((UIComponent)e.NewValue);
            }
         }
      }

      public Thickness Padding
      {
         get { return GetValue<Thickness>(PaddingProperty); }
         set { SetValue(PaddingProperty, value);}
      }

      [Content]
      public UIComponent Child
      {
         get { return GetValue<UIComponent>(ChildProperty); }
         set { SetValue(ChildProperty, value);}
      }

      protected override Size MeasureOverride(Size availableSize)
      {
         var content = Child;
         var padding = Padding;

         if (content != null)
         {
            content.Measure(availableSize.Deflate(padding));
            return content.DesiredSize.Inflate(padding);
         }
         else
         {
            return new Size(padding.Left + padding.Right, padding.Bottom + padding.Top);
         }
      }

      protected override Size ArrangeOverride(Size finalSize)
      {
         Child?.Arrange(new Rect(finalSize).Deflate(Padding));
         return finalSize;
      }
   }
}
