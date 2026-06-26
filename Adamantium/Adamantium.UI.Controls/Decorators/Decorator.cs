using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Decorators;

public abstract class Decorator : MeasurableUIComponent, IContainer
{
   public static readonly AdamantiumProperty PaddingProperty = AdamantiumProperty.Register(nameof(Padding),
      typeof(Thickness), typeof(Decorator),
      new PropertyMetadata(default(Thickness),
         PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange));

   public static readonly AdamantiumProperty ChildProperty = AdamantiumProperty.Register(nameof(Child),
      typeof(IMeasurableComponent), typeof(Decorator),
      new PropertyMetadata(null,
         PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange, ChildChanged));

   private static void ChildChanged(AdamantiumComponent adamantiumAdamantiumComponent, AdamantiumPropertyChangedEventArgs e)
   {
      if (adamantiumAdamantiumComponent is Decorator o)
      {
         if (e.OldValue != null && e.OldValue != AdamantiumProperty.UnsetValue)
         {
            o.LogicalChildrenCollection.Remove((IMeasurableComponent)e.OldValue);
            o.VisualChildrenCollection.Remove((IMeasurableComponent)e.OldValue);
         }

         if (e.NewValue != null)
         {
            o.LogicalChildrenCollection.Add((IMeasurableComponent)e.NewValue);
            o.VisualChildrenCollection.Add((IMeasurableComponent)e.NewValue);
         }
      }
   }

   public Thickness Padding
   {
      get => GetValue<Thickness>(PaddingProperty);
      set => SetValue(PaddingProperty, value);
   }

   [Content]
   public IMeasurableComponent Child
   {
      get => GetValue<IMeasurableComponent>(ChildProperty);
      set => SetValue(ChildProperty, value);
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

      return new Size(padding.Left + padding.Right, padding.Bottom + padding.Top);
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      // Fill the arranged slot and lay the child out inside it (minus padding); the child positions itself within via its
      // own alignment. Returning the CHILD's size instead collapsed a stretched decorator down to a non-stretch child -
      // e.g. a Button's border shrank to its centred ContentPresenter and the text spilled outside. Shrink-to-content is
      // a MEASURE concern (MeasureOverride already returns child+padding), not arrange.
      Child?.Arrange(new Rect(finalSize).Deflate(Padding));
      return finalSize;
   }

   public void AddOrSetChildComponent(object component)
   {
      if (component is IMeasurableComponent uiComponent)
      {
         Child = uiComponent;
      }
   }

   public void RemoveAllChildComponents()
   {
      Child = null;
   }

   public IReadOnlyList<object> GetChildComponents() =>
      Child != null ? new object[] { Child } : System.Array.Empty<object>();

   public void InsertChildComponent(int index, object component)
   {
      if (component is IMeasurableComponent measurable) Child = measurable;
   }

   public void RemoveChildComponentAt(int index) => Child = null;
}