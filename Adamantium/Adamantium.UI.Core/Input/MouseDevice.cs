using Adamantium.Mathematics;
using Adamantium.UI.Core.Input.Raw;

namespace Adamantium.UI.Core.Input;

public class MouseDevice
{
    private MouseDevice()
    {
    }

    private int clickCount = 0;
    private uint lastClickTime = 0;
    private IInputComponent lastClickedComponent = null;
    private Vector2 lastClickPosition;

    public IInputComponent Captured { get; protected set; }
    public IInputComponent DirectlyOver { get; private set; }

    public MouseButtonState LeftButton { get; private set; }
    public MouseButtonState RightButton { get; private set; }
    public MouseButtonState MiddleButton { get; private set; }
    public MouseButtonState XButton1 { get; private set; }
    public MouseButtonState XButton2 { get; private set; }

    public Cursor OverrideCursor { get; set; }

    private PixelPoint Position;

    // The window Position was measured against: popup-overlay content has no visual path to a window.
    private IInputComponent _positionRoot;

    // The window the OS says the pointer is over: hover is recomputed from the screen position, which cannot tell that
    // another window covers the point.
    private IInputComponent _hoverRoot;

    private static MouseDevice currentDevice;

    public static MouseDevice CurrentDevice => currentDevice ??= new MouseDevice();

    /// <summary>Captures the mouse for <paramref name="component"/>; null releases it. Raises Lost/GotMouseCapture, so a
    /// control learns when the OS revoked its capture.</summary>
    public bool Capture(IInputComponent component)
    {
        var previous = Captured;
        if (ReferenceEquals(previous, component)) return component != null;
        Captured = component;

        previous?.RaiseEvent(new MouseEventArgs(this, InputModifiers.None, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        component?.RaiseEvent(new MouseEventArgs(this, InputModifiers.None, 0) { RoutedEvent = Mouse.GotMouseCaptureEvent });
        return component != null;
    }

    /// <summary>
    /// Mirrors <see cref="Captured"/> to the OS after a mouse event, so a drag keeps its moves and release off the window.
    /// <paramref name="osCaptured"/> is the worker's own flag; the call fires only on a transition.
    /// </summary>
    public void SyncOsMouseCapture(IWindowWorkerService worker, ref bool osCaptured)
    {
        var want = Captured != null;
        if (want == osCaptured) return;

        osCaptured = want;
        worker.SetMouseCapture(want);
    }

    public Vector2 GetPosition(IInputComponent relativeTo)
    {
        IUIComponent root = relativeTo;
        while (root.VisualParent != null)
        {
            root = root.VisualParent;
        }

        // To logical client coords first, then the logical offsets: mixing physical and logical units broke off 100% DPI.
        // A popup's root is not a window, so the window Position was measured against converts it.
        var clientRoot = root is IWindow ? root : ((_positionRoot as IUIComponent) ?? root);
        var p = clientRoot.PointToClient(Position);

        // Under a render transform the offsets no longer say where the element is: invert the composed transform.
        if (Transformed(relativeTo))
        {
            var local = Vector3F.TransformCoordinate(new Vector3F((float)p.X, (float)p.Y, 0),
                Matrix4x4F.Invert(relativeTo.WorldTransform));

            return new Vector2(local.X, local.Y);
        }

        IUIComponent v = relativeTo;
        while (v != null)
        {
            p -= v.Bounds.Location;
            v = v.VisualParent;
        }

        return p;
    }

    private static bool Transformed(IUIComponent of)
    {
        for (var at = of; at != null; at = at.VisualParent)
        {
            if (at.RenderTransform != null) return true;
        }

        return false;
    }

    /// <summary>
    /// Feeds the pointer position from an OS-driven drag, where another application owns the mouse and no move reaches
    /// us. Call on the UI loop thread.
    /// </summary>
    public void SetExternalPosition(IInputComponent root, PixelPoint screenPoint)
    {
        Position = screenPoint;
        _positionRoot = root;
    }

    public void SetCursor(Cursor cursor)
    {
        Cursor.Platform?.Apply(cursor);
    }

    public void UpdateCursor()
    {

    }

    /// <summary>Where the pointer is on the desktop: the live OS position when a platform is registered, else the
    /// last point an input event carried.</summary>
    public PixelPoint GetScreenPosition() => Mouse.Platform?.Position ?? Position;

    internal void UpdateButtonStates(InputModifiers buttons)
    {
        LeftButton = (buttons & InputModifiers.LeftMouseButton) != 0 ? MouseButtonState.Pressed : MouseButtonState.Released;
        RightButton = (buttons & InputModifiers.RightMouseButton) != 0 ? MouseButtonState.Pressed : MouseButtonState.Released;
        MiddleButton = (buttons & InputModifiers.MiddleMouseButton) != 0 ? MouseButtonState.Pressed : MouseButtonState.Released;
        XButton1 = (buttons & InputModifiers.X1MouseButton) != 0 ? MouseButtonState.Pressed : MouseButtonState.Released;
        XButton2 = (buttons & InputModifiers.X2MouseButton) != 0 ? MouseButtonState.Pressed : MouseButtonState.Released;
    }

    public void ProcessEvent(RawMouseEventArgs e)
    {
        Position = e.RootComponent.PointToScreen(e.Position);
        _positionRoot = e.RootComponent;
        UpdateButtonStates(e.InputModifiers);
        var button = MouseButtons.None;
        switch (e.EventType)
        {
            case RawMouseEventType.MouseMove:
                _hoverRoot = e.RootComponent;   // the OS routed this move here
                MouseMove(e.RootComponent, e.Position, e.InputModifiers, e.Timestamp);
                break;
            case RawMouseEventType.LeaveWindow:
                LeaveWindow(e.RootComponent, e.Position, e.InputModifiers, e.Timestamp);
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
                MouseDown(e.RootComponent, e.Position, e.Timestamp, button, e.InputModifiers);
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
                MouseUp(e.RootComponent, e.Position, e.Timestamp, button, e.InputModifiers);
                break;
            case RawMouseEventType.MouseWheel:
                var wheel = (RawMouseWheelEventArgs)e;
                MouseWheel(e.RootComponent, e.Position, e.InputModifiers, e.Timestamp, wheel.WheelDelta, wheel.IsHorizontal);
                break;
            case RawMouseEventType.RawMouseMove:
                var args = (RawInputMouseEventArgs)e;
                RawMouseEvent(args.RootComponent, args.Delta, args.InputModifiers, args.Timestamp);
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
                RawInputMouseDown(e.RootComponent, e.Position, e.Timestamp, button, e.InputModifiers);
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
                RawInputMouseUp(e.RootComponent, e.Timestamp, button, e.InputModifiers);
                break;
        }
    }

    private void RawInputMouseUp(IInputComponent rootComponent, uint timestamp, MouseButtons button,
        InputModifiers inputModifiers)
    {
        if (FocusManager.Focused != null)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(this, button, MouseButtonState.Released, inputModifiers, timestamp);
            args.RoutedEvent = Mouse.RawMouseUpEvent;
            FocusManager.Focused.RaiseEvent(args);
        }
    }

    private void RawInputMouseDown(IInputComponent rootComponent, Vector2 p, uint timestamp, MouseButtons button,
        InputModifiers inputModifiers)
    {
        if (FocusManager.Focused != null)
        {
            MouseButtonEventArgs args = new MouseButtonEventArgs(this, button, MouseButtonState.Released, inputModifiers, timestamp);
            args.RoutedEvent = Mouse.RawMouseDownEvent;
            FocusManager.Focused.RaiseEvent(args);
        }
    }

    private void RawMouseEvent(IInputComponent component, Vector2 delta, InputModifiers modifiers, uint timestamp)
    {
        if (FocusManager.Focused != null)
        {
            UnboundMouseEventArgs args = new UnboundMouseEventArgs(this, delta, modifiers, timestamp)
                { RoutedEvent = Mouse.RawMouseMoveEvent };
            FocusManager.Focused.RaiseEvent(args);
        }
    }

    // Open popups first, newest on top, then the window content. boundsForContent: false is the pixel-accurate click
    // hit; true is bounds containment for the hover chain, so a transparent gap does not drop its container from it.
    private static IInputComponent HitTestTopmost(IInputComponent rootComponent, Vector2 p, bool boundsForContent = false)
    {
        if (rootComponent is IWindow window)
        {
            var popups = window.PopupRoots;
            for (var i = popups.Count - 1; i >= 0; i--)
            {
                // A popup that is no target absorbs nothing: a tooltip under the pointer took the click from its control.
                if (!popups[i].IsHitTestVisible) continue;

                if (InputExtensions.HitTest(popups[i], p) is { } popupHit)
                    return popupHit;
                // Its own card absorbs the click, so nothing behind the popup gets it.
                if (popups[i].ClipRectangle.Contains(p))
                    return popups[i] as IInputComponent;
            }
        }

        var pixel = InputExtensions.HitTest(rootComponent, p);
        if (!boundsForContent) return pixel;

        // The pixel hit, unless it fell through a transparent gap to an ancestor of the bounds hit.
        var bounds = InputExtensions.HitTestBounds(rootComponent, p);
        if (pixel == null) return bounds;
        if (bounds != null && !ReferenceEquals(pixel, bounds) && IsAncestorOf(pixel, bounds)) return bounds;
        return pixel;
    }

    private static bool IsAncestorOf(IInputComponent ancestor, IInputComponent node)
    {
        var parent = (node as IUIComponent)?.VisualParent;
        while (parent != null)
        {
            if (ReferenceEquals(parent, ancestor)) return true;
            parent = parent.VisualParent;
        }
        return false;
    }

    private void LeaveWindow(IInputComponent rootComponent, Vector2 p, InputModifiers inputModifiers, uint timestamp)
    {
        // The whole hovered chain leaves, not just the root.
        if (ReferenceEquals(_hoverRoot, rootComponent)) _hoverRoot = null;
        AncestorState.Transition(DirectlyOver, null, Mouse.MouseEnterEvent, Mouse.MouseLeaveEvent,
            evt => new MouseEventArgs(this, inputModifiers, timestamp) { RoutedEvent = evt });
        ChangeDirectlyOver(null, inputModifiers, timestamp);
    }

    private void ChangeDirectlyOver(IInputComponent newOver, InputModifiers modifiers, uint timestamp)
    {
        var previous = DirectlyOver;
        if (ReferenceEquals(previous, newOver))
        {
            return;
        }

        DirectlyOver = newOver;
        previous?.RaiseEvent(new MouseEventArgs(this, modifiers, timestamp) { RoutedEvent = Mouse.DirectlyOverLeaveEvent });
        newOver?.RaiseEvent(new MouseEventArgs(this, modifiers, timestamp) { RoutedEvent = Mouse.DirectlyOverEnterEvent });
    }

    private void MouseMove(IInputComponent rootComponent, Vector2 p, InputModifiers inputModifiers, uint timestamp)
    {
        IInputComponent source = null;

        if (Captured == null)
        {
            source = SetMouseOver(rootComponent, p, inputModifiers, timestamp);
        }
        else
        {
            // The captured element stays the hover target off its bounds, or every ancestor's IsMouseOver drops mid-drag.
            var element = Captured.HitTest(p) ?? Captured;
            SetMouseOver(rootComponent, element, inputModifiers, timestamp);
            source = Captured;
        }
        var args = new MouseEventArgs(this, inputModifiers, timestamp) { RoutedEvent = Mouse.PreviewMouseMoveEvent };
        source.RaiseEvent(args);
        args.RoutedEvent = Mouse.MouseMoveEvent;
        source.RaiseEvent(args);
    }

    /// <summary>Re-decides hover when the content under a still pointer has moved (a list scrolled by keyboard or wheel).
    /// Call once layout has settled; ignored while something holds the capture.</summary>
    public void RefreshMouseOver(IInputComponent root)
    {
        if (Captured != null || root is not IRootVisualComponent client) return;
        // Only the window the pointer is over, or hover bleeds through from a window behind.
        if (!ReferenceEquals(root, _hoverRoot)) return;

        SetMouseOver(root, client.PointToClient(Position), InputModifiers.None, 0);
    }

    private IInputComponent SetMouseOver(IInputComponent rootComponent, Vector2 p, InputModifiers modifiers, uint timestamp)
    {
        var element = HitTestTopmost(rootComponent, p, boundsForContent: true);
        return SetMouseOver(rootComponent, element, modifiers, timestamp);
    }

    // MouseEnter/Leave are Direct events, raised along the whole ancestor chain (WPF IsMouseOver semantics), so a
    // templated control sees its own IsMouseOver change when a part is hit.
    private IInputComponent SetMouseOver(IInputComponent root, IInputComponent component, InputModifiers modifiers, uint timestamp)
    {
        var newOver = component ?? root;
        AncestorState.Transition(DirectlyOver, newOver, Mouse.MouseEnterEvent, Mouse.MouseLeaveEvent,
            evt => new MouseEventArgs(this, modifiers, timestamp) { RoutedEvent = evt });
        ChangeDirectlyOver(newOver, modifiers, timestamp);
        return DirectlyOver;
    }

    private void MouseDown(IInputComponent rootComponent, Vector2 p, uint timestamp, MouseButtons button, InputModifiers inputModifiers)
    {
        var hit = Captured ?? HitTestTopmost(rootComponent, p);

        if (hit != null)
        {
            // timestamp - lastClickTime, not the reverse: uint wraps. And the second click must land in the same place.
            var box = PlatformSettings.DoubleClickSize;
            var moved = Math.Abs(p.X - lastClickPosition.X) > box.Width ||
                        Math.Abs(p.Y - lastClickPosition.Y) > box.Height;

            if (timestamp - lastClickTime > PlatformSettings.DoubleClickTime || lastClickedComponent != hit || moved)
            {
                clickCount = 0;
            }
            clickCount++;
            lastClickTime = timestamp;
            lastClickedComponent = hit;
            lastClickPosition = p;

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

    private void MouseUp(IInputComponent rootComponent, Vector2 p, uint timestamp, MouseButtons button, InputModifiers inputModifiers)
    {
        var hit = Captured ?? HitTestTopmost(rootComponent, p);

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

    private void MouseWheel(IInputComponent rootComponent, Vector2 p, InputModifiers modifiers, uint timestemp, Int32 wheelDelta, bool isHorizontal = false)
    {
        var hit = HitTestTopmost(rootComponent, p);
        if (hit == null) return;

        // One args object, so a handled Preview suppresses the bubble.
        var args = new MouseWheelEventArgs(this, modifiers, wheelDelta, timestemp, isHorizontal) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
        hit.RaiseEvent(args);
        args.RoutedEvent = Mouse.MouseWheelEvent;
        hit.RaiseEvent(args);
    }
}
