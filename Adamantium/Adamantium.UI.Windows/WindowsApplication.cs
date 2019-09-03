﻿using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.UI.Input;
using Adamantium.Win32;

namespace Adamantium.UI.Windows
{
    public class WindowsApplication : Application
    {
        public WindowsApplication()
        {
            Windows.WindowAdded += OnWindowAdded;
        }

        private void OnWindowAdded(object sender, WindowEventArgs e)
        {
            //InputDevice mouseDevice = new InputDevice();
            //mouseDevice.WindowHandle = e.Window.Handle;
            //mouseDevice.UsagePage = HIDUsagePage.Generic;
            //mouseDevice.UsageId = HIDUsageId.Mouse;
            //mouseDevice.Flags = InputDeviceFlags.None;
            //Interop.RegisterRawInputDevices(new[] { mouseDevice }, 1, Marshal.SizeOf(mouseDevice));
        }

        internal override MouseDevice MouseDevice => WindowsMouseDevice.Instance;
        internal override KeyboardDevice KeyboardDevice => WindowsKeyboardDevice.Instance;

        public override void Run()
        {
            Message msg;

            while (IsRunning)
            {
                while (Messages.PeekMessage(out msg, IntPtr.Zero, 0, 0, PeekMessageFlag.Remove))
                {
                    Messages.TranslateMessage(ref msg);
                    Messages.DispatchMessage(ref msg);
                }

                RunUpdateDrawBlock();
                CheckExitCondition();
            }
        }

        //protected override 
    }
}