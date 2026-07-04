using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Internals;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Input.Raw;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.Win32;
using Adamantium.Win32.RawInput;

namespace Adamantium.UI.Platforms.Windows;

internal class Win32WindowWorker : AdamantiumComponent, IWindowWorkerService
{
    //private WindowBase window;
    private IWindow window;
    private readonly Dictionary<uint, HandleMessage> messageTable;

    private bool isOverSizeFrame;
    private bool trackMouse;
    private bool osMouseCaptured;
    private InputModifiers lastRawMouseModifiers;
    private Win32NativeWindowWrapper source;

    static Win32WindowWorker()
    {
        // Declare the process Per-Monitor-DPI-Aware V2 before any window exists (this static ctor runs once, before the
        // first worker instance = before the first HWND). PMv2 => the OS stops bitmap-stretching our frames and sends
        // WM_DPICHANGED when the window crosses monitors; we scale the render ourselves (docs/PER_MONITOR_DPI_PLAN.md).
        try { Win32Interop.SetProcessDpiAwarenessContext(Win32Interop.DpiAwarenessContextPerMonitorAwareV2); }
        catch { /* pre-1703 OS without the API, or awareness already pinned by a manifest - ignore */ }

        RawInputDevice.RegisterDevice(HIDUsagePage.Generic, HIDUsageId.Mouse, InputDeviceFlags.None);
    }

    public Win32WindowWorker(IUIContext uiContext)
    {
        UIContext = uiContext;
        
        messageTable = new Dictionary<uint, HandleMessage>();
        messageTable[(uint)WindowMessages.Activate] = HandleActivate;
        messageTable[(uint)WindowMessages.Syscommand] = HandleSysCommand;
        messageTable[(uint)WindowMessages.Nclbuttondown] = HandleNcButtonDown;
        messageTable[(uint)WindowMessages.Nchittest] = HandleNcHittest;
        //messageTable[(uint)WindowMessages.Nccalcsize] = HandleNcCalcSize;
        messageTable[(uint)WindowMessages.Size] = HandleResize;
        messageTable[(uint)WindowMessages.Dpichanged] = HandleDpiChanged;
        messageTable[(uint)WindowMessages.Keydown] = HandleKeyDown;
        messageTable[(uint)WindowMessages.Syskeydown] = HandleKeyDown;
        messageTable[(uint)WindowMessages.Keyup] = HandleKeyUp;
        messageTable[(uint)WindowMessages.Syskeyup] = HandleKeyUp;
        messageTable[(uint)WindowMessages.Char] = HandleChar;
        messageTable[(uint)WindowMessages.Mousemove] = HandleMouseMove;
        messageTable[(uint)WindowMessages.Mouseleave] = HandleMouseLeave;
        messageTable[(uint)WindowMessages.Capturechanged] = HandleCaptureChanged;
        messageTable[(uint)WindowMessages.LeftButtondown] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.RightButtondown] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.MiddleButtondown] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.Xbuttondown] = HandleMouseLeftButtonDown;

        messageTable[(uint)WindowMessages.LeftButtonup] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.RightButtonup] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.MiddleButtonup] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.Xbuttonup] = HandleMouseLeftButtonDown;

        messageTable[(uint)WindowMessages.LeftButtondblclk] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.RightButtondblclk] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.MiddleButtondblclk] = HandleMouseLeftButtonDown;
        messageTable[(uint)WindowMessages.Xbuttondblclk] = HandleMouseLeftButtonDown;

        messageTable[(uint)WindowMessages.MouseWheel] = HandleMouseWheel;
        messageTable[(uint)WindowMessages.Input] = HandleRawInput;
        messageTable[(uint)WindowMessages.Setcursor] = HandleSetCursor;
    }

    public void SetWindow(IWindow window)
    {
        this.window = window;
        this.window.Closed += OnWindowClosed;
        var classStyle = WindowClassStyle.OwnDC | WindowClassStyle.DoubleClicks; //| WindowClassStyle.VerticalRedraw | WindowClassStyle.HorizontalRedraw;
        var wndStyleEx = WindowStyleEx.Appwindow | WindowStyleEx.Acceptfiles;
        var wndStyle = //WindowStyle.Popup |
            WindowStyle.Overlappedwindow | WindowStyle.Maximizebox | WindowStyle.Minimizebox |
            WindowStyle.Clipsiblings | WindowStyle.Clipchildren | WindowStyle.Sizeframe;
        source = new Win32NativeWindowWrapper(
            classStyle, 
            wndStyleEx, 
            wndStyle, 
            (int)window.Left,
            (int)window.Top, 
            (int)window.Width, 
            (int)window.Height, 
            IntPtr.Zero);
        this.window.SetHandle(source.Handle);
        
        if (source.Handle == IntPtr.Zero) 
            return;
        
        this.window.SetSurfaceHandle(source.Handle);
                
        source.AddHook(CustomWndProc);

        this.window.DpiScale = ReadDpiScale(source.Handle);   // initial per-monitor DPI (PMv2)

        Win32Interop.GetClientRect(window.Handle, out var client);
        // ClientWidth/Height are LOGICAL (DIP) = physical px / DPI scale (per-axis). The renderer sizes the swapchain
        // back up by RenderScale (= DpiScale); the projection + layout stay logical. On a 100% monitor this is identity.
        this.window.ClientWidth = client.Width / this.window.DpiScale.X;
        this.window.ClientHeight = client.Height / this.window.DpiScale.Y;

        UIContext.ThemeContext.ApplyCurrentTheme(this.window);
        UIContext.UIApplication.AddWindow(this.window);
                
        this.window.OnSourceInitialized();
        this.window.StateChanged += WindowOnStateChanged;
    }

    public void SetTitle(string title)
    {
        if (window == null) return;
        
        Win32Interop.SetWindowText(window.Handle, title);
    }

    public void ShowWindow(WindowState windowState)
    {
        var windowShowStyle = WindowShowStyle.Show;
        switch (windowState)
        {
            case WindowState.Maximized:
                windowShowStyle = WindowShowStyle.Maximize;
                break;
            case WindowState.Minimized:
                windowShowStyle = WindowShowStyle.Minimize;
                break;
        }
        Win32Interop.ShowWindow(source.Handle, windowShowStyle);
    }

    public void HideWindow()
    {
        Win32Interop.ShowWindow(source.Handle, WindowShowStyle.Hide);
    }

    public IUIContext UIContext { get; }

    private void WindowOnStateChanged(object sender, StateChangedEventArgs e)
    {
        Win32Interop.ShowWindow(window.Handle, ConvertStateToShowStyle(e.State));
    }

    private WindowShowStyle ConvertStateToShowStyle(WindowState state)
    {
        switch (state)
        {
            case WindowState.Normal:
                return WindowShowStyle.ShowNormal;
            case WindowState.Maximized:
                return WindowShowStyle.Maximize;
            case WindowState.Minimized:
                return WindowShowStyle.Minimize;
            default:
                return WindowShowStyle.ShowNormal;
        }
    }

    private void OnWindowClosed(object sender, EventArgs e)
    {
        source.RemoveHook(CustomWndProc);
        UIContext.UIApplication.RemoveWindow(window);
    }

    /// <summary>
    /// Window Procedure
    /// </summary>
    /// <param name="hWnd">windows handler</param>
    /// <param name="msg">window message (one of window messages)<see cref="WindowMessages"/>"/></param>
    /// <param name="wParam"></param>
    /// <param name="lParam"></param>
    /// <param name="handled"></param>
    /// <returns></returns>
    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (messageTable.TryGetValue(msg, out var handler))
        {
            return handler((WindowMessages)msg, wParam, lParam, out handled);
        }

        return IntPtr.Zero;
    }

    // Window input arrives on the OS message thread. Raising the event below routes to controls and mutates/invalidates
    // the visual tree, which must NOT happen concurrently with the measure/arrange/render running on the loop thread.
    // The event args are built synchronously by the caller (capturing the message's transient state); only the raising
    // is marshalled onto the loop thread (drained at the start of Update). Order is preserved (a single FIFO queue).
    private static void DispatchInput(Action raise)
    {
        var dispatcher = Threading.Dispatcher.CurrentDispatcher;
        if (dispatcher != null) dispatcher.Post(raise);
        else raise();
    }

    // OS mouse messages carry PHYSICAL client px; the layout / hit-test work in logical DIP. Divide by the window's DPI
    // scale so the tracked position + reported GetPosition are logical (identity at 100%). Screen coords stay physical.
    private Vector2 ToLogical(Vector2 physicalClient)
    {
        var dpi = window.DpiScale;
        return new Vector2(physicalClient.X / dpi.X, physicalClient.Y / dpi.Y);
    }


    private IntPtr HandleActivate(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var state = Messages.GetWindowActivationState(wParam);
        // Activation touches focus (FocusManager), the window's IsActive + the app's active-window - all loop-owned
        // managed state that must not be mutated straight off the OS message thread. Marshal it like the input handlers.
        switch (state)
        {
            case WindowActivation.Active:
            case WindowActivation.ClickActive:
                DispatchInput(HandleActivation);
                break;

            case WindowActivation.Inactive:
                DispatchInput(HandleDeactivation);
                break;
        }

        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleNcButtonDown(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var ht = (NcHitTest)(Environment.Is64BitProcess ? wParam.ToInt64() : wParam.ToInt32());
        window.StateChanged -= WindowOnStateChanged;
        switch (ht)
        {
            case NcHitTest.Close:
                window.Close();
                break;
            // case NcHitTest.Minbutton:
            //     window.State = WindowState.Minimized;
            //     break;
            // case NcHitTest.Maxbutton:
            //     window.State = WindowState.Maximized;
            //     break;
        }
        handled = true;
        window.StateChanged += WindowOnStateChanged;
        return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
    }

    private IntPtr HandleNcHittest(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var uHitTest = Win32Interop.DefWindowProc(window.Handle, WindowMessages.Nchittest, wParam, lParam);
        var result = (NcHitTest)(Environment.Is64BitProcess ? uHitTest.ToInt64() : uHitTest.ToInt32());
        isOverSizeFrame = result != NcHitTest.Client;
        handled = false;
        return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
    }

    private IntPtr HandleNcCalcSize(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = false;
        bool bValue = Convert.ToBoolean(wParam.ToInt32());
        NCCALCSIZE_PARAMS param = new NCCALCSIZE_PARAMS();
        RECT wRect;
        if (bValue)
        {
            param = Marshal.PtrToStructure<NCCALCSIZE_PARAMS>(lParam);
            wRect = param.rgrc[0];
        }
        else
        {
            wRect = Marshal.PtrToStructure<RECT>(lParam);
        }

        if (bValue)
        {
            wRect.Top = wRect.Top + 1;
            param.rgrc[1] = wRect;
            param.rgrc[0] = wRect;
            param.rgrc[0].Left += 7;
            param.rgrc[0].Right -= 7;
            param.rgrc[0].Bottom -= 7;

            Marshal.StructureToPtr(param, lParam, true);

            handled = true;
            return IntPtr.Zero;
        }
        return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
    }

    private WindowState lastWindowState;
    private IntPtr HandleSysCommand(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var command = (SystemCommands)(Environment.Is64BitProcess ? wParam.ToInt64() : wParam.ToInt32());
        var p = Messages.PointFromLParam(lParam);
        window.StateChanged -= WindowOnStateChanged;
        switch (command)
        {
            case SystemCommands.CLOSE:
                window.Close();
                if (window.IsClosed)
                {
                    source.Destroy();
                    window.SetHandle(IntPtr.Zero);
                }
                break;
            case SystemCommands.MOVE:
                Win32Interop.SetWindowPos(window.Handle, IntPtr.Zero, (int)p.X, (int)p.Y, (int)window.Width,
                    (int)window.Height, SetWindowPosFlags.Asyncwindowpos | SetWindowPosFlags.Nosize);
                break;
            case SystemCommands.MAXIMIZE:
                window.State = WindowState.Maximized;
                Win32Interop.ShowWindow(window.Handle, WindowShowStyle.Maximize);
                break;
            case SystemCommands.MINIMIZE:
                lastWindowState = window.State;
                window.State = WindowState.Minimized;
                Win32Interop.ShowWindow(window.Handle, WindowShowStyle.Minimize);
                break;
            case SystemCommands.RESTORE:
                if (lastWindowState == WindowState.Maximized && window.State == WindowState.Minimized)
                {
                    lastWindowState = WindowState.Maximized;
                }
                else
                {
                    lastWindowState = WindowState.Normal;
                }
                var style = ConvertStateToShowStyle(lastWindowState);
                window.State = lastWindowState;
                Win32Interop.ShowWindow(window.Handle, style);
                break;
            default:
                Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
                break;
        }
        window.StateChanged += WindowOnStateChanged;
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleResize(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = true;
        if (window.State == WindowState.Minimized)
        {
            return IntPtr.Zero;
        }

        // Read the new size synchronously (the message's transient state), but APPLY it on the UI loop thread via the
        // same dispatch input uses. The Width/ClientWidth setters fire ClientSizeChanged, which mutates the renderer's
        // viewport/scissor/projection + IsRendererUpToDate + re-runs layout - doing that straight off the OS message
        // thread races the render/layout loop (a non-volatile flag it may never see, a torn viewport vs swapchain), so
        // the picture stops tracking the window. Marshalled through the queue it runs in order at the next Update start.
        Win32Interop.GetWindowRect(window.Handle, out var rect);
        Win32Interop.GetClientRect(window.Handle, out var client);
        var w = rect.Width;
        var h = rect.Height;
        var cw = client.Width;   // physical client px
        var ch = client.Height;

        DispatchInput(() =>
        {
            window.Width = w;
            window.Height = h;
            // ClientWidth/Height logical (DIP) = physical / DPI. DpiScale is read on the loop thread (where it's updated
            // by the marshalled WM_DPICHANGED handler), so a DPI change that precedes this resize is already applied.
            window.ClientWidth = cw / window.DpiScale.X;
            window.ClientHeight = ch / window.DpiScale.Y;
        });

        return IntPtr.Zero;
    }

    // The window moved to a monitor with a different scale (or the scale changed). wParam packs the new DPI (LOWORD X /
    // HIWORD Y); lParam is the RECT the OS wants the window at, already sized for the new DPI. Apply that rect
    // synchronously here (a Win32 op that must run on the owning thread), but marshal the managed DpiScale update onto
    // the loop thread - it fires DpiChanged, which re-scales the renderer + re-lays-out, and must not race the loop.
    private IntPtr HandleDpiChanged(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = true;
        var packed = wParam.ToInt64();
        var dpiX = (uint)(packed & 0xFFFF);
        var dpiY = (uint)((packed >> 16) & 0xFFFF);

        // Update DpiScale (loop thread) BEFORE SetWindowPos: SetWindowPos synchronously fires WM_SIZE -> HandleResize,
        // which marshals ClientWidth = physical / DpiScale. Queuing the scale update ahead of it (one FIFO) makes that
        // divide use the NEW scale. Setting DpiScale also fires DpiChanged -> the renderer re-scales RenderScale.
        var scale = new Vector2(dpiX / 96.0, dpiY / 96.0);
        DispatchInput(() => window.DpiScale = scale);

        var r = Marshal.PtrToStructure<RECT>(lParam);
        Win32Interop.SetWindowPos(window.Handle, IntPtr.Zero, r.Left, r.Top, r.Width, r.Height,
            SetWindowPosFlags.Nozorder | SetWindowPosFlags.Noactivate);
        return IntPtr.Zero;
    }

    // The window's current-monitor DPI as a per-axis scale (1,1 = 96 DPI / 100%). Falls back to 1,1 if the query fails.
    private static Vector2 ReadDpiScale(IntPtr hwnd)
    {
        var monitor = Win32Interop.MonitorFromWindow(hwnd, Win32Interop.MonitorDefaultToNearest);
        if (Win32Interop.GetDpiForMonitor(monitor, Win32Interop.MdtEffectiveDpi, out var dpiX, out var dpiY) == 0 && dpiX > 0)
            return new Vector2(dpiX / 96.0, dpiY / 96.0);
        return new Vector2(1, 1);
    }

    private IntPtr HandleKeyDown(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        DispatchInput(() => KeyboardDevice.CurrentDevice.ProcessEvent(new RawKeyboardEventArgs((Key)Messages.GetKey(wParam),
            RawKeyboardEventType.KeyDown, lParam, KeyboardDevice.CurrentDevice.Modifiers, GetTimeStamp())));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleKeyUp(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        DispatchInput(() => KeyboardDevice.CurrentDevice.ProcessEvent(new RawKeyboardEventArgs((Key)Messages.GetKey(wParam),
            RawKeyboardEventType.KeyUp, lParam, KeyboardDevice.CurrentDevice.Modifiers, GetTimeStamp())));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleChar(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var text = Messages.GetChar(wParam);
        //Ignoring system keys
        if (text >= 32)
        {
            DispatchInput(() => KeyboardDevice.CurrentDevice.ProcessEvent(new RawTextInputEventArgs(text.ToString(),
                KeyboardDevice.CurrentDevice.Modifiers, GetTimeStamp())));
        }
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleMouseMove(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        if (!trackMouse)
        {
            var tm = new TRACKMOUSEEVENT
            {
                cbSize = Marshal.SizeOf(typeof(TRACKMOUSEEVENT)),
                dwFlags = 2, // we are tracking only MouseLease event
                hwndTrack = window.Handle,
                dwHoverTime = 0,
            };
            trackMouse = true;
            Win32Interop.TrackMouseEvent(ref tm);
        }
        var eventArgs = new RawMouseEventArgs(RawMouseEventType.MouseMove, window, ToLogical(Messages.PointFromLParam(lParam)),
            WindowsMouseDeviceExtension.GetKeyModifiers(windowMessage, wParam), MouseDevice.CurrentDevice, GetTimeStamp());
        DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(eventArgs));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleMouseLeave(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        trackMouse = false;
        var eventArgs = new RawMouseEventArgs(RawMouseEventType.LeaveWindow, window, Vector2.Zero,
            KeyboardDevice.CurrentDevice.Modifiers, MouseDevice.CurrentDevice, GetTimeStamp());
        DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(eventArgs));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleMouseLeftButtonDown(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var modifiers = WindowsMouseDeviceExtension.GetKeyModifiers(windowMessage, wParam);
        var eventType = WindowsMouseDeviceExtension.EventTypeFromMessage(windowMessage, wParam);
        var eventArgs = new RawMouseEventArgs(eventType, window, ToLogical(Messages.PointFromLParam(lParam)),
            modifiers, MouseDevice.CurrentDevice, GetTimeStamp());
        DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(eventArgs));

        // Hold the OS mouse capture on the WINDOW while ANY mouse button is down (button state read from wParam, which
        // the OS sets per message). This guarantees the window receives the button-UP even when the release happens
        // OUTSIDE it, so a drag past the window edge (a scrollbar thumb dragged out and released) still gets its release
        // and stops - instead of sticking and then "following" the pointer with no button held. Runs on THIS message-pump
        // thread that owns the window (SetCapture is thread-affine). The app-level routing capture (MouseDevice.Captured)
        // is separate and set on the loop thread by the marshalled ProcessEvent above - which is exactly why mirroring
        // THAT here was unreliable: Captured is not set yet when this line runs.
        var anyButton = InputModifiers.LeftMouseButton | InputModifiers.RightMouseButton |
            InputModifiers.MiddleMouseButton | InputModifiers.X1MouseButton | InputModifiers.X2MouseButton;
        var buttonDown = (modifiers & anyButton) != 0;
        if (buttonDown != osMouseCaptured)
        {
            osMouseCaptured = buttonDown;
            SetMouseCapture(buttonDown);
        }

        handled = true;
        return IntPtr.Zero;
    }

    // Platform-specific OS capture (the shared "when" lives in MouseDevice.SyncOsMouseCapture).
    public void SetMouseCapture(bool capture)
    {
        if (capture) Win32Interop.SetCapture(window.Handle);
        else Win32Interop.ReleaseCapture();
    }

    private IntPtr HandleCaptureChanged(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        // The OS revoked our capture (another window/app grabbed it, alt-tab, etc.). Drop the internal capture too so a
        // captured control doesn't stay stuck believing the drag is still live. Capture() re-routes input against the
        // visual tree, so marshal it onto the loop thread (the check runs there too - no cross-thread read of Captured).
        osMouseCaptured = false;
        DispatchInput(() => { if (MouseDevice.CurrentDevice.Captured != null) MouseDevice.CurrentDevice.Capture(null); });
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleMouseWheel(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var eventArgs = new RawMouseWheelEventArgs(
            Messages.GetWheelDelta(wParam), 
            RawMouseEventType.MouseWheel, 
            window,
            window.PointToClient(Messages.PointFromLParam(lParam)),
            WindowsMouseDeviceExtension.GetKeyModifiers(windowMessage, wParam),
            MouseDevice.CurrentDevice, GetTimeStamp());
        DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(eventArgs));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleRawInput(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        int outSize = 0;
        int size = Marshal.SizeOf(typeof(RawInputData));

        outSize = Win32Interop.GetRawInputData(lParam, RawInputCommand.Input, out var inputData, ref size,
            Marshal.SizeOf(typeof(RawInputHeader)));
        if (outSize == -1)
        {
            handled = false;
            return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
        }

        if (inputData.Header.DeviceType == DeviceType.Mouse)
        {
            var position = MouseDevice.CurrentDevice.GetScreenPosition();
            Win32Interop.GetWindowRect(window.Handle, out var wndRect);
            WindowStyle value = Win32Interop.GetWindowStyle(window.Handle, WindowLongType.Style);
            // TODO check can we remove IsLocked property completely
            //if (!window.IsLocked)
            {
                var delta = new Vector2(inputData.Data.Mouse.LastX, inputData.Data.Mouse.LastY);
                if (inputData.Data.Mouse.Data.ButtonFlags != RawMouseButtons.None)
                {
                    lastRawMouseModifiers = WindowsMouseDeviceExtension.GetRawMouseModifiers(inputData.Data.Mouse);
                }

                // Build the args synchronously (message-time screen position + modifiers), but marshal the ProcessEvent
                // onto the loop thread - it routes into the visual tree, same as the other mouse handlers. The single
                // FIFO queue keeps the button event ahead of the move.
                if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.LeftUp))
                {
                    var args = new RawMouseEventArgs(RawMouseEventType.RawLeftButtonUp, window,
                        MouseDevice.CurrentDevice.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                        MouseDevice.CurrentDevice, GetTimeStamp());
                    DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(args));
                }
                else if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.LeftDown))
                {
                    var args = new RawMouseEventArgs(RawMouseEventType.RawLeftButtonDown, window,
                        MouseDevice.CurrentDevice.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                        MouseDevice.CurrentDevice, GetTimeStamp());
                    DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(args));
                }
                if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.RightUp))
                {
                    var args = new RawMouseEventArgs(RawMouseEventType.RawRightButtonUp, window,
                        MouseDevice.CurrentDevice.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                        MouseDevice.CurrentDevice, GetTimeStamp());
                    DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(args));
                }
                else if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.RightDown))
                {
                    var args = new RawMouseEventArgs(RawMouseEventType.RawRightButtonDown, window,
                        MouseDevice.CurrentDevice.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                        MouseDevice.CurrentDevice, GetTimeStamp());
                    DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(args));
                }

                //if (input.Data.Mouse.LastX!=0 || input.Data.Mouse.LastY!=0)
                {
                    var moveArgs = new RawInputMouseEventArgs(delta,
                        RawMouseEventType.RawMouseMove, window,
                        window.ScreenToClient(Mouse.ScreenCoordinates), GetKeyModifiers(lastRawMouseModifiers),
                        MouseDevice.CurrentDevice, GetTimeStamp());
                    DispatchInput(() => MouseDevice.CurrentDevice.ProcessEvent(moveArgs));
                }
            }
        }

        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleSetCursor(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        handled = false;
        Win32Interop.SetCursor(Mouse.Cursor.CursorHandle);
        if (isOverSizeFrame)
        {
            return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
        }
        else
        {
            handled = true;
            return IntPtr.Zero;
        }
    }

    private static uint GetTimeStamp()
    {
        return unchecked((uint)Win32Interop.GetMessageTime());
    }

    private void HandleActivation()
    {
        FocusManager.TryRestoreFocus(window);
        window.SetIsActive(true);
        UIContext.UIApplication.SetActiveWindow(window);
    }

    private void HandleDeactivation()
    {
        window.SetIsActive(false);
        UIContext.UIApplication.InactivateWindow(window);
    }

    private static InputModifiers GetKeyModifiers(InputModifiers mouse)
    {
        var modifiers = KeyboardDevice.CurrentDevice.Modifiers;
        return modifiers | mouse;
    }

    private static InputModifiers GetKeyModifiers(RawMouse rawMouse)
    {
        var modifiers = WindowsMouseDeviceExtension.GetRawMouseModifiers(rawMouse);
        var keyModifiers = KeyboardDevice.CurrentDevice.Modifiers;
        modifiers |= keyModifiers;
        return modifiers;
    }
}