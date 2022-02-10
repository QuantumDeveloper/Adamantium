using Adamantium.UI.RoutedEvents;
using Adamantium.UI.Windows.Input;
using Adamantium.Win32;

namespace Adamantium.UI.Input;

/// <summary>
/// 
/// </summary>
public static class Mouse
{
   public static readonly RoutedEvent MouseMoveEvent = EventManager.RegisterRoutedEvent("MouseMove",
      RoutingStrategy.Bubble, typeof(MouseEventHandler), typeof(Mouse));

   public static readonly RoutedEvent MouseEnterEvent = EventManager.RegisterRoutedEvent("MouseEnter",
      RoutingStrategy.Direct, typeof(MouseEventHandler), typeof(Mouse));

   public static readonly RoutedEvent MouseLeaveEvent = EventManager.RegisterRoutedEvent("MouseLeave",
      RoutingStrategy.Direct, typeof(MouseEventHandler), typeof(Mouse));

   public static readonly RoutedEvent MouseWheelEvent = EventManager.RegisterRoutedEvent("MouseWheel",
      RoutingStrategy.Bubble, typeof(MouseWheelEventHandler), typeof(Mouse));

   public static readonly RoutedEvent MouseDownEvent = EventManager.RegisterRoutedEvent("MouseDown",
      RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(Mouse));

   public static readonly RoutedEvent MouseUpEvent = EventManager.RegisterRoutedEvent("MouseUp",
      RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(Mouse));

   public static readonly RoutedEvent GotMouseCaptureEvent = EventManager.RegisterRoutedEvent("GotMouseCapture",
      RoutingStrategy.Bubble, typeof(MouseEventHandler), typeof(Mouse));

   public static readonly RoutedEvent LostMouseCaptureEvent = EventManager.RegisterRoutedEvent("LostMouseCapture",
      RoutingStrategy.Bubble, typeof(MouseEventHandler), typeof(Mouse));

   //TODO: deside leave it as bubble or make it direct event
   public static readonly RoutedEvent RawMouseMoveEvent = EventManager.RegisterRoutedEvent("RawMouseMove",
      RoutingStrategy.Direct, typeof (RawMouseEventHandler), typeof (UIComponent));

   public static readonly RoutedEvent RawMouseDownEvent = EventManager.RegisterRoutedEvent("RawMouseDown",
      RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(Mouse));

   public static readonly RoutedEvent RawMouseUpEvent = EventManager.RegisterRoutedEvent("RawMouseUp",
      RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(Mouse));


   public static readonly RoutedEvent PreviewMouseMoveEvent = EventManager.RegisterRoutedEvent("PreviewMouseMove",
      RoutingStrategy.Tunnel, typeof(MouseEventHandler), typeof(Mouse));

   public static readonly RoutedEvent PreviewMouseWheelEvent = EventManager.RegisterRoutedEvent("PreviewMouseWheel",
      RoutingStrategy.Tunnel, typeof(MouseWheelEventHandler), typeof(Mouse));

   public static readonly RoutedEvent PreviewMouseDownEvent = EventManager.RegisterRoutedEvent("PreviewMouseDown",
      RoutingStrategy.Tunnel, typeof(MouseButtonEventHandler), typeof(Mouse));

   public static readonly RoutedEvent PreviewMouseUpEvent = EventManager.RegisterRoutedEvent("PreviewMouseUp",
      RoutingStrategy.Tunnel, typeof(MouseButtonEventHandler), typeof(Mouse));

   public static readonly RoutedEvent PreviewGotMouseCaptureEvent = EventManager.RegisterRoutedEvent("PreviewGotMouseCapture",
      RoutingStrategy.Tunnel, typeof(MouseEventHandler), typeof(Mouse));

   public static readonly RoutedEvent PreviewLostMouseCaptureEvent = EventManager.RegisterRoutedEvent("PreviewLostMouseCapture",
      RoutingStrategy.Tunnel, typeof(MouseEventHandler), typeof(Mouse));


   public static readonly RoutedEvent MouseDoubleClickEvent = EventManager.RegisterRoutedEvent("MouseDoubleClick",
      RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(Mouse));

   public static readonly RoutedEvent PreviewMouseDoubleClickEvent = EventManager.RegisterRoutedEvent("PreviewMouseDoubleClick",
      RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(Mouse));


   static Mouse()
   {
      PrimaryDevice = MouseDevice.CurrentDevice;
   }

   public static MouseDevice PrimaryDevice { get; }

   public static IInputComponent Captured => PrimaryDevice.Captured;
   public static MouseButtonState LeftButton => PrimaryDevice.LeftButton;
   public static MouseButtonState RightButton => PrimaryDevice.RightButton;
   public static MouseButtonState MiddleButton => PrimaryDevice.MiddleButton;
   public static MouseButtonState XButton1 => PrimaryDevice.XButton1;
   public static MouseButtonState XButton2 => PrimaryDevice.XButton2;

   public static Vector2 ScreenCoordinates
   {
      get
      {
         NativePoint point;
         Win32Interop.GetCursorPos(out point);
         return point;
      }
      set => Win32Interop.SetCursorPos((int)value.X, (int)value.Y);
   }

   public static Vector2 GetPosition(IInputComponent component)
   {
      return PrimaryDevice.GetPosition(component);
   }

   private static Cursor _cursor = Cursor.Default;

   public static Cursor Cursor
   {
      get => _cursor;
      set
      {
         _cursor = value;
         Win32Interop.SetCursor(value.CursorHandle);
      }
   }

   public static bool Capture(IInputComponent component)
   {
      return PrimaryDevice.Capture(component);
   }
}