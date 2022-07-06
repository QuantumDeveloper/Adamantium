using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls;

public abstract class Decorator : MeasurableUIComponent, IContainer
{
   public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
      typeof(Thickness), typeof(Decorator),
      new PropertyMetadata(default(Thickness),
         PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

   public static readonly AdamantiumProperty ChildProperty = AdamantiumProperty.Register(nameof(Child),
      typeof(UIComponent), typeof(Decorator),
      new PropertyMetadata(null,
         PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange, ChildChanged));

   private static void ChildChanged(AdamantiumComponent adamantiumObject, AdamantiumPropertyChangedEventArgs e)
   {
      if (adamantiumObject is Decorator o)
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

   public Thickness Padding
   {
      get => GetValue<Thickness>(PaddingProperty);
      set => SetValue(PaddingProperty, value);
   }

   [Content]
   public IUIComponent Child
   {
      get => GetValue<IUIComponent>(ChildProperty);
      set => SetValue(ChildProperty, value);
   }

   protected override Size MeasureOverride(Size availableSize)
   {
      var content = Child;
      var padding = Padding;

      if (content is IMeasurableComponent measurable)
      {
         measurable.Measure(availableSize.Deflate(padding));
         return measurable.DesiredSize.Inflate(padding);
      }

      return new Size(padding.Left + padding.Right, padding.Bottom + padding.Top);
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      if (Child is IMeasurableComponent measurable)
      {
         measurable.Arrange(new Rect(finalSize).Deflate(Padding));
      }

      return Child.Bounds.Size;
   }

   void IContainer.AddOrSetChildComponent(IMeasurableComponent component)
   {
      Child = component;
   }

   void IContainer.RemoveAllChildComponents()
   {
      throw new System.NotImplementedException();
   }
}