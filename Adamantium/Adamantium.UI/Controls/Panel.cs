using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public abstract class Panel: InputUIComponent, IContainer
{
   public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background),
      typeof(Brush), typeof(Panel),
      new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

   public Brush Background
   {
      get => GetValue<Brush>(BackgroundProperty);
      set => SetValue(BackgroundProperty, value);
   }

   [Content]
   public MeasurableComponentsCollection Children { get; }

   protected Panel()
   {
      Children = new MeasurableComponentsCollection();
      Children.CollectionChanged += ChildrenChanged;
   }

   private void ChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
   {
      switch (e.Action)
      {
         case NotifyCollectionChangedAction.Add:
            var controls = e.NewItems.OfType<MeasurableUIComponent>();
            LogicalChildrenCollection.InsertRange(e.NewStartingIndex, controls);
            VisualChildrenCollection.AddRange(e.NewItems.OfType<IUIComponent>());
            break;
         case NotifyCollectionChangedAction.Remove:
            LogicalChildrenCollection.Remove(e.OldItems.OfType<MeasurableUIComponent>());
            VisualChildrenCollection.Remove(e.OldItems.OfType<IUIComponent>());
            break;
         case NotifyCollectionChangedAction.Replace:
            for (var i = 0; i < e.OldItems.Count; ++i)
            {
               var index = LogicalChildrenCollection.IndexOf((MeasurableUIComponent)e.OldItems[i]);
               var child = (MeasurableUIComponent)e.NewItems[i];
               LogicalChildrenCollection[index] = child;
               VisualChildrenCollection[index] = child;
            }
            break;

         case NotifyCollectionChangedAction.Reset:
            LogicalChildrenCollection.Clear();
            VisualChildrenCollection.Clear();
            break;
      }

      InvalidateMeasure();
   }

   protected override void OnRender(DrawingContext context)
   {
      context.BeginDraw(this);
      context.DrawRectangle(Background, new Rect(new Size(ActualWidth, ActualHeight)));
      context.EndDraw(this);
   }

   void IContainer.AddOrSetChildComponent(IMeasurableComponent component)
   {
      Children.Add(component);
   }

   void IContainer.RemoveAllChildComponents()
   {
      Children.Clear();
   }
}