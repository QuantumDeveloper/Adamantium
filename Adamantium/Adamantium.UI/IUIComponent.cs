using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Adamantium.UI.Media;

namespace Adamantium.UI
{
    public interface IUIComponent : IVisualComponent
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
        IVisualComponent VisualComponentParent { get; }
        Int32 ZIndex { get; set; }
        bool IsAttachedToVisualTree { get; }

        /// <summary>
        /// Gets or sets the parent object that inherited <see cref="AdamantiumProperty"/> values
        /// are inherited from.
        /// </summary>
        /// <value>
        /// The inheritance parent.
        /// </value>
        AdamantiumComponent InheritanceParent { get; set; }

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
        IEnumerable<UiComponent> GetBubbleEventRoute();
        IEnumerable<UiComponent> GetTunnelEventRoute();
        void RemoveHandler(RoutedEvent routedEvent, Delegate handler);
        void ReleaseMouseCapture();
        void ReleaseStylusCapture();
        void OnRender(DrawingContext context);

        /// <summary>
        /// Fires when value on <see cref="AdamantiumProperty"/> was changed
        /// </summary>
        event EventHandler<AdamantiumPropertyChangedEventArgs> PropertyChanged;

        /// <summary>
        /// Gets or sets the value of a <see cref="AdamantiumProperty"/>
        /// </summary>
        /// <param name="property"><see cref="AdamantiumProperty"/></param>
        object this[AdamantiumProperty property] { get; set; }

        /// <summary>
        /// Clears a <see cref="AdamantiumProperty"/>'s local value.
        /// </summary>
        /// <param name="property">The property.</param>
        void ClearValue(AdamantiumProperty property);

        /// <summary>
        /// Gets a <see cref="AdamantiumProperty"/> value.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>The value.</returns>
        object GetValue(AdamantiumProperty property);

        /// <summary>
        /// Gets a <see cref="AdamantiumProperty"/> value.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="property">The property.</param>
        /// <returns>The value.</returns>
        T GetValue<T>(AdamantiumProperty property);

        /// <summary>
        /// Check if <see cref="AdamantiumProperty"/> is registered.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <returns>True if property registered, otherwise - false</returns>
        bool IsRegistered(AdamantiumProperty property);

        /// <summary>
        /// Checks whether a <see cref="AdamantiumProperty"/> is set on this object.
        /// </summary>
        /// <param name="property">Adamantium property.</param>
        /// <returns>True if the property is set, otherwise false.</returns>
        bool IsSet(AdamantiumProperty property);

        /// <summary>
        /// Sets a <see cref="AdamantiumProperty"/> value.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="value">New value.</param>
        void SetValue(AdamantiumProperty property, object value);

        /// <summary>
        /// Sets a <see cref="AdamantiumProperty"/> value.
        /// </summary>
        /// <param name="property">The property.</param>
        /// <param name="value">New value.</param>
        void SetCurrentValue(AdamantiumProperty property, object value);
    }
}