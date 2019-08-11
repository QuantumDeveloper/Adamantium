using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Input;
using Adamantium.UI.Input.Raw;
using Adamantium.Win32;
using Adamantium.Win32.RawInput;
using RawInputEventArgs = Adamantium.UI.Input.Raw.RawInputEventArgs;

namespace Adamantium.UI
{
    public class Window : ContentControl, IWindow, IRootVisual
    {
        private delegate IntPtr WndProc(IntPtr hWnd, WindowMessages msg, IntPtr wParam, IntPtr lParam);

        private const int ERROR_CLASS_ALREADY_EXISTS = 1410;

        public IntPtr WindowHandle { get; private set; }

        private Interop.WndProc wndProcDelegate;

        public Int32 ClientWidth { get; set; }
        public Int32 ClientHeight { get; set; }

        private static string className = "Adamantium Window";

        static Window()
        {
            //CreateAndRegisterWndClass();
        }

        public Window()
        {

        }

        private void CreateAndRegisterWndClass()
        {
            //wndProcDelegate = StaticWndProc;
            wndProcDelegate = CustomWndProc;
            // Create WNDCLASS
            WndClass wndClass = new WndClass
            {
                style = WindowClassStyle.OwnDC | WindowClassStyle.DoubleClicks| WindowClassStyle.VerticalRedraw | WindowClassStyle.HorizontalRedraw,
                lpszClassName = className,
                hCursor = Interop.LoadCursor(IntPtr.Zero, NativeCursors.Arrow),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate)
            };

            UInt16 classAtom = Interop.RegisterClassW(ref wndClass);

            int lastError = Marshal.GetLastWin32Error();

            if (classAtom == 0 && lastError != ERROR_CLASS_ALREADY_EXISTS)
            {
                throw new Exception("Could not register window class");
            }
        }

        private void CreateNativeWindow()
        {
            CreateAndRegisterWndClass();
            // Create window
            WindowHandle = Interop.CreateWindowExW(
               WindowStyleEx.Appwindow | WindowStyleEx.Acceptfiles,
               className,
               String.Empty,
               WindowStyle.Popup | WindowStyle.Clipsiblings| WindowStyle.Clipchildren| WindowStyle.Sizeframe,
               200,
               200,
               (int)Width,
               (int)Height,
               IntPtr.Zero,
               IntPtr.Zero,
               IntPtr.Zero,
               IntPtr.Zero
               );

            var r = GetClientRectangle(WindowHandle);
            ClientWidth = (int)r.Width;
            ClientHeight = (int)r.Height;
            Width = (int)r.Width;
            Height = (int)r.Height;
        }

        private static RECT GetClientRectangle(IntPtr hwnd)
        {
            Interop.GetClientRect(hwnd, out var rect);
            return rect;
        }

        private static RECT GetWindowRectangle(IntPtr hwnd)
        {
            Interop.GetWindowRect(hwnd, out var rect);
            return rect;
        }

        public void Show()
        {
            CreateNativeWindow();
            if (WindowHandle != IntPtr.Zero)
            {
                Interop.ShowWindow(WindowHandle, WindowShowStyle.ShowNormal);
                OnLoaded();
            }
        }

        public void Close()
        {
            if (WindowHandle != IntPtr.Zero)
            {
                Interop.DestroyWindow(WindowHandle);
                WindowHandle = IntPtr.Zero;
            }
            IsClosed = true;
            OnClosed();
        }

        public Boolean IsClosed { get; set; }
        private Point startCoords;

        private bool _trackMouse = false;
        private int resizeBorderWidth = 4;
        private bool isOverSizeFrame = false;

        /// <summary>
        /// Window Procedure
        /// </summary>
        /// <param name="hWnd">windows hendler</param>
        /// <param name="msg">window message (one of window mesages)<see cref="WindowMessages"/>"/></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        protected virtual IntPtr CustomWndProc(IntPtr hWnd, WindowMessages msg, IntPtr wParam, IntPtr lParam)
        {
            UInt32 timeStamp = unchecked((uint)Interop.GetMessageTime());

            RawInputEventArgs eventArgs = null;
            switch (msg)
            {
                case WindowMessages.Nccalcsize:
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
                        return IntPtr.Zero;
                    }
                    break;
                case WindowMessages.Ncpaint:
                //    uint color = 0;
                //    color |= 255 << 8;
                //    color |= 0 << 16;
                //    var hdc = Interop.GetWindowDC(WindowHandle);
                //    var windowRect = GetWindowRectangle(WindowHandle);
                //    var brush = Interop.CreateSolidBrush(color);
                //    Interop.SelectObject(hdc, brush);
                //    var pen = Interop.CreatePen(PenStyle.Solid, 1, color);
                //    Interop.SelectObject(hdc, pen);
                //    Interop.SendMessage(WindowHandle, WindowMessages.Print, hdc, new IntPtr((int)PrintOptions.NonClient));
                //    Interop.Rectangle(hdc, 0, 0, windowRect.Width, windowRect.Height);
                //    Interop.ReleaseDC(WindowHandle, hdc);
                //    Interop.RedrawWindow(WindowHandle, ref windowRect, wParam, RedrawWindowFlags.Validate | RedrawWindowFlags.Frame);

                    return IntPtr.Zero;

                case WindowMessages.Nchittest:
                    var uHitTest = Interop.DefWindowProcW(hWnd, WindowMessages.Nchittest, wParam, lParam);
                    var result = (NcHitTest)(Environment.Is64BitProcess ? uHitTest.ToInt64() : uHitTest.ToInt32());
                    if (result != NcHitTest.Client)
                    {
                        isOverSizeFrame = true;
                    }
                    else
                    {
                        isOverSizeFrame = false;
                    }
                    Debug.WriteLine($"NcHittTest = {result}");
                    var screenPos = GetWindowRectangle(hWnd);

                    break;


                case WindowMessages.Nclbuttondown:
                    var ht = (NcHitTest)(Environment.Is64BitProcess ? wParam.ToInt64() : wParam.ToInt32());
                    if (ht == NcHitTest.Close)
                    {
                        Close();
                    }
                    break;
                case WindowMessages.Activate:
                    Margins margins = new Margins();
                    margins.Left = 5;
                    margins.Right = 5;
                    margins.Top = 5;
                    margins.Bottom = 5;
                    //Interop.DwmExtendFrameIntoClientArea(WindowHandle, ref margins);
                    //var lastError = Marshal.GetLastWin32Error();
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
                    return IntPtr.Zero;
                case WindowMessages.Paint:
                    PAINTSTRUCT ps;

                    if (Interop.BeginPaint(hWnd, out ps) != IntPtr.Zero)
                    {
                        RECT r;
                        Interop.GetUpdateRect(hWnd, out r, false);
                        Interop.EndPaint(hWnd, ref ps);
                    }
                    return IntPtr.Zero;


                case WindowMessages.Size:
                    var rect = GetWindowRectangle(hWnd);
                    Width = rect.Width;
                    Height = rect.Height;
                    var client = GetClientRectangle(hWnd);
                    ClientWidth = client.Width;
                    ClientHeight = client.Height;
                    if (ClientWidth != 0 && ClientHeight != 0)
                    {

                        ClientSizeChanged?.Invoke(this,
                           new SizeChangedEventArgs(new Size(ClientWidth, ClientHeight), new Size(), true, true));
                    }
                    return IntPtr.Zero;

                case WindowMessages.Setcursor:
                    Interop.SetCursor(Mouse.Cursor.CursorHandle);
                    if (isOverSizeFrame)
                    {
                        break;
                    }
                    else
                    {
                        return IntPtr.Zero;
                    }


                case WindowMessages.Keydown:
                case WindowMessages.Syskeydown:
                    WindowsKeyboardDevice.Instance.ProcessEvent(new RawKeyboardEventArgs((Key)Messages.GetKey(wParam),
                       RawKeyboardEventType.KeyDown, lParam, WindowsKeyboardDevice.Instance.Modifiers, timeStamp));
                    return IntPtr.Zero;

                case WindowMessages.Keyup:
                case WindowMessages.Syskeyup:
                    WindowsKeyboardDevice.Instance.ProcessEvent(new RawKeyboardEventArgs((Key)Messages.GetKey(wParam),
                       RawKeyboardEventType.KeyUp, lParam, WindowsKeyboardDevice.Instance.Modifiers, timeStamp));
                    return IntPtr.Zero;

                case WindowMessages.Char:
                    var text = Messages.GetChar(wParam);
                    //Ignoring system keys
                    if (text >= 32)
                    {
                        WindowsKeyboardDevice.Instance.ProcessEvent(new RawTextInputEventArgs(text.ToString(),
                           WindowsKeyboardDevice.Instance.Modifiers, timeStamp));
                    }
                    return IntPtr.Zero;

                case WindowMessages.Syscommand:
                    var command = (SystemCommands)(Environment.Is64BitProcess ? wParam.ToInt64() : wParam.ToInt32());
                    var p = Messages.PointFromLParam(lParam);
                    switch (command)
                    {
                        case SystemCommands.CLOSE:
                            Close();
                            break;
                        case SystemCommands.MOVE:
                            Interop.SetWindowPos(WindowHandle, IntPtr.Zero, (int)p.X, (int)p.Y, (int)Width,
                               (int)Height, SetWindowPosFlags.Asyncwindowpos | SetWindowPosFlags.Nosize);
                            break;
                        default:
                            Interop.DefWindowProcW(hWnd, msg, wParam, lParam);
                            break;
                    }

                    return IntPtr.Zero;


                case WindowMessages.Mousemove:
                    Debug.WriteLine("Mouse move");
                    if (!_trackMouse)
                    {
                        var tm = new TRACKMOUSEEVENT
                        {
                            cbSize = Marshal.SizeOf(typeof(TRACKMOUSEEVENT)),
                            dwFlags = 2,
                            hwndTrack = WindowHandle,
                            dwHoverTime = 0,
                        };
                        _trackMouse = true;
                        Interop.TrackMouseEvent(ref tm);
                    }
                    eventArgs = new RawMouseEventArgs(RawMouseEventType.MouseMove, this, Messages.PointFromLParam(lParam),
                       GetKeyModifiers(msg, wParam), WindowsMouseDevice.Instance, timeStamp);
                    WindowsMouseDevice.Instance.ProcessEvent((RawMouseEventArgs)eventArgs);
                    return IntPtr.Zero;

                case WindowMessages.Mouseleave:
                    _trackMouse = false;
                    eventArgs = new RawMouseEventArgs(RawMouseEventType.LeaveWindow, this, Point.Zero,
                       WindowsKeyboardDevice.Instance.Modifiers, WindowsMouseDevice.Instance, timeStamp);
                    WindowsMouseDevice.Instance.ProcessEvent((RawMouseEventArgs)eventArgs);
                    return IntPtr.Zero;

                case WindowMessages.LeftButtondown:
                case WindowMessages.RightButtondown:
                case WindowMessages.MiddleButtondown:
                case WindowMessages.Xbuttondown:
                    var eventType = MouseEventTypeFromMessage(msg, wParam);
                    eventArgs = new RawMouseEventArgs(eventType, this, Messages.PointFromLParam(lParam),
                       GetKeyModifiers(msg, wParam), WindowsMouseDevice.Instance, timeStamp);
                    WindowsMouseDevice.Instance.ProcessEvent((RawMouseEventArgs)eventArgs);
                    return IntPtr.Zero;

                case WindowMessages.LeftButtonup:
                case WindowMessages.RightButtonup:
                case WindowMessages.MiddleButtonup:
                case WindowMessages.Xbuttonup:
                    eventType = MouseEventTypeFromMessage(msg, wParam);
                    eventArgs = new RawMouseEventArgs(eventType, this, Messages.PointFromLParam(lParam),
                       GetKeyModifiers(msg, wParam), WindowsMouseDevice.Instance, timeStamp);
                    WindowsMouseDevice.Instance.ProcessEvent((RawMouseEventArgs)eventArgs);
                    return IntPtr.Zero;

                case WindowMessages.LeftButtondblclk:
                case WindowMessages.RightButtondblclk:
                case WindowMessages.MiddleButtondblclk:
                case WindowMessages.Xbuttondblclk:
                    eventType = MouseEventTypeFromMessage(msg, wParam);
                    eventArgs = new RawMouseEventArgs(eventType, this, Messages.PointFromLParam(lParam),
                       GetKeyModifiers(msg, wParam), WindowsMouseDevice.Instance, timeStamp);
                    WindowsMouseDevice.Instance.ProcessEvent((RawMouseEventArgs)eventArgs);
                    return IntPtr.Zero;

                case WindowMessages.MouseWheel:
                    eventArgs = new RawMouseWheelEventArgs(Messages.GetWheelDelta(wParam), RawMouseEventType.MouseWheel, this,
                       ScreenToClient(Messages.PointFromLParam(lParam)), GetKeyModifiers(msg, wParam),
                       WindowsMouseDevice.Instance, timeStamp);
                    WindowsMouseDevice.Instance.ProcessEvent((RawMouseEventArgs)eventArgs);
                    return IntPtr.Zero;

                case WindowMessages.Input:
                    RawInputData inputData;
                    int outSize = 0;
                    int size = Marshal.SizeOf(typeof(RawInputData));

                    outSize = Interop.GetRawInputData(lParam, RawInputCommand.Input, out inputData, ref size,
                       Marshal.SizeOf(typeof(RawInputHeader)));
                    if (outSize != -1)
                    {
                        if (inputData.Header.DeviceType == DeviceType.Mouse)
                        {
                            var position = WindowsMouseDevice.Instance.GetScreenPosition();
                            RECT wndRect;
                            Interop.GetWindowRect(hWnd, out wndRect);
                            WindowStyle value = Interop.GetWindowStyle(hWnd, WindowLongType.Style);
                            if (!IsLocked)
                            {
                                Point delta = new Point(inputData.Data.Mouse.LastX, inputData.Data.Mouse.LastY);
                                if (inputData.Data.Mouse.Data.ButtonFlags != RawMouseButtons.None)
                                {
                                    lastRawMouseModifiers = GetRawMouseModifiers(inputData.Data.Mouse);
                                }

                                if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.LeftUp))
                                {
                                    WindowsMouseDevice.Instance.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawLeftButtonUp, this,
                                       WindowsMouseDevice.Instance.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                                       WindowsMouseDevice.Instance, timeStamp));
                                }
                                else if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.LeftDown))
                                {
                                    WindowsMouseDevice.Instance.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawLeftButtonDown, this,
                                          WindowsMouseDevice.Instance.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                                          WindowsMouseDevice.Instance, timeStamp));
                                }
                                if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.RightUp))
                                {
                                    WindowsMouseDevice.Instance.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawRightButtonUp, this,
                                       WindowsMouseDevice.Instance.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                                       WindowsMouseDevice.Instance, timeStamp));
                                }
                                else if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.RightDown))
                                {
                                    WindowsMouseDevice.Instance.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawRightButtonDown, this,
                                          WindowsMouseDevice.Instance.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                                          WindowsMouseDevice.Instance, timeStamp));
                                }

                                //if (input.Data.Mouse.LastX!=0 || input.Data.Mouse.LastY!=0)
                                {
                                    WindowsMouseDevice.Instance.ProcessEvent(new RawInputMouseEventArgs(delta,
                                       RawMouseEventType.RawMouseMove, this,
                                       ScreenToClient(Mouse.ScreenCoordinates), GetKeyModifiers(lastRawMouseModifiers),
                                       WindowsMouseDevice.Instance, timeStamp));
                                }
                            }
                        }
                    }
                    return IntPtr.Zero;
            }

            return Interop.DefWindowProcW(hWnd, msg, wParam, lParam);
        }



        private InputModifiers lastRawMouseModifiers;
        private bool IsLocked = false;
        private IntPtr resizeHandle;
        private WindowSizing sizingDirection;

        private static InputModifiers GetRawMouseModifiers(RawMouse rawMouse)
        {
            InputModifiers modifiers = InputModifiers.None;

            var buttons = rawMouse.Data.ButtonFlags;
            if (buttons.HasFlag(RawMouseButtons.LeftDown))
            {
                modifiers |= InputModifiers.LeftMouseButton;
            }
            if (buttons.HasFlag(RawMouseButtons.MiddleDown))
            {
                modifiers |= InputModifiers.MiddleMouseButton;
            }
            if (buttons.HasFlag(RawMouseButtons.RightDown))
            {
                modifiers |= InputModifiers.RightMouseButton;
            }
            if (buttons.HasFlag(RawMouseButtons.Button4Down))
            {
                modifiers |= InputModifiers.X1MouseButton;
            }
            if (buttons.HasFlag(RawMouseButtons.Button5Down))
            {
                modifiers |= InputModifiers.X2MouseButton;
            }
            return modifiers;
        }

        private static InputModifiers GetKeyModifiers(InputModifiers mouse)
        {
            var modifiers = WindowsKeyboardDevice.Instance.Modifiers;
            return modifiers | mouse;
        }


        private static InputModifiers GetKeyModifiers(RawMouse rawMouse)
        {
            var modifiers = GetRawMouseModifiers(rawMouse);
            var keyModif = WindowsKeyboardDevice.Instance.Modifiers;
            modifiers |= keyModif;
            return modifiers;
        }

        private static InputModifiers GetKeyModifiers(WindowMessages msg, IntPtr wParam)
        {
            var mouseButtons = Messages.GetMouseModifyKeys(msg, wParam);
            var modifiers = WindowsKeyboardDevice.Instance.Modifiers;
            if (mouseButtons.HasFlag(MouseModifiers.LeftButton))
            {
                modifiers |= InputModifiers.LeftMouseButton;
            }
            if (mouseButtons.HasFlag(MouseModifiers.RightButton))
            {
                modifiers |= InputModifiers.RightMouseButton;
            }
            if (mouseButtons.HasFlag(MouseModifiers.MiddleButton))
            {
                modifiers |= InputModifiers.MiddleMouseButton;
            }
            if (mouseButtons.HasFlag(MouseModifiers.XButton1))
            {
                modifiers |= InputModifiers.X1MouseButton;
            }
            if (mouseButtons.HasFlag(MouseModifiers.XButton2))
            {
                modifiers |= InputModifiers.X2MouseButton;
            }
            return modifiers;
        }

        private RawMouseEventType MouseEventTypeFromMessage(WindowMessages msg, IntPtr wParam)
        {
            if (msg == WindowMessages.Xbuttondown)
            {
                var exactButton = Messages.GetMouseModifyKeys(msg, wParam);
                if (exactButton == MouseModifiers.XButton1)
                {
                    return RawMouseEventType.X1ButtonDown;
                }
                else
                {
                    return RawMouseEventType.X2ButtonDown;
                }
            }

            else if (msg == WindowMessages.Xbuttonup)
            {
                var exactButton = Messages.GetMouseModifyKeys(msg, wParam);
                if (exactButton == MouseModifiers.XButton1)
                {
                    return RawMouseEventType.X1ButtonUp;
                }
                else
                {
                    return RawMouseEventType.X2ButtonUp;
                }
            }
            else
            {
                return (RawMouseEventType)msg;
            }
        }



        private void OnLoaded()
        {
            Application.Current.Windows.Add(this);
            Loaded?.Invoke(this, new EventArgs());
        }

        private void OnClosed()
        {
            Application.Current.Windows.Remove(this);
            Closed?.Invoke(this, new EventArgs());
        }

        public event EventHandler<SizeChangedEventArgs> ClientSizeChanged;
        public event EventHandler<EventArgs> Loaded;
        public event EventHandler<EventArgs> Closing;
        public event EventHandler<EventArgs> Closed;

        private void HandleActivation()
        {
            FocusManager.TryRestoreFocus(this);
            IsActive = true;
        }

        public bool IsActive { get; private set; }

        private void HandleDeactivation()
        {
            IsActive = false;
            IsLocked = false;
        }

        public Point PointToClient(Point point)
        {
            return ScreenToClient(point);
        }

        public Point PointToScreen(Point point)
        {
            return ClientToScreen(point);
        }

        private Point ScreenToClient(Point p)
        {
            var point = new NativePoint((int)p.X, (int)p.Y);
            Interop.ScreenToClient(WindowHandle, ref point);
            return point;
        }

        private Point ClientToScreen(Point p)
        {
            var point = new NativePoint((int)p.X, (int)p.Y);
            Interop.ClientToScreen(WindowHandle, ref point);
            return point;
        }
    }

}
