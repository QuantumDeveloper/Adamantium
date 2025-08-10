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
    private InputModifiers lastRawMouseModifiers;
    private Win32NativeWindowWrapper source;

    static Win32WindowWorker()
    {
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
        messageTable[(uint)WindowMessages.Keydown] = HandleKeyDown;
        messageTable[(uint)WindowMessages.Syskeydown] = HandleKeyDown;
        messageTable[(uint)WindowMessages.Keyup] = HandleKeyUp;
        messageTable[(uint)WindowMessages.Syskeyup] = HandleKeyUp;
        messageTable[(uint)WindowMessages.Char] = HandleChar;
        messageTable[(uint)WindowMessages.Mousemove] = HandleMouseMove;
        messageTable[(uint)WindowMessages.Mouseleave] = HandleMouseLeave;
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

        Win32Interop.GetClientRect(window.Handle, out var client);
        this.window.ClientWidth = (uint)client.Width;
        this.window.ClientHeight = (uint)client.Height;

        //this.window.OnApplyTemplate();
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


    private IntPtr HandleActivate(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var state = Messages.GetWindowActivationState(wParam);
        switch (state)
        {
            case WindowActivation.Active:
            case WindowActivation.ClickActive:
                HandleActivation();
                break;

            case WindowActivation.Inactive:
                HandleDeactivation();
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
        //mutex.WaitOne();
        Win32Interop.GetWindowRect(window.Handle, out var rect);
        window.Width = rect.Width;
        window.Height = rect.Height;
            
        Win32Interop.GetClientRect(window.Handle, out var client);
        window.ClientWidth = (uint)client.Width;
        window.ClientHeight = (uint)client.Height;

        //mutex.ReleaseMutex();
        return IntPtr.Zero;
    }

    private IntPtr HandleKeyDown(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        KeyboardDevice.CurrentDevice.ProcessEvent(new RawKeyboardEventArgs((Key)Messages.GetKey(wParam),
            RawKeyboardEventType.KeyDown, lParam, KeyboardDevice.CurrentDevice.Modifiers, GetTimeStamp()));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleKeyUp(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        KeyboardDevice.CurrentDevice.ProcessEvent(new RawKeyboardEventArgs((Key)Messages.GetKey(wParam),
            RawKeyboardEventType.KeyUp, lParam, KeyboardDevice.CurrentDevice.Modifiers, GetTimeStamp()));
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleChar(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var text = Messages.GetChar(wParam);
        //Ignoring system keys
        if (text >= 32)
        {
            KeyboardDevice.CurrentDevice.ProcessEvent(new RawTextInputEventArgs(text.ToString(),
                KeyboardDevice.CurrentDevice.Modifiers, GetTimeStamp()));
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
        var eventArgs = new RawMouseEventArgs(RawMouseEventType.MouseMove, window, Messages.PointFromLParam(lParam),
            WindowsMouseDeviceExtension.GetKeyModifiers(windowMessage, wParam), MouseDevice.CurrentDevice, GetTimeStamp());
        MouseDevice.CurrentDevice.ProcessEvent(eventArgs);
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleMouseLeave(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        trackMouse = false;
        var eventArgs = new RawMouseEventArgs(RawMouseEventType.LeaveWindow, window, Vector2.Zero,
            KeyboardDevice.CurrentDevice.Modifiers, MouseDevice.CurrentDevice, GetTimeStamp());
        MouseDevice.CurrentDevice.ProcessEvent(eventArgs);
        handled = true;
        return IntPtr.Zero;
    }

    private IntPtr HandleMouseLeftButtonDown(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
    {
        var eventType = WindowsMouseDeviceExtension.EventTypeFromMessage(windowMessage, wParam);
        var eventArgs = new RawMouseEventArgs(eventType, window, Messages.PointFromLParam(lParam),
            WindowsMouseDeviceExtension.GetKeyModifiers(windowMessage, wParam), MouseDevice.CurrentDevice, GetTimeStamp());
        MouseDevice.CurrentDevice.ProcessEvent(eventArgs);
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
        MouseDevice.CurrentDevice.ProcessEvent(eventArgs);
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

                if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.LeftUp))
                {
                    MouseDevice.CurrentDevice.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawLeftButtonUp, window,
                        MouseDevice.CurrentDevice.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                        MouseDevice.CurrentDevice, GetTimeStamp()));
                }
                else if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.LeftDown))
                {
                    MouseDevice.CurrentDevice.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawLeftButtonDown, window,
                        MouseDevice.CurrentDevice.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                        MouseDevice.CurrentDevice, GetTimeStamp()));
                }
                if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.RightUp))
                {
                    MouseDevice.CurrentDevice.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawRightButtonUp, window,
                        MouseDevice.CurrentDevice.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                        MouseDevice.CurrentDevice, GetTimeStamp()));
                }
                else if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.RightDown))
                {
                    MouseDevice.CurrentDevice.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawRightButtonDown, window,
                        MouseDevice.CurrentDevice.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                        MouseDevice.CurrentDevice, GetTimeStamp()));
                }

                //if (input.Data.Mouse.LastX!=0 || input.Data.Mouse.LastY!=0)
                {
                    MouseDevice.CurrentDevice.ProcessEvent(new RawInputMouseEventArgs(delta,
                        RawMouseEventType.RawMouseMove, window,
                        window.ScreenToClient(Mouse.ScreenCoordinates), GetKeyModifiers(lastRawMouseModifiers),
                        MouseDevice.CurrentDevice, GetTimeStamp()));
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