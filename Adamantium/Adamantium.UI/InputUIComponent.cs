using System;
using Adamantium.UI.Input;
using Adamantium.UI.RoutedEvents;
using Adamantium.UI.Windows.Input;

namespace Adamantium.UI;

public class InputUIComponent : MeasurableUIComponent, IInputComponent
{
    #region Routed events

    public static readonly RoutedEvent LoadedEvent = EventManager.RegisterRoutedEvent("Loaded",
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(UIComponent));
    
    public static readonly RoutedEvent UnloadedEvent = EventManager.RegisterRoutedEvent("Unloaded",
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent InitializedEvent = EventManager.RegisterRoutedEvent("Initialized",
        RoutingStrategy.Direct, typeof(RoutedEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent TextInputEvent = EventManager.RegisterRoutedEvent("TextInput",
        RoutingStrategy.Bubble, typeof(TextInputEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewTextInputEvent = 
        EventManager.RegisterRoutedEvent("PreviewTextInput",
            RoutingStrategy.Tunnel, typeof(TextInputEventHandler), typeof(UIComponent));
        
    public static readonly RoutedEvent MouseLeftButtonDownEvent =
        EventManager.RegisterRoutedEvent("MouseLeftButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseLeftButtonDownEvent =
        EventManager.RegisterRoutedEvent("RawMouseLeftButtonDownEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseLeftButtonUpEvent =
        EventManager.RegisterRoutedEvent("RawMouseLeftButtonUpEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseRightButtonDownEvent =
        EventManager.RegisterRoutedEvent("RawMouseRightButtonDownEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseRightButtonUpEvent =
        EventManager.RegisterRoutedEvent("RawMouseRightButtonUpEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseMiddleButtonDownEvent =
        EventManager.RegisterRoutedEvent("RawMouseMiddleButtonDownEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent RawMouseMiddleButtonUpEvent =
        EventManager.RegisterRoutedEvent("RawMouseMiddleButtonUpEvent",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseLeftButtonUpEvent = 
        EventManager.RegisterRoutedEvent("MouseLeftButtonUp",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseRightButtonDownEvent =
        EventManager.RegisterRoutedEvent("MouseRightButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseRightButtonUpEvent = EventManager.RegisterRoutedEvent(
        "MouseRightButtonUp",
        RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseMiddleButtonDownEvent =
        EventManager.RegisterRoutedEvent("MouseMiddleButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent MouseMiddleButtonUpEvent = EventManager.RegisterRoutedEvent(
        "MouseMiddleButtonUp",
        RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));



    public static readonly RoutedEvent PreviewMouseLeftButtonDownEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseLeftButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewMouseLeftButtonUpEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseLeftButtonUp",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewMouseRightButtonDownEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseRightButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewMouseRightButtonUpEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseRightButtonUp",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewMouseMiddleButtonDownEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseMiddleButtonDown",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    public static readonly RoutedEvent PreviewMouseMiddleButtonUpEvent =
        EventManager.RegisterRoutedEvent("PreviewMouseMiddleButtonUp",
            RoutingStrategy.Direct, typeof(MouseButtonEventHandler), typeof(UIComponent));

    #endregion
    
    static InputUIComponent()
    {
        FocusManager.GotFocusEvent.RegisterClassHandler<IInputComponent>(new RoutedEventHandler(GotFocusHandler));
        FocusManager.LostFocusEvent.RegisterClassHandler<IInputComponent>(new RoutedEventHandler(LostFocusHandler));
        Mouse.MouseEnterEvent.RegisterClassHandler<IInputComponent>(new MouseEventHandler(MouseEnterHandler));
        Mouse.MouseLeaveEvent.RegisterClassHandler<IInputComponent>(new MouseEventHandler(MouseLeaveHandler));
        Mouse.PreviewMouseDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseDownHandler));
        Mouse.MouseDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseDownHandler));
        Mouse.PreviewMouseUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseUpHandler));
        Mouse.MouseUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseUpHandler));
        Mouse.RawMouseDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseDownHandler));
        Mouse.RawMouseUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseUpHandler));
        Mouse.MouseMoveEvent.RegisterClassHandler<IInputComponent>(new MouseEventHandler(MouseMoveHandler));
        Mouse.RawMouseMoveEvent.RegisterClassHandler<IInputComponent>(new RawMouseEventHandler(RawMouseMoveHandler));

        PreviewMouseLeftButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseLeftButtonDownHandler));
        PreviewMouseLeftButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseLeftButtonUpHandler));
        MouseLeftButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseLeftButtonDownHandler));
        MouseLeftButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseLeftButtonUpHandler));

        PreviewMouseRightButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseRightButtonDownHandler));
        PreviewMouseRightButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseRightButtonUpHandler));
        MouseRightButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseRightButtonDownHandler));
        MouseRightButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseRightButtonUpHandler));

        PreviewMouseMiddleButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseMiddleButtonDownHandler));
        PreviewMouseMiddleButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(PreviewMouseMiddleButtonUpHandler));
        MouseMiddleButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseMiddleButtonDownHandler));
        MouseMiddleButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(MouseMiddleButtonUpHandler));

        RawMouseLeftButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseLeftButtonDownHandler));
        RawMouseLeftButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseLeftButtonUpHandler));
        RawMouseRightButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseRightButtonDownHandler));
        RawMouseRightButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseRightButtonUpHandler));
        RawMouseMiddleButtonDownEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseMiddleButtonDownHandler));
        RawMouseMiddleButtonUpEvent.RegisterClassHandler<IInputComponent>(new MouseButtonEventHandler(RawMouseMiddleButtonUpHandler));
    }
    
    public static readonly AdamantiumProperty IsFocusedProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsFocused),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(false));
    
    public static readonly AdamantiumProperty FocusableProperty = AdamantiumProperty.Register(nameof(Focusable),
        typeof(Boolean), typeof(UIComponent), new PropertyMetadata(true));
    
    public static readonly AdamantiumProperty CursorProperty = AdamantiumProperty.Register(nameof(Cursor),
        typeof(Cursor), typeof(UIComponent), new PropertyMetadata(Cursor.Default));
    
    public static readonly AdamantiumProperty IsKeyboardFocusedProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsKeyboardFocused),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(false));
    
    public static readonly AdamantiumProperty IsMouseOverProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsMouseOver),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsMouseDirectlyOverProperty =
        AdamantiumProperty.RegisterReadOnly(nameof(IsMouseDirectlyOver),
            typeof(Boolean), typeof(UIComponent), new PropertyMetadata(false));
    
    public bool IsMouseOver
    {
        get => GetValue<bool>(IsMouseOverProperty);
        private set => SetValue(IsMouseOverProperty, value);
    }

    public bool IsMouseDirectlyOver
    {
        get => GetValue<bool>(IsMouseDirectlyOverProperty);
        private set => SetValue(IsMouseDirectlyOverProperty, value);
    }

    
    public bool IsKeyboardFocused
    {
        get => GetValue<bool>(IsKeyboardFocusedProperty);
        private set => SetValue(IsKeyboardFocusedProperty, value);
    }
    
    public Cursor Cursor
    {
        get => GetValue<Cursor>(CursorProperty);
        set => SetValue(CursorProperty, value);
    }
    
    public Boolean IsFocused
    {
        get => GetValue<Boolean>(IsFocusedProperty);
        private set => SetValue(IsFocusedProperty, value);
    }
    
    public Boolean Focusable
    {
        get => GetValue<Boolean>(FocusableProperty);
        set => SetValue(FocusableProperty, value);
    }
    
    public event RoutedEventHandler Loaded
    {
        add => AddHandler(LoadedEvent, value);
        remove => RemoveHandler(LoadedEvent, value);
    }
    
    public event RoutedEventHandler Unloaded
    {
        add => AddHandler(UnloadedEvent, value);
        remove => RemoveHandler(UnloadedEvent, value);
    }
    
    public event RoutedEventHandler Initialized
    {
        add => AddHandler(InitializedEvent, value);
        remove => RemoveHandler(InitializedEvent, value);
    }

    public event RawMouseEventHandler RawMouseMove
    {
        add => AddHandler(Mouse.RawMouseMoveEvent, value);
        remove => RemoveHandler(Mouse.RawMouseMoveEvent, value);
    }

    public event MouseButtonEventHandler RawMouseDown
    {
        add => AddHandler(Mouse.RawMouseDownEvent, value);
        remove => RemoveHandler(Mouse.RawMouseDownEvent, value);
    }

    public event MouseButtonEventHandler RawMouseUp
    {
        add => AddHandler(Mouse.RawMouseUpEvent, value);
        remove => RemoveHandler(Mouse.RawMouseUpEvent, value);
    }

    public event MouseButtonEventHandler RawMouseLeftButtonDown
    {
        add => AddHandler(RawMouseLeftButtonDownEvent, value);
        remove => RemoveHandler(RawMouseLeftButtonDownEvent, value);
    }

    public event MouseButtonEventHandler RawMouseLeftButtonUp
    {
        add => AddHandler(RawMouseLeftButtonUpEvent, value);
        remove => RemoveHandler(RawMouseLeftButtonUpEvent, value);
    }

    public event MouseButtonEventHandler RawMouseRightButtonDown
    {
        add => AddHandler(RawMouseRightButtonDownEvent, value);
        remove => RemoveHandler(RawMouseRightButtonDownEvent, value);
    }

    public event MouseButtonEventHandler RawMouseRightButtonUp
    {
        add => AddHandler(RawMouseRightButtonUpEvent, value);
        remove => RemoveHandler(RawMouseRightButtonUpEvent, value);
    }

    public event MouseButtonEventHandler RawMouseMiddleButtonDown
    {
        add => AddHandler(RawMouseMiddleButtonDownEvent, value);
        remove => RemoveHandler(RawMouseMiddleButtonDownEvent, value);
    }

    public event MouseButtonEventHandler RawMouseMiddleButtonUp
    {
        add => AddHandler(RawMouseMiddleButtonUpEvent, value);
        remove => RemoveHandler(RawMouseMiddleButtonUpEvent, value);
    }

    

    public event MouseButtonEventHandler MouseDoubleClick
    {
        add => AddHandler(Mouse.MouseDoubleClickEvent, value);
        remove => RemoveHandler(Mouse.MouseDoubleClickEvent, value);
    }

    public event MouseButtonEventHandler MouseMiddleButtonDown
    {
        add => AddHandler(MouseMiddleButtonDownEvent, value);
        remove => RemoveHandler(MouseMiddleButtonDownEvent, value);
    }

    public event MouseButtonEventHandler MouseMiddleButtonUp
    {
        add => AddHandler(MouseMiddleButtonUpEvent, value);
        remove => RemoveHandler(MouseMiddleButtonUpEvent, value);
    }

    public event RoutedEventHandler GotFocus
    {
        add => AddHandler(FocusManager.GotFocusEvent, value);
        remove => RemoveHandler(FocusManager.GotFocusEvent, value);
    }

    public event RoutedEventHandler LostFocus
    {
        add => AddHandler(FocusManager.LostFocusEvent, value);
        remove => RemoveHandler(FocusManager.LostFocusEvent, value);
    }

    public event KeyEventHandler KeyDown
    {
        add => AddHandler(Keyboard.KeyDownEvent, value);
        remove => RemoveHandler(Keyboard.KeyDownEvent, value);
    }

    public event KeyEventHandler KeyUp
    {
        add => AddHandler(Keyboard.KeyUpEvent, value);
        remove => RemoveHandler(Keyboard.KeyUpEvent, value);
    }

    public event KeyboardGotFocusEventHandler GotKeyboardFocus
    {
        add => AddHandler(Keyboard.GotKeyboardFocusEvent, value);
        remove => RemoveHandler(Keyboard.GotKeyboardFocusEvent, value);
    }

    public event KeyboardFocusChangedEventHandler LostKeyboardFocus
    {
        add => AddHandler(Keyboard.LostKeyboardFocusEvent, value);
        remove => RemoveHandler(Keyboard.LostKeyboardFocusEvent, value);
    }

    public event MouseEventHandler GotMouseCapture
    {
        add => AddHandler(Mouse.GotMouseCaptureEvent, value);
        remove => RemoveHandler(Mouse.GotMouseCaptureEvent, value);
    }

    public event MouseEventHandler LostMouseCapture
    {
        add => AddHandler(Mouse.LostMouseCaptureEvent, value);
        remove => RemoveHandler(Mouse.LostMouseCaptureEvent, value);
    }

    public event MouseEventHandler MouseEnter
    {
        add => AddHandler(Mouse.MouseEnterEvent, value);
        remove => RemoveHandler(Mouse.MouseEnterEvent, value);
    }

    public event MouseEventHandler MouseLeave
    {
        add => AddHandler(Mouse.MouseLeaveEvent, value);
        remove => RemoveHandler(Mouse.MouseLeaveEvent, value);
    }

    public event MouseEventHandler MouseMove
    {
        add => AddHandler(Mouse.MouseMoveEvent, value);
        remove => AddHandler(Mouse.MouseMoveEvent, value);
    }

    public event MouseWheelEventHandler MouseWheel
    {
        add => AddHandler(Mouse.MouseWheelEvent, value);
        remove => AddHandler(Mouse.MouseWheelEvent, value);
    }

    public event MouseButtonEventHandler MouseDown
    {
        add => AddHandler(Mouse.MouseDownEvent, value);
        remove => RemoveHandler(Mouse.MouseDownEvent, value);
    }

    public event MouseButtonEventHandler MouseUp
    {
        add => AddHandler(Mouse.MouseUpEvent, value);
        remove => RemoveHandler(Mouse.MouseUpEvent, value);
    }

    public event MouseButtonEventHandler MouseLeftButtonDown
    {
        add => AddHandler(MouseLeftButtonDownEvent, value);
        remove => RemoveHandler(MouseLeftButtonDownEvent, value);
    }

    public event MouseButtonEventHandler MouseLeftButtonUp
    {
        add => AddHandler(MouseLeftButtonUpEvent, value);
        remove => RemoveHandler(MouseLeftButtonUpEvent, value);
    }

    public event MouseButtonEventHandler MouseRightButtonDown
    {
        add => AddHandler(MouseRightButtonDownEvent, value);
        remove => RemoveHandler(MouseRightButtonDownEvent, value);
    }

    public event MouseButtonEventHandler MouseRightButtonUp
    {
        add => AddHandler(MouseRightButtonUpEvent, value);
        remove => RemoveHandler(MouseRightButtonUpEvent, value);
    }

    public event TextInputEventHandler TextInput
    {
        add => AddHandler(TextInputEvent, value);
        remove => RemoveHandler(TextInputEvent, value);
    }


    public event KeyEventHandler PreviewKeyDown
    {
        add => AddHandler(Keyboard.PreviewKeyDownEvent, value);
        remove => RemoveHandler(Keyboard.PreviewKeyDownEvent, value);
    }

    public event KeyEventHandler PreviewKeyUp
    {
        add => AddHandler(Keyboard.PreviewKeyUpEvent, value);
        remove => RemoveHandler(Keyboard.PreviewKeyUpEvent, value);
    }

    public event KeyboardGotFocusEventHandler PreviewGotKeyboardFocus
    {
        add => AddHandler(Keyboard.PreviewGotKeyboardFocusEvent, value);
        remove => RemoveHandler(Keyboard.PreviewGotKeyboardFocusEvent, value);
    }

    public event KeyboardFocusChangedEventHandler PreviewLostKeyboardFocus
    {
        add => AddHandler(Keyboard.PreviewLostKeyboardFocusEvent, value);
        remove => RemoveHandler(Keyboard.PreviewLostKeyboardFocusEvent, value);
    }

    public event MouseEventHandler PreviewGotMouseCapture
    {
        add => AddHandler(Mouse.PreviewGotMouseCaptureEvent, value);
        remove => RemoveHandler(Mouse.PreviewGotMouseCaptureEvent, value);
    }

    public event MouseEventHandler PreviewLostMouseCapture
    {
        add => AddHandler(Mouse.PreviewLostMouseCaptureEvent, value);
        remove => RemoveHandler(Mouse.PreviewLostMouseCaptureEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseDown
    {
        add => AddHandler(Mouse.PreviewMouseDownEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseDownEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseUp
    {
        add => AddHandler(Mouse.PreviewMouseUpEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseUpEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseLeftButtonDown
    {
        add => AddHandler(PreviewMouseLeftButtonDownEvent, value);
        remove => RemoveHandler(PreviewMouseLeftButtonDownEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseLeftButtonUp
    {
        add => AddHandler(PreviewMouseLeftButtonUpEvent, value);
        remove => RemoveHandler(PreviewMouseLeftButtonUpEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseRightButtonDown
    {
        add => AddHandler(PreviewMouseRightButtonDownEvent, value);
        remove => RemoveHandler(PreviewMouseRightButtonDownEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseRightButtonUp
    {
        add => AddHandler(PreviewMouseRightButtonUpEvent, value);
        remove => RemoveHandler(PreviewMouseRightButtonUpEvent, value);
    }

    public event MouseWheelEventHandler PreviewMouseWheel
    {
        add => AddHandler(Mouse.PreviewMouseWheelEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseWheelEvent, value);
    }

    public event MouseEventHandler PreviewMouseMove
    {
        add => AddHandler(Mouse.PreviewMouseMoveEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseMoveEvent, value);
    }

    public event TextInputEventHandler PreviewTextInput
    {
        add => AddHandler(PreviewTextInputEvent, value);
        remove => RemoveHandler(PreviewTextInputEvent, value);
    }

    public event MouseButtonEventHandler PreviewMouseDoubleClick
    {
        add => AddHandler(Mouse.PreviewMouseDoubleClickEvent, value);
        remove => RemoveHandler(Mouse.PreviewMouseDoubleClickEvent, value);
    }
    
    

    private static void GotFocusHandler(object sender, RoutedEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnGotFocus(e);
    }

    private static void LostFocusHandler(object sender, RoutedEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnLostFocus(e);
    }

    private static void MouseEnterHandler(object sender, MouseEventArgs e)
    {
        if (sender is InputUIComponent ui && !ui.IsMouseOver)
        {
            ui.OnMouseEnter(e);
        }
    }

    private static void MouseLeaveHandler(object sender, MouseEventArgs e)
    {
        if (sender is InputUIComponent ui && ui.IsMouseOver)
        {
            ui.OnMouseLeave(e);
        }
    }

    private static void PreviewMouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = PreviewMouseLeftButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = PreviewMouseRightButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = PreviewMouseMiddleButtonDownEvent;
            }
            input.RaiseEvent(args);
        }
    }

    private static void PreviewMouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = PreviewMouseLeftButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = PreviewMouseRightButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = PreviewMouseMiddleButtonUpEvent;
            }
            input.RaiseEvent(args);
        }
    }

    private static void MouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = MouseLeftButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = MouseRightButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = MouseMiddleButtonDownEvent;
            }

            input.RaiseEvent(args);
        }
    }

    private static void MouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = MouseLeftButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = MouseRightButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = MouseMiddleButtonUpEvent;
            }

            input.RaiseEvent(args);
        }
    }

    private static void RawMouseDownHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = RawMouseLeftButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = RawMouseRightButtonDownEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = RawMouseMiddleButtonDownEvent;
            }

            if (e.ChangedButton != MouseButtons.None)
            {
                input.RaiseEvent(args);
            }
        }
    }

    private static void RawMouseUpHandler(object sender, MouseButtonEventArgs e)
    {
        if (sender is IInputComponent input)
        {
            var args = new MouseButtonEventArgs(e.MouseDevice, e.ChangedButton, e.ButtonState, e.Modifiers, e.Timestamp);
            if (e.ChangedButton == MouseButtons.Left)
            {
                args.RoutedEvent = RawMouseLeftButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Right)
            {
                args.RoutedEvent = RawMouseRightButtonUpEvent;
            }
            else if (e.ChangedButton == MouseButtons.Middle)
            {
                args.RoutedEvent = RawMouseMiddleButtonUpEvent;
            }

            if (e.ChangedButton != MouseButtons.None)
            {
                input.RaiseEvent(args);
            }
        }
    }

    private static void RawMouseMoveHandler(object sender, UnboundMouseEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseMove(ui, e);
    }

    private static void MouseMoveHandler(object sender, MouseEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseMove(ui, e);
    }

    private static void PreviewMouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseLeftButtonDown(ui, e);
    }

    private static void PreviewMouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseLeftButtonUp(ui, e);
    }

    private static void MouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseLeftButtonDown(ui, e);
    }

    private static void MouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseLeftButtonUp(ui, e);
    }

    private static void PreviewMouseRightButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseRightButtonDown(ui, e);
    }

    private static void PreviewMouseRightButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseRightButtonUp(ui, e);
    }

    private static void MouseRightButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseRightButtonDown(ui, e);
    }

    private static void MouseRightButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseRightButtonUp(ui, e);
    }

    private static void PreviewMouseMiddleButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseMiddleButtonDown(ui, e);
    }

    private static void PreviewMouseMiddleButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnPreviewMouseMiddleButtonUp(ui, e);
    }

    private static void MouseMiddleButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseMiddleButtonDown(ui, e);
    }

    private static void MouseMiddleButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnMouseMiddleButtonUp(ui, e);
    }

    private static void RawMouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseLeftButtonDown(ui, e);
    }

    private static void RawMouseLeftButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseLeftButtonUp(ui, e);
    }

    private static void RawMouseRightButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseRightButtonDown(ui, e);
    }

    private static void RawMouseRightButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseRightButtonUp(ui, e);
    }

    private static void RawMouseMiddleButtonDownHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseMiddleButtonDown(ui, e);
    }

    private static void RawMouseMiddleButtonUpHandler(object sender, MouseButtonEventArgs e)
    {
        var ui = sender as InputUIComponent;
        ui?.OnRawMouseMiddleButtonUp(ui, e);
    }

    protected virtual void OnRawMouseMove(object sender, UnboundMouseEventArgs e)
    {

    }

    protected virtual void OnMouseMove(object sender, MouseEventArgs e)
    {
    }

    protected virtual void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnPreviewMouseMiddleButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnPreviewMouseMiddleButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseMiddleButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnMouseMiddleButtonUp(object sender, MouseButtonEventArgs e)
    {

    }



    protected virtual void OnRawMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnRawMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnRawMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnRawMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnRawMouseMiddleButtonDown(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnRawMouseMiddleButtonUp(object sender, MouseButtonEventArgs e)
    {

    }

    protected virtual void OnGotFocus(RoutedEventArgs e)
    {
        IsFocused = e.OriginalSource == this;
    }

    protected virtual void OnLostFocus(RoutedEventArgs e)
    {
        IsFocused = false;
    }

    protected virtual void OnMouseEnter(MouseEventArgs e)
    {
        IsMouseOver = true;
        Mouse.Cursor = Cursor;
    }

    protected virtual void OnMouseLeave(MouseEventArgs e)
    {
        IsMouseOver = false;
    }
    
    public bool CaptureMouse()
    {
        return Mouse.Capture(this);
    }

    public bool CaptureStylus()
    {
        throw new NotImplementedException();
    }

    public bool Focus()
    {
        return FocusManager.Focus(this);
    }
    
    public void ReleaseMouseCapture()
    {
        throw new NotImplementedException();
    }

    public void ReleaseStylusCapture()
    {
        throw new NotImplementedException();
    }
}