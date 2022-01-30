using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls;

public abstract class Panel: FrameworkComponent, IContainer
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
   public UIElementCollection Children { get; }

   protected Panel()
   {
      Children = new UIElementCollection();
      Children.CollectionChanged += ChildrenChanged;
   }

   private void ChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
   {
      switch (e.Action)
      {
         case NotifyCollectionChangedAction.Add:
            var controls = e.NewItems.OfType<FrameworkComponent>();
            LogicalChildren.InsertRange(e.NewStartingIndex, controls);
            VisualChildrenCollection.AddRange(e.NewItems.OfType<IUIComponent>());
            break;
         case NotifyCollectionChangedAction.Remove:
            LogicalChildren.Remove(e.OldItems.OfType<FrameworkComponent>());
            VisualChildrenCollection.Remove(e.OldItems.OfType<IUIComponent>());
            break;
         case NotifyCollectionChangedAction.Replace:
            for (var i = 0; i < e.OldItems.Count; ++i)
            {
               var index = LogicalChildren.IndexOf((FrameworkComponent)e.OldItems[i]);
               var child = (FrameworkComponent)e.NewItems[i];
               LogicalChildren[index] = child;
               VisualChildrenCollection[index] = child;
            }
            break;

         case NotifyCollectionChangedAction.Reset:
            LogicalChildren.Clear();
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

   public void AddOrSetChildComponent(IUIComponent component)
   {
      Children.Add(component);
   }

   public void RemoveAllChildComponents()
   {
      Children.Clear();
   }
}