using System;
using Adamantium.UI.RoutedEvents;
using Adamantium.UI.Windows.Input;

namespace Adamantium.UI.Input;

/// <summary>
/// Interface for all elements, which takes user input
/// </summary>
public interface IInputComponent: IMeasurableComponent
{
   /// <summary>
   /// Define is element could receive focus
   /// </summary>
   bool Focusable { get; set; }
   
   bool IsFocused { get; }
   
   Cursor Cursor { get; set; }
      
   /// <summary>
   /// Returns true if mouse cursor is over element
   /// </summary>
   bool IsMouseOver { get; }

   /// <summary>
   /// Returns true if mouse cursor directly over element
   /// </summary>
   bool IsMouseDirectlyOver { get; }
      
   /// <summary>
   /// Return true if Keyboard focused on element
   /// </summary>
   bool IsKeyboardFocused { get; }

   /// <summary>
   /// Occurs when the keyboard is focused on this element.
   /// </summary>
   event KeyboardGotFocusEventHandler GotKeyboardFocus;

   /// <summary>
   /// Occurs when the element captures the mouse.
   /// </summary>
   event MouseEventHandler GotMouseCapture;

   /// <summary>
   /// Occurs when a key is pressed while the keyboard is focused on this element.
   /// </summary>
   event KeyEventHandler KeyDown;

   /// <summary>
   /// Occurs when a key is released while the keyboard is focused on this element.
   /// </summary>
   event KeyEventHandler KeyUp;

   /// <summary>
   /// Occurs when the keyboard is no longer focused on this element.
   /// </summary>
   event KeyboardFocusChangedEventHandler LostKeyboardFocus;

   /// <summary>
   /// Occurs when this element loses mouse capture.
   /// </summary>
   event MouseEventHandler LostMouseCapture;

   /// <summary>
   /// Occurs when the mouse pointer enters the bounds of this element.
   /// </summary>
   event MouseEventHandler MouseEnter;

   /// <summary>
   /// Occurs when the mouse pointer leaves the bounds of this element.
   /// </summary>
   event MouseEventHandler MouseLeave;

   /// <summary>
   /// Occurs when the left mouse button is pressed while the mouse pointer is over
   /// the element.
   /// </summary>
   event MouseButtonEventHandler MouseLeftButtonDown;

   /// <summary>
   /// Occurs when the left mouse button is released while the mouse pointer is over
   /// the element.
   /// </summary>
   event MouseButtonEventHandler MouseLeftButtonUp;

   /// <summary>
   /// Occurs when the mouse pointer moves while the mouse pointer is over the element.
   /// </summary>
   event MouseEventHandler MouseMove;

   /// <summary>
   /// Occurs when the right mouse button is pressed while the mouse pointer is over
   /// the element.
   /// </summary>
   event MouseButtonEventHandler MouseRightButtonDown;

   /// <summary>
   /// Occurs when the right mouse button is released while the mouse pointer is over
   /// the element.
   /// </summary>
   event MouseButtonEventHandler MouseRightButtonUp;

   event MouseButtonEventHandler MouseDown;

   event MouseButtonEventHandler MouseUp;

   /// <summary>
   /// Occurs when the mouse wheel moves while the mouse pointer is over this element.
   /// </summary>
   event MouseWheelEventHandler MouseWheel;

   event RawMouseEventHandler RawMouseMove;

   event MouseButtonEventHandler RawMouseDown;

   event MouseButtonEventHandler RawMouseUp;

   event MouseButtonEventHandler RawMouseLeftButtonDown;

   event MouseButtonEventHandler RawMouseLeftButtonUp;

   event MouseButtonEventHandler RawMouseRightButtonDown;

   event MouseButtonEventHandler RawMouseRightButtonUp;

   event MouseButtonEventHandler RawMouseMiddleButtonDown;

   event MouseButtonEventHandler RawMouseMiddleButtonUp;

   event MouseButtonEventHandler MouseDoubleClick;

   public event MouseButtonEventHandler MouseMiddleButtonDown;

   public event MouseButtonEventHandler MouseMiddleButtonUp;

   public event RoutedEventHandler GotFocus;

   public event RoutedEventHandler LostFocus;

   /// <summary>
   /// Occurs when the keyboard is focused on this element.
   /// </summary>
   event KeyboardGotFocusEventHandler PreviewGotKeyboardFocus;

   /// <summary>
   /// Occurs when a key is pressed while the keyboard is focused on this element.
   /// </summary>
   event KeyEventHandler PreviewKeyDown;

   /// <summary>
   /// Occurs when a key is released while the keyboard is focused on this element.
   /// </summary>
   event KeyEventHandler PreviewKeyUp;

   /// <summary>
   /// Occurs when the keyboard is no longer focused on this element.
   /// </summary>
   event KeyboardFocusChangedEventHandler PreviewLostKeyboardFocus;

   /// <summary>
   /// Occurs when the left mouse button is pressed while the mouse pointer is over
   /// the element.
   /// </summary>
   event MouseButtonEventHandler PreviewMouseLeftButtonDown;

   /// <summary>
   /// Occurs when the left mouse button is released while the mouse pointer is over
   /// the element.
   /// </summary>
   event MouseButtonEventHandler PreviewMouseLeftButtonUp;

   /// <summary>
   /// Occurs when the mouse pointer moves while the mouse pointer is over the element.
   /// </summary>
   event MouseEventHandler PreviewMouseMove;

   /// <summary>
   /// Occurs when the right mouse button is pressed while the mouse pointer is over
   /// the element.
   /// </summary>
   event MouseButtonEventHandler PreviewMouseRightButtonDown;

   /// <summary>
   /// Occurs when the right mouse button is released while the mouse pointer is over
   /// the element.
   /// </summary>
   event MouseButtonEventHandler PreviewMouseRightButtonUp;

   /// <summary>
   /// Occurs when the mouse wheel moves while the mouse pointer is over this element.
   /// </summary>
   event MouseWheelEventHandler PreviewMouseWheel;

   /// <summary>
   /// Occurs when this element gets text in a device-independent manner.
   /// </summary>
   event TextInputEventHandler TextInput;

   /// <summary>
   /// Occurs when this element gets text in a device-independent manner.
   /// </summary>
   event TextInputEventHandler PreviewTextInput;

   event MouseEventHandler PreviewGotMouseCapture;

   public event MouseEventHandler PreviewLostMouseCapture;

   public event MouseButtonEventHandler PreviewMouseDown;

   public event MouseButtonEventHandler PreviewMouseUp;

   event MouseButtonEventHandler PreviewMouseDoubleClick;

   /// <summary>
   /// Attempts to force capture of the mouse to this element.
   /// </summary>
   /// <returns>true if the mouse is successfully captured; otherwise, false.</returns>
   bool CaptureMouse();

   /// <summary>
   /// Attempts to force capture of the stylus to this element.
   /// </summary>
   /// <returns>true if the stylus is successfully captured; otherwise, false.</returns>
   bool CaptureStylus();

   /// <summary>
   /// Attempts to focus the keyboard on this element.
   /// </summary>
   /// <returns>true if keyboard focus is moved to this element or already was on this element;
   /// otherwise, false.</returns>
   bool Focus();

   /// <summary>
   /// Releases the mouse capture, if this element holds the capture.
   /// </summary>
   void ReleaseMouseCapture();

   /// <summary>
   /// Releases the stylus capture, if this element holds the capture.
   /// </summary>
   void ReleaseStylusCapture();
}