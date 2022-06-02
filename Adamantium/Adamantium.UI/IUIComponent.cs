using System;
using System.Collections.Generic;
using Adamantium.UI.Input;
using Adamantium.UI.Media;
using Adamantium.UI.RoutedEvents;
using Transform = Adamantium.UI.Media.Transform;

namespace Adamantium.UI;

public interface IUIComponent : IFundamentalUIComponent
{
    event MouseButtonEventHandler RawMouseDown;
    event MouseButtonEventHandler RawMouseUp;
    event MouseButtonEventHandler RawMouseLeftButtonDown;
    event MouseButtonEventHandler RawMouseLeftButtonUp;
    event MouseButtonEventHandler RawMouseRightButtonDown;
    event MouseButtonEventHandler RawMouseRightButtonUp;
    event MouseButtonEventHandler RawMouseMiddleButtonDown;
    event MouseButtonEventHandler RawMouseMiddleButtonUp;
    event SizeChangedEventHandler SizeChanged;
    event MouseButtonEventHandler MouseDoubleClick;
    event MouseButtonEventHandler MouseMiddleButtonDown;
    event MouseButtonEventHandler MouseMiddleButtonUp;
    event RoutedEventHandler GotFocus;
    event RoutedEventHandler LostFocus;
    event KeyEventHandler KeyDown;
    event KeyEventHandler KeyUp;
    event KeyboardGotFocusEventHandler GotKeyboardFocus;
    event KeyboardFocusChangedEventHandler LostKeyboardFocus;
    event MouseEventHandler GotMouseCapture;
    event MouseEventHandler LostMouseCapture;
    event MouseEventHandler MouseEnter;
    event MouseEventHandler MouseLeave;
    event MouseEventHandler MouseMove;
    event MouseWheelEventHandler MouseWheel;
    event MouseButtonEventHandler MouseDown;
    event MouseButtonEventHandler MouseUp;
    event MouseButtonEventHandler MouseLeftButtonDown;
    event MouseButtonEventHandler MouseLeftButtonUp;
    event MouseButtonEventHandler MouseRightButtonDown;
    event MouseButtonEventHandler MouseRightButtonUp;
    event TextInputEventHandler TextInput;
    event KeyEventHandler PreviewKeyDown;
    event KeyEventHandler PreviewKeyUp;
    event KeyboardGotFocusEventHandler PreviewGotKeyboardFocus;
    event KeyboardFocusChangedEventHandler PreviewLostKeyboardFocus;
    event MouseButtonEventHandler PreviewMouseDoubleClick;
    event MouseEventHandler PreviewGotMouseCapture;
    event MouseEventHandler PreviewLostMouseCapture;
    event MouseButtonEventHandler PreviewMouseDown;
    event MouseButtonEventHandler PreviewMouseUp;
    event MouseButtonEventHandler PreviewMouseLeftButtonDown;
    event MouseButtonEventHandler PreviewMouseLeftButtonUp;
    event MouseButtonEventHandler PreviewMouseRightButtonDown;
    event MouseButtonEventHandler PreviewMouseRightButtonUp;
    event MouseWheelEventHandler PreviewMouseWheel;
    event MouseEventHandler PreviewMouseMove;
    event TextInputEventHandler PreviewTextInput;
    event EventHandler<VisualParentChangedEventArgs> VisualParentChanged;
        
    Cursor Cursor { get; set; }
    Boolean ClipToBounds { get; set; }
    Double Opacity { get; set; }
    bool IsEnabled { get; set; }
    Boolean AllowDrop { get; set; }
    Boolean Focusable { get; set; }
    bool IsMouseOver { get; }
    bool IsMouseDirectlyOver { get; }
    bool IsKeyboardFocused { get; }
    String Uid { get; }
    Boolean IsFocused { get; }
    Boolean IsHitTestVisible { get; set; }
    bool IsGeometryValid { get; }
    Size RenderSize { get; set; }
    Vector2 Location { get; set; }
    Visibility Visibility { get; set; }
    Rect Bounds { get; set; }
    Rect ClipRectangle { get; }
    Vector2 ClipPosition { get; set; }
    IUIComponent VisualParent { get; }
    Int32 ZIndex { get; set; }
    bool IsAttachedToVisualTree { get; }
    
    Transform LayoutTransform { get; set; }
    
    Transform RenderTransform { get; set; }

    IReadOnlyCollection<IUIComponent> GetVisualDescendants();
        
    IReadOnlyCollection<IUIComponent> VisualChildren { get; }

    void InvalidateRender();

    void Render(DrawingContext context);
    void AddHandler(RoutedEvent routedEvent, Delegate handler, bool handledEventsToo = false);
    bool CaptureMouse();
    bool CaptureStylus();
    bool Focus();
    void RaiseEvent(RoutedEventArgs e);
    IEnumerable<IUIComponent> GetBubbleEventRoute();
    IEnumerable<IUIComponent> GetTunnelEventRoute();
    void RemoveHandler(RoutedEvent routedEvent, Delegate handler);
    void ReleaseMouseCapture();
    void ReleaseStylusCapture();
}