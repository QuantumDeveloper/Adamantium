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

   protected override void OnRender(IDrawingContext context)
   {
      context.ForControl(this).DrawRectangle(Background, new Rect(new Size(ActualWidth, ActualHeight)));
  }

   // A layout panel catches the mouse only where it paints a VISIBLE background; its transparent/empty areas are
   // pass-through, so a panel that overlaps other content (e.g. the centered slider bar over the centered wrap list)
   // doesn't eat clicks meant for what's behind it. Panel.Background DEFAULTS to Brushes.Transparent, so without this
   // every panel was a full-box hit target. Children are hit-tested BEFORE the panel, so this only frees the panel's
   // own empty gaps - a click on a real child (or on interactive content behind a gap) still lands.
   public override bool HitTestCore(Vector2 localPoint) => Background.IsVisible();

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