using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Media;

namespace Adamantium.UI.Controls
{
   public abstract class Panel: FrameworkComponent
   {
      public static readonly AdamantiumProperty BackgroundProperty = AdamantiumProperty.Register(nameof(Background), typeof(Brush), typeof(Panel), new PropertyMetadata(Brushes.Transparent, PropertyMetadataOptions.AffectsRender));

      public Brush Background
      {
         get => GetValue<Brush>(BackgroundProperty);
         set => SetValue(BackgroundProperty, value);
      }

      private readonly UIElementCollection childern;

      [Content]
      public UIElementCollection Children => childern;

      protected Panel()
      {
         childern = new UIElementCollection();
         childern.CollectionChanged += ChildernChanged;
      }

      private void ChildernChanged(object sender, NotifyCollectionChangedEventArgs e)
      {
         switch (e.Action)
         {
            case NotifyCollectionChangedAction.Add:
               var controls = e.NewItems.OfType<FrameworkComponent>();
               LogicalChildren.InsertRange(e.NewStartingIndex, controls);
               VisualChildren.AddRange(e.NewItems.OfType<IVisual>());
               break;
            case NotifyCollectionChangedAction.Remove:
               LogicalChildren.Remove(e.OldItems.OfType<FrameworkComponent>());
               VisualChildren.Remove(e.OldItems.OfType<IVisual>());
               break;
            case NotifyCollectionChangedAction.Replace:
               for (var i = 0; i < e.OldItems.Count; ++i)
               {
                  var index = LogicalChildren.IndexOf((FrameworkComponent)e.OldItems[i]);
                  var child = (FrameworkComponent)e.NewItems[i];
                  LogicalChildren[index] = child;
                  VisualChildren[index] = child;
               }
               break;

            case NotifyCollectionChangedAction.Reset:
               LogicalChildren.Clear();
               VisualChildren.Clear();
               break;
         }

         InvalidateMeasure();
      }

      public override void OnRender(DrawingContext context)
      {
         context.BeginDraw(this);
         context.DrawRectangle(this, Background, new Rect(new Size(ActualWidth, ActualHeight)));
         context.EndDraw(this);
      }
   }
}
