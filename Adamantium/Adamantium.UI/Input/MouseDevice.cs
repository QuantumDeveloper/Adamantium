using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Input.Raw;
using Adamantium.UI.Media;
using Adamantium.Win32;

namespace Adamantium.UI.Input;

public abstract class MouseDevice
{
    protected MouseDevice()
    {
    }

    private int clickCount = 0;
    private uint lastClickTime = 0;
    private IInputElement lastClickedElement = null;

    public IInputElement Captured { get; protected set; }
    public IInputElement DirectlyOver { get; private set; }

    public MouseButtonState LeftButton { get; private set; }
    public MouseButtonState RightButton { get; private set; }
    public MouseButtonState MiddleButton { get; private set; }
    public MouseButtonState XButton1 { get; private set; }
    public MouseButtonState XButton2 { get; private set; }

    public Cursor OverrideCursor { get; set; }

    private Vector2 Position;

    private static MouseDevice currentDeice;
        
    public static MouseDevice CurrentDevice
    {
        get
        {
            if (currentDeice == null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    currentDeice = WindowsMouseDevice.Instance;
                }
            }

            return currentDeice;
        } 
    }

    public bool Capture(IInputElement element)
    {
        if (element != null)
        {
            Captured = element;
            return true;
        }
        return false;
    }

    public Vector2 GetPosition(IInputElement relativeTo)
    {
        var p = Position;
        IUIComponent v = relativeTo;
        IUIComponent root = null;

        while (v != null)
        {
            p -= v.Bounds.Location;
            root = v;
            v = v.VisualParent;
        }

        return root.PointToClient(p);
    }

    public void SetCursor(Cursor cursor)
    {
        Win32Interop.SetCursor(cursor.CursorHandle);
    }

    public void UpdateCursor()
    {

    }

    public Vector2 GetScreenPosition()
    {
        Win32Interop.GetCursorPos(out var point);
        return point;
    }

    internal void UpdateButtonStates(InputModifiers buttons)
    {
        LeftButton = (buttons & InputModifiers.LeftMouseButton) != 0 ? MouseButtonState.Pressed : MouseButtonState.Relesed;
        RightButton = (buttons & InputModifiers.RightMouseButton) != 0 ? MouseButtonState.Pressed : MouseButtonState.Relesed;
        MiddleButton = (buttons & InputModifiers.MiddleMouseButton) != 0 ? MouseButtonState.Pressed : MouseButtonState.Relesed;
        XButton1 = (buttons & InputModifiers.X1MouseButton) != 0 ? MouseButtonState.Pressed : MouseButtonState.Relesed;
        XButton2 = (buttons & InputModifiers.X2MouseButton) != 0 ? MouseButtonState.Pressed : MouseButtonState.Relesed;
    }

    public void ProcessEvent(RawMouseEventArgs e)
    {
        Position = e.RootElement.PointToScreen(e.Position);
        UpdateButtonStates(e.InputModifiers);
        MouseButtons button = MouseButtons.None;
        switch (e.EventType)
        {
            case RawMouseEventType.MouseMove:
                MouseMove(e.RootElement, e.Position, e.InputModifiers, e.Timestamp);
                break;
            case RawMouseEventType.LeaveWindow:
                LeaveWindow(e.RootElement, e.Position, e.InputModifiers, e.Timestamp);
                break;

            case RawMouseEventType.LeftButtonDown:
            case RawMouseEventType.RightButtonDown:
            case RawMouseEventType.MiddleButtonDown:
            case RawMouseEventType.X1ButtonDown:
            case RawMouseEventType.X2ButtonDown:
                switch (e.EventType)
                {
                    case RawMouseEventType.LeftButtonDown:
                        button = MouseButtons.Left;
                        break;
                    case RawMouseEventType.RightButtonDown:
                        button = MouseButtons.Right;
                        break;
                    case RawMouseEventType.MiddleButtonDown:
                        button = MouseButtons.Middle;
                        break;
                    case RawMouseEventType.X1ButtonDown:
                        button = MouseButtons.XButton1;
                        break;
                    case RawMouseEventType.X2ButtonDown:
                        button = MouseButtons.XButton2;
                        break;
                }
                MouseDown(e.RootElement, e.Position, e.Timestamp, button, e.InputModifiers);
                break;
            case RawMouseEventType.LeftButtonUp:
            case RawMouseEventType.RightButtonUp:
            case RawMouseEventType.MiddleButtonUp:
            case RawMouseEventType.X1ButtonUp:
            case RawMouseEventType.X2ButtonUp:
                switch (e.EventType)
                {
                    case RawMouseEventType.LeftButtonUp:
                        button = MouseButtons.Left;
                        break;
                    case RawMouseEventType.RightButtonUp:
                        button = MouseButtons.Right;
                        break;
                    case RawMouseEventType.MiddleButtonUp:
                        button = MouseButtons.Middle;
                        break;
                    case RawMouseEventType.X1ButtonUp:
                        button = MouseButtons.XButton1;
                        break;
                    case RawMouseEventType.X2ButtonUp:
                        button = MouseButtons.XButton2;
                        break;
                }
                MouseUp(e.RootElement, e.Position, e.Timestamp, button, e.InputModifiers);
                break;
            case RawMouseEventType.MouseWheel:
                MouseWheel(e.RootElement, e.Position, e.InputModifiers, e.Timestamp, ((RawMouseWheelEventArgs)e).WheelDelta);
                break;
            case RawMouseEventType.LeftButtonDoubleClick:
            case RawMouseEventType.MiddleButtonDoubleClick:
            case RawMouseEventType.RightButtonDoubleClick:
                switch (e.EventType)
                {
                    case RawMouseEventType.LeftButtonDoubleClick:
                        button = MouseButtons.Left;
                        break;
                    case RawMouseEventType.RightButtonDoubleClick:
                        button = MouseButtons.Right;
                        break;
                    case RawMouseEventType.MiddleButtonDoubleClick:
                        button = MouseButtons.Left;
                        break;
                }
                MouseDoubleClick(e.RootElement, e.Position, e.Timestamp, button, e.InputModifiers);
                break;

            case RawMouseEventType.RawMouseMove:
                var args = (RawInputMouseEventArgs)e;
                RawMouseEvent(args.RootElement, args.Delta, args.InputModifiers, args.Timestamp);
                break;

            case RawMouseEventType.RawLeftButtonDown:
            case RawMouseEventType.RawRightButtonDown:
            case RawMouseEventType.RawMiddleButtonDown:
                switch (e.EventType)
                {
                    case RawMouseEventType.RawLeftButtonDown:
                        button = MouseButtons.Left;
                        break;
                    case RawMouseEventType.RawRightButtonDown:
                        button = MouseButtons.Right;
                        break;
                    case RawMouseEventType.RawMiddleButtonDown:
                        button = MouseButtons.Middle;
                        break;
                    case RawMouseEventType.X1ButtonUp:
                        button = MouseButtons.XButton1;
                        break;
                    case RawMouseEventType.X2ButtonUp:
                        button = MouseButtons.XButton2;
                        break;
                }
                RawInputMouseDown(e.RootElement, e.Position, e.Timestamp, button, e.InputModifiers);
                break;

            case RawMouseEventType.RawLeftButtonUp:
            case RawMouseEventType.RawRightButtonUp:
            case RawMouseEventType.RawMiddleButtonUp:
                switch (e.EventType)
                {
                    case RawMouseEventType.RawLeftButtonUp:
                        button = MouseButtons.Left;
                        break;
                    case RawMouseEventType.RawRightButtonUp:
                        button = MouseButtons.Right;
                        break;
                    case RawMouseEventType.RawMiddleButtonUp:
                        button = MouseButtons.Middle;
                        break;
                    case RawMouseEventType.X1ButtonUp:
                        button = MouseButtons.XButton1;
                        break;
                    case RawMouseEventType.X2ButtonUp:
                        button = MouseButtons.XButton2;
                        break;
                }
                RawInputMouseUp(e.RootElement, e.Timestamp, button, e.InputModifiers);
                break;
        }
    }

    private void RawInputMouseUp(IInputElement rootElement, uint timestamp, MouseButtons button,
        InputModifiers inputModifiers)
    {
        if (FocusManager.Focused != null)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(this, button, MouseButtonState.Relesed, inputModifiers, timestamp);
            args.RoutedEvent = Mouse.RawMouseUpEvent;
            FocusManager.Focused.RaiseEvent(args);
        }
    }

    private void RawInputMouseDown(IInputElement rootElement, Vector2 p, uint timestamp, MouseButtons button,
        InputModifiers inputModifiers)
    {
        if (FocusManager.Focused != null)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(this, button, MouseButtonState.Relesed, inputModifiers, timestamp);
            args.RoutedEvent = Mouse.RawMouseDownEvent;
            FocusManager.Focused.RaiseEvent(args);
        }
    }

    private void RawMouseEvent(IInputElement element, Vector2 delta, InputModifiers modifiers, uint timestamp)
    {
        if (FocusManager.Focused != null)
        {
            UnboundMouseEventArgs args = new UnboundMouseEventArgs(this, delta, modifiers, timestamp)
                { RoutedEvent = Mouse.RawMouseMoveEvent };
            FocusManager.Focused.RaiseEvent(args);
        }
    }

    private void MouseDoubleClick(IInputElement rootElement, Vector2 p, uint timestamp, MouseButtons button, InputModifiers inputModifiers)
    {
        var hit = rootElement.HitTest(p);
        if (hit != null)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(this, button, GetState(button), inputModifiers, timestamp);
            args.RoutedEvent = Mouse.PreviewMouseDoubleClickEvent;

            hit.RaiseEvent(args);

            args.RoutedEvent = Mouse.PreviewMouseDownEvent;
            hit.RaiseEvent(args);

            args.RoutedEvent = Mouse.MouseDoubleClickEvent;
            hit.RaiseEvent(args);

            args.RoutedEvent = Mouse.MouseDownEvent;
            hit.RaiseEvent(args);
        }
    }

    private void LeaveWindow(IInputElement rootElement, Vector2 p, InputModifiers inputModifiers, uint timestamp)
    {
        MouseEventArgs args = new MouseEventArgs(this, inputModifiers, timestamp)
        {
            RoutedEvent = Mouse.MouseLeaveEvent
        };
        rootElement.RaiseEvent(args);
        DirectlyOver = null;
    }

    private void MouseMove(IInputElement rootElement, Vector2 p, InputModifiers inputModifiers, uint timestamp)
    {
        IInputElement source = null;

        if (Captured == null)
        {
            source = SetMouseOver(rootElement, p, inputModifiers, timestamp);
        }
        else
        {
            var element = Captured.HitTest(p);
            SetMouseOver(rootElement, element, inputModifiers, timestamp);
            source = Captured;
        }
        var args = new MouseEventArgs(this, inputModifiers, timestamp) { RoutedEvent = Mouse.PreviewMouseMoveEvent };
        source.RaiseEvent(args);
        args.RoutedEvent = Mouse.MouseMoveEvent;
        source.RaiseEvent(args);
    }

    private IInputElement SetMouseOver(IInputElement rootElement, Vector2 p, InputModifiers modifiers, uint timestamp)
    {
        var element = rootElement.HitTest(p);
        return SetMouseOver(rootElement, element, modifiers, timestamp);
    }

    private IInputElement SetMouseOver(IInputElement root, IInputElement element, InputModifiers modifiers, uint timestamp)
    {
        if (element != null)
        {
            if (DirectlyOver != element)
            {
                MouseEventArgs args = new MouseEventArgs(this, modifiers, timestamp)
                {
                    RoutedEvent = Mouse.MouseLeaveEvent
                };
                if (DirectlyOver != root && DirectlyOver != null)
                {
                    if (DirectlyOver.IsMouseOver)
                    {
                        DirectlyOver.RaiseEvent(args);
                    }
                }

                args.RoutedEvent = Mouse.MouseEnterEvent;

                if (!element.IsMouseOver)
                {
                    element.RaiseEvent(args);
                }
            }
        }

        DirectlyOver = element ?? root;
        return DirectlyOver;
    }

    private void MouseDown(IInputElement rootElement, Vector2 p, uint timestamp, MouseButtons button, InputModifiers inputModifiers)
    {
        var hit = rootElement.HitTest(p);

        if (hit != null)
        {
            if (lastClickTime - timestamp > PlatformSettings.DoubleClickTime || lastClickedElement != hit)
            {
                clickCount = 0;
            }
            clickCount++;
            lastClickTime = timestamp;
            lastClickedElement = hit;

            MouseButtonEventArgs eventArgs = new MouseButtonEventArgs(this, button, GetState(button), inputModifiers, timestamp)
            {
                RoutedEvent = Mouse.PreviewMouseDownEvent,
                ClickCount = clickCount,
            };
            hit.RaiseEvent(eventArgs);

            eventArgs.RoutedEvent = Mouse.MouseDownEvent;
            hit.RaiseEvent(eventArgs);
        }
    }

    private void MouseUp(IInputElement rootElement, Vector2 p, uint timestamp, MouseButtons button, InputModifiers inputModifiers)
    {
        var hit = rootElement.HitTest(p);

        if (hit != null)
        {
            MouseButtonEventArgs eventArgs = new MouseButtonEventArgs(this, button, GetState(button), inputModifiers, timestamp)
            {
                RoutedEvent = Mouse.PreviewMouseUpEvent,
                ClickCount = 1,
            };
            hit.RaiseEvent(eventArgs);

            eventArgs.RoutedEvent = Mouse.MouseUpEvent;
            hit.RaiseEvent(eventArgs);
        }
    }

    private MouseButtonState GetState(MouseButtons button)
    {
        if (button == MouseButtons.Left)
        {
            return LeftButton;
        }
        else if (button == MouseButtons.Right)
        {
            return RightButton;
        }
        else if (button == MouseButtons.Middle)
        {
            return MiddleButton;
        }
        else if (button == MouseButtons.XButton1)
        {
            return XButton1;
        }
        else
        {
            return XButton2;
        }
    }

    private void MouseWheel(IInputElement rootElement, Vector2 p, InputModifiers modifiers, uint timestemp, Int32 wheelDelta)
    {
        var hit = rootElement.HitTest(p);

        hit?.RaiseEvent(new MouseWheelEventArgs(this, modifiers, wheelDelta, timestemp) { RoutedEvent = Mouse.MouseWheelEvent });
    }
}