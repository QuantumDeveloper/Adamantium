using Adamantium.Mathematics;
using Adamantium.UI.Input;
using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives
{
   public class Thumb:Control
   {
      public static readonly RoutedEvent DragStartedEvent = EventManager.RegisterRoutedEvent("DragStarted",
         RoutingStrategy.Bubble, typeof (DragStartedEventHandler), typeof (Thumb));

      public static readonly RoutedEvent DragDeltaEvent = EventManager.RegisterRoutedEvent("DragDelta",
         RoutingStrategy.Bubble, typeof(DragEventHandler), typeof(Thumb));

      public static readonly RoutedEvent DragCompletedEvent = EventManager.RegisterRoutedEvent("DragCompleted",
         RoutingStrategy.Bubble, typeof(DragCompletedEventHandler), typeof(Thumb));

      public static readonly AdamantiumProperty IsDraggingProperty = AdamantiumProperty.RegisterReadOnly(nameof(IsDragging),
         typeof (bool), typeof (Thumb), new PropertyMetadata(false));

      public event DragStartedEventHandler DragStarted
      {
         add => AddHandler(DragStartedEvent, value);
         remove => RemoveHandler(DragStartedEvent, value);
      }

      public event DragEventHandler DragDelta
      {
         add => AddHandler(DragDeltaEvent, value);
         remove => RemoveHandler(DragDeltaEvent, value);
      }

      public event DragCompletedEventHandler DragCompleted
      {
         add => AddHandler(DragCompletedEvent, value);
         remove => RemoveHandler(DragCompletedEvent, value);
      }

      public bool IsDragging
      {
         get => GetValue<bool>(IsDraggingProperty);
         private set => SetValue(IsDraggingProperty, value);
      }

      static Thumb()
      {
         DragStartedEvent.RegisterClassHandler<Thumb>(new DragStartedEventHandler(DragStartedHandler));
         DragDeltaEvent.RegisterClassHandler<Thumb>(new DragEventHandler(DragDeltaHandler));
         DragCompletedEvent.RegisterClassHandler<Thumb>(new DragCompletedEventHandler(DragCompletedHandler));
      }

      private static void DragStartedHandler(object sender, DragStartedEventArgs e)
      {
         var thumb = sender as Thumb;
         thumb?.OnDragStarted(e);
      }

      private static void DragDeltaHandler(object sender, DragEventArgs e)
      {
         var thumb = sender as Thumb;
         thumb?.OnDragDelta(e);
      }

      private static void DragCompletedHandler(object sender, DragCompletedEventArgs e)
      {
         var thumb = sender as Thumb;
         thumb?.OnDragCompleted(e);
      }

      protected virtual void OnDragStarted(DragStartedEventArgs e)
      {
      }

      protected virtual void OnDragDelta(DragEventArgs e)
      {
      }

      protected virtual void OnDragCompleted(DragCompletedEventArgs e)
      {
      }

      public Thumb()
      {
      }

      private Vector2D dragStartPoint;

      public void CancelDrag()
      {
         IsDragging = false;
      }

      protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
      {
         if (!IsDragging)
         {
            IsDragging = true;
            e.Handled = true;
            dragStartPoint = e.GetPosition(this);
            DragStartedEventArgs args = new DragStartedEventArgs(dragStartPoint);
            args.RoutedEvent = DragStartedEvent;
            RaiseEvent(args);
         }
      }

      protected override void OnRawMouseMove(object sender, UnboundMouseEventArgs e)
      {
         
         if (e.MouseDevice.LeftButton == MouseButtonState.Relesed)
         {
            IsDragging = false;
         }

         e.Handled = true;
         if (IsDragging && e.MouseDevice.LeftButton == MouseButtonState.Pressed)
         {
            var delta = e.GetPosition(this) - dragStartPoint;
            DragEventArgs args = new DragEventArgs(delta);
            args.RoutedEvent = DragDeltaEvent;
            RaiseEvent(args);
         }
      }

      protected override void OnMouseMove(object sender, MouseEventArgs e)
      {
         e.Handled = true;
      }

      protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
      {
         e.Handled = true;
      }

      protected override void OnRawMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
      {
         e.Handled = true;
         if (IsDragging)
         {
            var delta = e.GetPosition(this) - dragStartPoint;
            DragCompletedEventArgs args = new DragCompletedEventArgs(delta, false);
            args.RoutedEvent = DragCompletedEvent;
            RaiseEvent(args);
         }
         IsDragging = false;
      }

      protected override void OnRender(DrawingContext context)
      {
         if (!IsGeometryValid)
         {
            base.OnRender(context);
            context.BeginDraw(this);
            context.DrawRectangle(Background, new Rect(new Size(ActualWidth, ActualHeight)));
            context.EndDraw(this);
         }
      }
   }
}
