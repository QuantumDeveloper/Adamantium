﻿using Adamantium.Mathematics;
using Adamantium.Win32;

namespace Adamantium.UI.Input
{
   /// <summary>
   /// 
   /// </summary>
   public static class Mouse
   {
      public static readonly RoutedEvent MouseMoveEvent = EventManager.RegisterRoutedEvent("MouseMove",
         RoutingStrategy.Bubble, typeof(MouseEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent MouseEnterEvent = EventManager.RegisterRoutedEvent("MouseEnter",
         RoutingStrategy.Direct, typeof(MouseEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent MouseLeaveEvent = EventManager.RegisterRoutedEvent("MouseLeave",
         RoutingStrategy.Direct, typeof(MouseEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent MouseWheelEvent = EventManager.RegisterRoutedEvent("MouseWheel",
         RoutingStrategy.Bubble, typeof(MouseWheelEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent MouseDownEvent = EventManager.RegisterRoutedEvent("MouseDown",
         RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent MouseUpEvent = EventManager.RegisterRoutedEvent("MouseUp",
         RoutingStrategy.Bubble, typeof(MouseButtonEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent GotMouseCaptureEvent = EventManager.RegisterRoutedEvent("GotMouseCapture",
         RoutingStrategy.Bubble, typeof(MouseEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent LostMouseCaptureEvent = EventManager.RegisterRoutedEvent("LostMouseCapture",
         RoutingStrategy.Bubble, typeof(MouseEventHandler), typeof(UIComponent));

      //TODO: deside leave it as bubble or make it direct event
      public static readonly RoutedEvent RawMouseMoveEvent = EventManager.RegisterRoutedEvent("RawMouseMove",
         RoutingStrategy.Direct, typeof (RawMouseEventHandler), typeof (UIComponent));

      public static readonly RoutedEvent RawMouseDownEvent = EventManager.RegisterRoutedEvent("RawMouseDown",
         RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent RawMouseUpEvent = EventManager.RegisterRoutedEvent("RawMouseUp",
         RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));


      public static readonly RoutedEvent PreviewMouseMoveEvent = EventManager.RegisterRoutedEvent("PreviewMouseMove",
         RoutingStrategy.Tunnel, typeof(MouseEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent PreviewMouseWheelEvent = EventManager.RegisterRoutedEvent("PreviewMouseWheel",
         RoutingStrategy.Tunnel, typeof(MouseWheelEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent PreviewMouseDownEvent = EventManager.RegisterRoutedEvent("PreviewMouseDown",
         RoutingStrategy.Tunnel, typeof(MouseButtonEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent PreviewMouseUpEvent = EventManager.RegisterRoutedEvent("PreviewMouseUp",
         RoutingStrategy.Tunnel, typeof(MouseButtonEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent PreviewGotMouseCaptureEvent = EventManager.RegisterRoutedEvent("PreviewGotMouseCapture",
         RoutingStrategy.Tunnel, typeof(MouseEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent PreviewLostMouseCaptureEvent = EventManager.RegisterRoutedEvent("PreviewLostMouseCapture",
         RoutingStrategy.Tunnel, typeof(MouseEventHandler), typeof(UIComponent));


      public static readonly RoutedEvent MouseDoubleClickEvent = EventManager.RegisterRoutedEvent("MouseDoubleClick",
         RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

      public static readonly RoutedEvent PreviewMouseDoubleClickEvent = EventManager.RegisterRoutedEvent("PreviewMouseDoubleClick",
         RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));


      static Mouse()
      {
         PrimaryDevice = WindowsMouseDevice.Instance;
      }
      

      public static MouseDevice PrimaryDevice { get; }

      public static IInputElement Captured => PrimaryDevice.Captured;
      public static MouseButtonState LeftButton => PrimaryDevice.LeftButton;
      public static MouseButtonState RightButton => PrimaryDevice.RightButton;
      public static MouseButtonState MiddleButton => PrimaryDevice.MiddleButton;
      public static MouseButtonState XButton1 => PrimaryDevice.XButton1;
      public static MouseButtonState XButton2 => PrimaryDevice.XButton2;

      public static Point ScreenCoordinates
      {
         get
         {
            NativePoint point;
            Interop.GetCursorPos(out point);
            return point;
         }
         set { Interop.SetCursorPos((int)value.X, (int)value.Y); }
      }

      public static Point GetPosition(IInputElement element)
      {
         return PrimaryDevice.GetPosition(element);
      }

      private static Cursor _cursor = Cursor.Default;

      public static Cursor Cursor
      {
         get
         {
            return _cursor;
         }
         set
         {
            _cursor = value;
            Interop.SetCursor(value.CursorHandle);
         }
      }

      public static bool Capture(IInputElement element)
      {
         return PrimaryDevice.Capture(element);
      }
   }

   
}
