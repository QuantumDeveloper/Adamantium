using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Adamantium.UI.Media;

namespace Adamantium.UI
{
    public interface IUIComponent : IAdamantiumComponent, IComponent
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
        bool UseLayoutRounding { get; set; }
        bool IsMeasureValid { get; }
        bool IsArrangeValid { get; }
        bool IsGeometryValid { get; }
        Size DesiredSize { get; }
        Size RenderSize { get; set; }
        Point Location { get; set; }
        Vector2D Scale { get; set; }
        Double Rotation { get; set; }
        Visibility Visibility { get; set; }
        Rect Bounds { get; set; }
        Rect ClipRectangle { get; }
        Point ClipPosition { get; set; }
        IUIComponent VisualParent { get; }
        Int32 ZIndex { get; set; }
        bool IsAttachedToVisualTree { get; }

        IReadOnlyCollection<IUIComponent> GetVisualDescendants();
        
        IReadOnlyCollection<IUIComponent> VisualChildren { get; }

        void InvalidateMeasure();
        void InvalidateArrange();
        void InvalidateRender();

        /// <summary>
        /// Carries out a measure of the control.
        /// </summary>
        /// <param name="availableSize">The available size for the control.</param>
        /// <param name="force">
        /// If true, the control will be measured even if <paramref name="availableSize"/> has not
        /// changed from the last measure.
        /// </param>
        void Measure(Size availableSize, bool force = false);

        /// <summary>
        /// Arranges the control and its children.
        /// </summary>
        /// <param name="rect">The control's new bounds.</param>
        /// <param name="force">
        /// If true, the control will be arranged even if <paramref name="rect"/> has not changed
        /// from the last arrange.
        /// </param>
        void Arrange(Rect rect, bool force = false);

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
        void OnRender(DrawingContext context);

        }
}