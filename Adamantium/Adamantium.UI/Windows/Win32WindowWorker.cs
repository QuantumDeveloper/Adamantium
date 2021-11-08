using Adamantium.Mathematics;
using Adamantium.UI.Input;
using Adamantium.UI.Input.Raw;
using Adamantium.Win32;
using Adamantium.Win32.RawInput;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.UI.AggregatorEvents;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Windows
{
    internal class Win32WindowWorker : AdamantiumComponent, IWindowWorkerService
    {
        private WindowBase window;
        private Dictionary<uint, HandleMessage> messageTable;
        //private Mutex mutex = Mutex.OpenExisting("adamantiumMutex");

        private bool isOverSizeFrame;
        private bool trackMouse;
        private InputModifiers lastRawMouseModifiers;
        private Win32NativeWindowWrapper source;

        private IEventAggregator eventAggregator;

        static Win32WindowWorker()
        {
            RawInputDevice.RegisterDevice(HIDUsagePage.Generic, HIDUsageId.Mouse, InputDeviceFlags.None);
        }

        public Win32WindowWorker()
        {
            eventAggregator = AdamantiumServiceLocator.Current.Resolve<IEventAggregator>();
            messageTable = new Dictionary<uint, HandleMessage>();
            messageTable[(uint)WindowMessages.Activate] = HandleActivate;
            messageTable[(uint)WindowMessages.Syscommand] = HandleSysCommand;
            //messageTable[(uint)WindowMessages.Nclbuttondown] = HandleNcButtonDown;
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

        public void SetWindow(WindowBase window)
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
                (int)window.Location.X,
                (int)window.Location.Y, 
                (int)window.Width, 
                (int)window.Height, 
                IntPtr.Zero);
            this.window.Handle = source.Handle;
            if (source.Handle != IntPtr.Zero)
            {
                this.window.SurfaceHandle = source.Handle;
                
                source.AddHook(CustomWndProc);

                Win32Interop.GetClientRect(window.Handle, out var client);
                this.window.ClientWidth = (uint)client.Width;
                this.window.ClientHeight = (uint)client.Height;

                this.window.ApplyTemplate();
                
                eventAggregator.GetEvent<WindowAddedEvent>().Publish(this.window);
                
                this.window.OnSourceInitialized();
                Win32Interop.ShowWindow(source.Handle, WindowShowStyle.ShowNormal);
            }
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            source.RemoveHook(CustomWndProc);
            //Application.Current.Windows.Remove(this.window);
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
            if (ht == NcHitTest.Close)
            {
                window.Close();
            }
            handled = false;
            return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
        }

        private IntPtr HandleNcHittest(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
        {
            var uHitTest = Win32Interop.DefWindowProc(window.Handle, WindowMessages.Nchittest, wParam, lParam);
            var result = (NcHitTest)(Environment.Is64BitProcess ? uHitTest.ToInt64() : uHitTest.ToInt32());
            if (result != NcHitTest.Client)
            {
                isOverSizeFrame = true;
            }
            else
            {
                isOverSizeFrame = false;
            }
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

        private IntPtr HandleSysCommand(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
        {
            var command = (SystemCommands)(Environment.Is64BitProcess ? wParam.ToInt64() : wParam.ToInt32());
            var p = Messages.PointFromLParam(lParam);
            switch (command)
            {
                case SystemCommands.CLOSE:
                    window.Close();
                    if (window.IsClosed)
                    {
                        source.Destroy();
                        window.Handle = IntPtr.Zero;
                    }
                    break;
                case SystemCommands.MOVE:
                    Win32Interop.SetWindowPos(window.Handle, IntPtr.Zero, (int)p.X, (int)p.Y, (int)window.Width,
                       (int)window.Height, SetWindowPosFlags.Asyncwindowpos | SetWindowPosFlags.Nosize);
                    break;
                default:
                    Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
                    break;
            }

            handled = true;
            return IntPtr.Zero;
        }

        private IntPtr HandleResize(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
        {
            //mutex.WaitOne();
            Win32Interop.GetWindowRect(window.Handle, out var rect);
            window.Width = rect.Width;
            window.Height = rect.Height;
            
            Win32Interop.GetClientRect(window.Handle, out var client);
            window.ClientWidth = (uint)client.Width;
            window.ClientHeight = (uint)client.Height;

            handled = true;
            //mutex.ReleaseMutex();
            return IntPtr.Zero;
        }

        private IntPtr HandleKeyDown(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
        {
            WindowsKeyboardDevice.Instance.ProcessEvent(new RawKeyboardEventArgs((Key)Messages.GetKey(wParam),
                       RawKeyboardEventType.KeyDown, lParam, WindowsKeyboardDevice.Instance.Modifiers, GetTimeStamp()));
            handled = true;
            return IntPtr.Zero;
        }

        private IntPtr HandleKeyUp(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
        {
            WindowsKeyboardDevice.Instance.ProcessEvent(new RawKeyboardEventArgs((Key)Messages.GetKey(wParam),
                       RawKeyboardEventType.KeyUp, lParam, WindowsKeyboardDevice.Instance.Modifiers, GetTimeStamp()));
            handled = true;
            return IntPtr.Zero;
        }

        private IntPtr HandleChar(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
        {
            var text = Messages.GetChar(wParam);
            //Ignoring system keys
            if (text >= 32)
            {
                WindowsKeyboardDevice.Instance.ProcessEvent(new RawTextInputEventArgs(text.ToString(),
                   WindowsKeyboardDevice.Instance.Modifiers, GetTimeStamp()));
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
                    dwFlags = 2,
                    hwndTrack = window.Handle,
                    dwHoverTime = 0,
                };
                trackMouse = true;
                Win32Interop.TrackMouseEvent(ref tm);
            }
            var eventArgs = new RawMouseEventArgs(RawMouseEventType.MouseMove, window, Messages.PointFromLParam(lParam),
               WindowsMouseDevice.GetKeyModifiers(windowMessage, wParam), WindowsMouseDevice.Instance, GetTimeStamp());
            WindowsMouseDevice.Instance.ProcessEvent((RawMouseEventArgs)eventArgs);
            handled = true;
            return IntPtr.Zero;
        }

        private IntPtr HandleMouseLeave(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
        {
            trackMouse = false;
            var eventArgs = new RawMouseEventArgs(RawMouseEventType.LeaveWindow, window, Point.Zero,
               WindowsKeyboardDevice.Instance.Modifiers, WindowsMouseDevice.Instance, GetTimeStamp());
            WindowsMouseDevice.Instance.ProcessEvent((RawMouseEventArgs)eventArgs);
            handled = true;
            return IntPtr.Zero;
        }

        private IntPtr HandleMouseLeftButtonDown(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
        {
            var eventType = WindowsMouseDevice.EventTypeFromMessage(windowMessage, wParam);
            var eventArgs = new RawMouseEventArgs(eventType, window, Messages.PointFromLParam(lParam),
               WindowsMouseDevice.GetKeyModifiers(windowMessage, wParam), WindowsMouseDevice.Instance, GetTimeStamp());
            WindowsMouseDevice.Instance.ProcessEvent((RawMouseEventArgs)eventArgs);
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
                WindowsMouseDevice.GetKeyModifiers(windowMessage, wParam),
                WindowsMouseDevice.Instance, GetTimeStamp());
            WindowsMouseDevice.Instance.ProcessEvent((RawMouseEventArgs)eventArgs);
            handled = true;
            return IntPtr.Zero;
        }

        private IntPtr HandleRawInput(WindowMessages windowMessage, IntPtr wParam, IntPtr lParam, out bool handled)
        {
            RawInputData inputData;
            int outSize = 0;
            int size = Marshal.SizeOf(typeof(RawInputData));

            outSize = Win32Interop.GetRawInputData(lParam, RawInputCommand.Input, out inputData, ref size,
               Marshal.SizeOf(typeof(RawInputHeader)));
            if (outSize == -1)
            {
                handled = false;
                return Win32Interop.DefWindowProc(window.Handle, windowMessage, wParam, lParam);
            }

            if (inputData.Header.DeviceType == DeviceType.Mouse)
            {
                var position = WindowsMouseDevice.Instance.GetScreenPosition();
                RECT wndRect;
                Win32Interop.GetWindowRect(window.Handle, out wndRect);
                WindowStyle value = Win32Interop.GetWindowStyle(window.Handle, WindowLongType.Style);
                if (!window.IsLocked)
                {
                    Point delta = new Point(inputData.Data.Mouse.LastX, inputData.Data.Mouse.LastY);
                    if (inputData.Data.Mouse.Data.ButtonFlags != RawMouseButtons.None)
                    {
                        lastRawMouseModifiers = WindowsMouseDevice.GetRawMouseModifiers(inputData.Data.Mouse);
                    }

                    if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.LeftUp))
                    {
                        WindowsMouseDevice.Instance.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawLeftButtonUp, window,
                           WindowsMouseDevice.Instance.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                           WindowsMouseDevice.Instance, GetTimeStamp()));
                    }
                    else if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.LeftDown))
                    {
                        WindowsMouseDevice.Instance.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawLeftButtonDown, window,
                              WindowsMouseDevice.Instance.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                              WindowsMouseDevice.Instance, GetTimeStamp()));
                    }
                    if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.RightUp))
                    {
                        WindowsMouseDevice.Instance.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawRightButtonUp, window,
                           WindowsMouseDevice.Instance.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                           WindowsMouseDevice.Instance, GetTimeStamp()));
                    }
                    else if (inputData.Data.Mouse.Data.ButtonFlags.HasFlag(RawMouseButtons.RightDown))
                    {
                        WindowsMouseDevice.Instance.ProcessEvent(new RawMouseEventArgs(RawMouseEventType.RawRightButtonDown, window,
                              WindowsMouseDevice.Instance.GetScreenPosition(), GetKeyModifiers(lastRawMouseModifiers),
                              WindowsMouseDevice.Instance, GetTimeStamp()));
                    }

                    //if (input.Data.Mouse.LastX!=0 || input.Data.Mouse.LastY!=0)
                    {
                        WindowsMouseDevice.Instance.ProcessEvent(new RawInputMouseEventArgs(delta,
                           RawMouseEventType.RawMouseMove, window,
                           window.ScreenToClient(Mouse.ScreenCoordinates), GetKeyModifiers(lastRawMouseModifiers),
                           WindowsMouseDevice.Instance, GetTimeStamp()));
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
            window.IsActive = true;
        }

        private void HandleDeactivation()
        {
            window.IsActive = false;
            window.IsLocked = false;
        }


        private static InputModifiers GetKeyModifiers(InputModifiers mouse)
        {
            var modifiers = WindowsKeyboardDevice.Instance.Modifiers;
            return modifiers | mouse;
        }

        private static InputModifiers GetKeyModifiers(RawMouse rawMouse)
        {
            var modifiers = WindowsMouseDevice.GetRawMouseModifiers(rawMouse);
            var keyModif = WindowsKeyboardDevice.Instance.Modifiers;
            modifiers |= keyModif;
            return modifiers;
        }

    }
}
