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

    public IInputComponent Captured { get; protected set; }
    public IInputComponent DirectlyOver { get; private set; }

    public MouseButtonState LeftButton { get; private set; }
    public MouseButtonState RightButton { get; private set; }
    public MouseButtonState MiddleButton { get; private set; }
    public MouseButtonState XButton1 { get; private set; }
    public MouseButtonState XButton2 { get; private set; }

    public Cursor OverrideCursor { get; set; }

    private Vector2 Position;

    // The window the current Position was measured against (set on every raw event). GetPosition falls back to it when the
    // relativeTo element has NO VisualParent path to a window - i.e. it lives on a popup overlay (a detached logical child).
    private IInputComponent _positionRoot;

    /// <summary>The window the pointer is actually OVER, as the OS sees it: the platform delivers a move to the topmost
    /// window under the cursor and a LeaveWindow when it goes. Needed because hover is recomputed geometrically from the
    /// SCREEN position (see RefreshMouseOver), which on its own cannot tell that another, opaque window is covering the
    /// point - so a background window kept lighting up controls under a window in front of it. Clicks never had the
    /// problem: those arrive already addressed to a window by the OS.</summary>
    private IInputComponent _hoverRoot;

    private static MouseDevice currentDevice;
        
    public static MouseDevice CurrentDevice => currentDevice ??= new MouseDevice();

    public bool Capture(IInputComponent component)
    {
        // A null component releases the current capture. Routing (MouseMove/MouseDown/MouseUp) honours Captured, so a
        // control that captures on press keeps receiving move/up even when the pointer leaves it.
        var previous = Captured;
        if (ReferenceEquals(previous, component)) return component != null;
        Captured = component;

        // Raise Lost/GotMouseCapture so a captured control learns its capture went away - whether we released it or the OS
        // revoked it (WM_CAPTURECHANGED: another app, a screenshot overlay, Alt-Tab). Without this a drag that relied on
        // capture could hang forever when capture was yanked from under it.
        previous?.RaiseEvent(new MouseEventArgs(this, InputModifiers.None, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        component?.RaiseEvent(new MouseEventArgs(this, InputModifiers.None, 0) { RoutedEvent = Mouse.GotMouseCaptureEvent });
        return component != null;
    }

    /// <summary>
    /// Mirror the app's mouse capture (<see cref="Captured"/>) to the OS for the given window worker. A platform window
    /// worker calls this AFTER processing a mouse event; the platform-specific acquire/release is the worker's
    /// <see cref="IWindowWorkerService.SetMouseCapture"/>. Shared here so every platform gets the same behaviour -
    /// without OS capture the window stops receiving move/up once the pointer leaves it, freezing a drag and dropping
    /// the off-window release. <paramref name="osCaptured"/> is the worker's own "currently holds OS capture" flag,
    /// updated here so the call only fires on a transition.
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
        // Walk up to the root (window) that owns the screen<->client transform.
        IUIComponent root = relativeTo;
        while (root.VisualParent != null)
        {
            root = root.VisualParent;
        }

        // Convert the PHYSICAL screen position to LOGICAL client coords FIRST (PointToClient divides by the DPI scale),
        // THEN subtract the elements' logical Bounds.Location down the chain - all in the same DIP space. The old order
        // subtracted logical offsets straight off the physical screen position and converted after, mixing units: correct
        // only at 100% DPI, and increasingly wrong (by the DPI scale) the further an element sits from the window origin -
        // which made a picker on a scaled second monitor unusable.
        // Popup-overlay content is a DETACHED logical child (no VisualParent path to the window), so the walk above stops at
        // the overlay root, NOT the window. Use the window the Position was measured against for the screen->client + DPI
        // conversion; the overlay child's Bounds.Location is already window-space, so the offset walk below stays correct.
        // Without this, GetPosition inside a popup is garbage and drag controls (ColorPicker / Slider / ColorWheel) die there.
        var clientRoot = root is IWindow ? root : ((_positionRoot as IUIComponent) ?? root);
        var p = clientRoot.PointToClient(Position);
        IUIComponent v = relativeTo;
        while (v != null)
        {
            p -= v.Bounds.Location;
            v = v.VisualParent;
        }

        return p;
    }

    /// <summary>
    /// Feed the pointer position from an OS-DRIVEN drag (a native drop target's DragOver). During such a drag the drag
    /// SOURCE - another application - owns the mouse, so no move message reaches us and <see cref="Position"/> would
    /// stay where the pointer last was: every <see cref="GetPosition"/> in the drop machinery (insertion caret,
    /// auto-scroll, hit-test) would read a stale point. The native callback knows the real screen point, so it publishes
    /// it here and the whole drag-over path keeps working unchanged. Call on the UI loop thread.
    /// </summary>
    public void SetExternalPosition(IInputComponent root, Vector2 screenPoint)
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

    /// <summary>The pointer in PHYSICAL screen coordinates: the live OS position when a platform is registered, else the
    /// last point an input event carried.</summary>
    public Vector2 GetScreenPosition() => Mouse.Platform?.Position ?? Position;

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
        _positionRoot = e.RootComponent;   // remembered for GetPosition on detached popup-overlay content
        UpdateButtonStates(e.InputModifiers);
        var button = MouseButtons.None;
        switch (e.EventType)
        {
            case RawMouseEventType.MouseMove:
                _hoverRoot = e.RootComponent;   // the OS routed this move here, so this window is the one under the cursor
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
                MouseDoubleClick(e.RootComponent, e.Position, e.Timestamp, button, e.InputModifiers);
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

    // Hit-test the OPEN POPUPS first (they render - and so receive input - above the main content, newest on top), then
    // fall through to the window content. Without this a popup's contents (a DropDown list, a menu) are click-through.
    // boundsForContent=false (click routing): pixel-accurate hit - a shape's real geometry, a panel's visible background -
    // so a click in a transparent gap falls through to what's really behind. true (the mouse-over chain): bounds
    // containment - the pointer over a gap between transparent tiles is still geometrically inside its container
    // (a ScrollViewer), so IsMouseOver must NOT fall through to an ANCESTOR's background, which would drop the container
    // from the over-chain and restart its auto-hide fade every few frames (the hover FPS drop on a big grid).
    private static IInputComponent HitTestTopmost(IInputComponent rootComponent, Vector2 p, bool boundsForContent = false)
    {
        if (rootComponent is IWindow window)
        {
            // A popup ROOT is usually a plain container (a Border) that is NOT itself an IInputComponent - but its
            // children (the list items) are. Hit-test the root as an IUIComponent so the walk descends into them.
            var popups = window.PopupRoots;
            for (var i = popups.Count - 1; i >= 0; i--)
            {
                if (InputExtensions.HitTest(popups[i], p) is { } popupHit)
                    return popupHit;
                // Click landed on the popup's own OPAQUE card, not one of its child controls (a SlidePanel/menu root is a
                // Border, which HitTest lets clicks fall through - fine for content, WRONG for an overlay). A panel/menu is
                // opaque to the mouse: ABSORB the click within its bounds so it can't reach the window content behind it
                // (e.g. the title bar's close button showing through the diagnostics SlidePanel). Returning the root cast to
                // IInputComponent (may be null = nothing routed) still stops the fall-through to the content below.
                if (popups[i].ClipRectangle.Contains(p))
                    return popups[i] as IInputComponent;
            }
        }
        
        var pixel = InputExtensions.HitTest(rootComponent, p);
        if (!boundsForContent) return pixel;

        // Mouse-over (boundsForContent): PREFER the pixel-accurate hit so a real control keeps precise hover + click (a
        // CheckBox/RadioButton/Button whose IsMouseOver drives its hover trigger and its release-click gate). Fall back to
        // the geometric bounds hit ONLY when the pixel hit fell THROUGH to an ANCESTOR of it - i.e. the pointer sits in a
        // transparent gap (between tiles) and pixel-testing resolved to the container behind them; there the bounds hit is
        // the more specific content element, so taking it keeps the over-chain anchored (no ScrollViewer IsMouseOver
        // flicker / auto-hide restart). Blanket-geometric (the previous behaviour) broke controls: it returned transparent
        // overlay/descendant parts a pixel test skips, mis-driving their IsMouseOver-based hover + click.
        var bounds = InputExtensions.HitTestBounds(rootComponent, p);
        if (pixel == null) return bounds;
        if (bounds != null && !ReferenceEquals(pixel, bounds) && IsAncestorOf(pixel, bounds)) return bounds;
        return pixel;
    }

    // Is <paramref name="ancestor"/> a strict visual ancestor of <paramref name="node"/>? Walks the visual parent chain.
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

    private void MouseDoubleClick(IInputComponent rootComponent, Vector2 p, uint timestamp, MouseButtons button, InputModifiers inputModifiers)
    {
        var hit = HitTestTopmost(rootComponent, p);
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

    private void LeaveWindow(IInputComponent rootComponent, Vector2 p, InputModifiers inputModifiers, uint timestamp)
    {
        // Mouse left the window entirely: everything in the hovered chain leaves, nothing enters - so IsMouseOver is
        // cleared along the whole chain, not just the root (where it used to stick on inner elements).
        if (ReferenceEquals(_hoverRoot, rootComponent)) _hoverRoot = null;
        AncestorState.Transition(DirectlyOver, null, Mouse.MouseEnterEvent, Mouse.MouseLeaveEvent,
            evt => new MouseEventArgs(this, inputModifiers, timestamp) { RoutedEvent = evt });
        DirectlyOver = null;
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
            // The captured element stays the mouse-over target even when the pointer wanders off it (dragging a thumb
            // past its own bounds, a fast drag outrunning the thumb): hit-test within the capture, but fall back to the
            // captured element itself. Without the fallback HitTest returns null once the pointer leaves the thumb, the
            // over-chain collapses to the root, and every ancestor (a ScrollViewer's IsMouseOver, a Button's hover, ...)
            // spuriously goes false mid-drag - which made an overlay scrollbar fade out from under the dragging cursor.
            var element = Captured.HitTest(p) ?? Captured;
            SetMouseOver(rootComponent, element, inputModifiers, timestamp);
            source = Captured;
        }
        var args = new MouseEventArgs(this, inputModifiers, timestamp) { RoutedEvent = Mouse.PreviewMouseMoveEvent };
        source.RaiseEvent(args);
        args.RoutedEvent = Mouse.MouseMoveEvent;
        source.RaiseEvent(args);
    }

    /// <summary>Re-decide what the pointer is over when the CONTENT under it has moved, not the pointer. Hover says what
    /// is under the cursor NOW, and a list scrolled by the keyboard or the wheel slides its rows under a cursor that
    /// never moves: no Enter and no Leave arrive, so the highlight stays on the row that has left - and a recycled
    /// container carries it off to a row nobody is pointing at. Called once a layout pass has SETTLED, which is exactly
    /// when the answer can have changed.
    /// <para>Ignored while something holds the capture: there the captured element is the mouse-over target by
    /// definition (a thumb being dragged past its own bounds), and re-deciding would take that away mid-gesture.</para></summary>
    public void RefreshMouseOver(IInputComponent root)
    {
        if (Captured != null || root is not IRootVisualComponent client) return;
        // Only the window the pointer is genuinely over may re-evaluate hover. This runs on every layout update, from
        // EVERY window, and works from the screen position alone - so without this gate a window behind an opaque one
        // still found the cursor inside its own bounds and highlighted whatever sat under it. Hover bled through; the
        // click did not, which is exactly how it looked.
        if (!ReferenceEquals(root, _hoverRoot)) return;

        SetMouseOver(root, client.PointToClient(Position), InputModifiers.None, 0);
    }

    private IInputComponent SetMouseOver(IInputComponent rootComponent, Vector2 p, InputModifiers modifiers, uint timestamp)
    {
        // Mouse-over is GEOMETRIC: bounds containment, so the over-chain doesn't fall through a transparent gap to an
        // ancestor's background (see HitTestTopmost). Click routing (MouseDown/etc.) still uses the pixel-accurate hit.
        var element = HitTestTopmost(rootComponent, p, boundsForContent: true);
        return SetMouseOver(rootComponent, element, modifiers, timestamp);
    }

    // IsMouseOver is true for an element when the pointer is over it OR any of its visual descendants (WPF semantics).
    // MouseEnter/MouseLeave are Direct routed events, so they must be raised individually along the ancestor chain - not
    // just on the single deepest hit element. Otherwise a control whose hit target is one of its own template parts
    // (e.g. a templated Button) never sees its own IsMouseOver change, so element-level triggers on it never fire while
    // template/part triggers do. AncestorState does the chain diff (reused by the other "...Within"/"...Over" states).
    private IInputComponent SetMouseOver(IInputComponent root, IInputComponent component, InputModifiers modifiers, uint timestamp)
    {
        var newOver = component ?? root;
        AncestorState.Transition(DirectlyOver, newOver, Mouse.MouseEnterEvent, Mouse.MouseLeaveEvent,
            evt => new MouseEventArgs(this, modifiers, timestamp) { RoutedEvent = evt });
        DirectlyOver = newOver;
        return DirectlyOver;
    }

    private void MouseDown(IInputComponent rootComponent, Vector2 p, uint timestamp, MouseButtons button, InputModifiers inputModifiers)
    {
        var hit = Captured ?? HitTestTopmost(rootComponent, p);

        if (hit != null)
        {
            // Elapsed since the previous click = timestamp - lastClickTime (NOT the reverse: both are uint, so the wrong
            // order underflows to a huge value and reset ALWAYS fired, pinning clickCount at 1 - double-clicks never
            // registered anywhere). uint subtraction also wraps correctly across the GetMessageTime rollover.
            if (timestamp - lastClickTime > PlatformSettings.DoubleClickTime || lastClickedComponent != hit)
            {
                clickCount = 0;
            }
            clickCount++;
            lastClickTime = timestamp;
            lastClickedComponent = hit;

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

        // Tunnel (Preview) THEN bubble (Main) on ONE args object, exactly as MouseDown does, so a PreviewMouseWheel
        // handler's Handled carries into the bubbling MouseWheel and suppresses it. Previously only the bubble was raised,
        // so PreviewMouseWheel never fired at all.
        var args = new MouseWheelEventArgs(this, modifiers, wheelDelta, timestemp, isHorizontal) { RoutedEvent = Mouse.PreviewMouseWheelEvent };
        hit.RaiseEvent(args);
        args.RoutedEvent = Mouse.MouseWheelEvent;
        hit.RaiseEvent(args);
    }
}