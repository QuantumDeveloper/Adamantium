using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Primitives;

public class Thumb : Control
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

   private List<Size> sizes = new List<Size>();
   public Thumb()
   {
   }

   private Vector2 dragStartPoint;

   public void CancelDrag()
   {
      IsDragging = false;
   }

   protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
   {
      // Already dragging AND still holding capture -> ignore. If IsDragging is stale (capture was revoked externally,
      // e.g. an alt-tab, without an up reaching us) a fresh press re-starts cleanly instead of being swallowed.
      if (IsDragging && IsMouseCaptured) return;

      IsDragging = true;
      e.Handled = true;
      dragStartPoint = DragPosition(e);
      // Capture so the drag keeps tracking once the pointer leaves the thumb. WITHOUT this the move never reaches the
      // thumb: the raw-move event is routed to the FOCUSED element, and the captured MouseMove below is the only path
      // that honours capture - so the thumb (unfocused) would otherwise never see a drag and never move.
      CaptureMouse();
      RaiseEvent(new DragStartedEventArgs(dragStartPoint) { RoutedEvent = DragStartedEvent });
   }

   protected override void OnMouseMove(object sender, MouseEventArgs e)
   {
      e.Handled = true;
      if (!IsDragging) return;

      var delta = DragPosition(e) - dragStartPoint;
      RaiseEvent(new DragEventArgs(delta) { RoutedEvent = DragDeltaEvent });
   }

   protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
   {
      e.Handled = true;
      if (!IsDragging) return;

      IsDragging = false;
      if (IsMouseCaptured) ReleaseMouseCapture();
      var delta = DragPosition(e) - dragStartPoint;
      RaiseEvent(new DragCompletedEventArgs(delta, false) { RoutedEvent = DragCompletedEvent });
   }

   // Pointer position in the thumb's PARENT space, which does NOT move while the thumb is dragged. Measuring the delta
   // there makes DragDelta the true CUMULATIVE pointer movement since the press; measuring it relative to the thumb
   // itself was a residual that broke as soon as the thumb re-positioned (the value ran away to an extreme).
   private Vector2 DragPosition(MouseEventArgs e)
      => VisualParent is IInputComponent parent ? e.GetPosition(parent) : e.GetPosition(this);

   protected override Size MeasureOverride(Size availableSize)
   {
      sizes.Clear();
      foreach (var child in VisualChildren)
      {
         if (child is IMeasurableComponent measurableComponent)
         {
            measurableComponent.Measure(availableSize);
            sizes.Add(measurableComponent.DesiredSize);
         }
      }

      // No measurable children (e.g. a thumb/splitter with no template applied): desire nothing rather than indexing
      // an empty list. The element's own explicit Width/Height is still applied by Measure (Constrain).
      return sizes.Count > 0 ? sizes[0] : new Size();
   }

   protected override Size ArrangeOverride(Size finalSize)
   {
      // The thumb fills the slot it was arranged into (e.g. the rect a Track computed for it), with its template child
      // - if any - stretched to fill. Returning the child's size instead collapsed an untemplated thumb to nothing.
      foreach (var child in VisualChildren)
      {
         if (child is IMeasurableComponent measurableComponent)
         {
            measurableComponent.Arrange(new Rect(finalSize));
         }
      }

      return finalSize;
   }

   // protected override void OnRender(DrawingContext context)
   // {
   //    if (!IsGeometryValid)
   //    {
   //       context.DrawRectangle(Background, new Rect(new Size(ActualWidth, ActualHeight)));
   //    }
   // }
}