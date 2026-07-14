using System.Collections.Specialized;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls.Panels;

public abstract class Panel: InputUIComponent, IContainer
{
   // A panel is a passive layout container - never a keyboard-focus target. That now comes for free from the
   // Focusable=false default (see InputUIComponent); no per-panel override needed.

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
            // Insert at the SAME index (not AddRange/append) so the visual order mirrors Children - an inserted child
            // (e.g. a reordered tab) must keep its slot in the paint order, not jump to the end.
            VisualChildrenCollection.InsertRange(e.NewStartingIndex, e.NewItems.OfType<IUIComponent>());
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
            // A Reset carries no items, so the collection's own handler cannot name what left - only we still can, and only
            // BEFORE they are dropped. Everything else (add/remove/replace) is named by VisualChildrenCollectionChanged.
            foreach (var child in VisualChildrenCollection) RenderDirty.MarkStructural(child);
            LogicalChildrenCollection.Clear();
            VisualChildrenCollection.Clear();
            break;
      }

      InvalidateMeasure();
   }

   protected override void OnRender(IDrawingContext context)
   {
      context.ForControl(this).DrawRectangle(Background, new Rect(new Size(ActualWidth, ActualHeight)));
  }

   // NO HitTestCore override: hit-testing is DECOUPLED from Background. A panel catches the mouse across its whole
   // (honest, content-tight) bounds like every other container (Border/Decorator/Control), whether or not a Background
   // is set - so "I forgot to set a Background" never silently makes a container click-through (the WPF gotcha this
   // engine deliberately avoids). Children are still hit-tested BEFORE the panel, so a real child always wins. To make
   // a panel (or any element) intentionally pass-through - e.g. a transparent overlay covering interactive siblings
   // behind it - set IsHitTestVisible="False" explicitly; that is the one, discoverable opt-out.

   public void AddOrSetChildComponent(object component)
   {
      if (component is IMeasurableComponent measurable)
      {
         Children.Add(measurable);
      }
   }

   public void RemoveAllChildComponents()
   {
      Children.Clear();
   }

   public IReadOnlyList<object> GetChildComponents() => Children.Cast<object>().ToList();

   public void InsertChildComponent(int index, object component)
   {
      if (component is IMeasurableComponent measurable)
         Children.Insert(Math.Clamp(index, 0, Children.Count), measurable);
   }

   public void RemoveChildComponentAt(int index)
   {
      if (index >= 0 && index < Children.Count) Children.RemoveAt(index);
   }
}