using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Adamantium.MacOS;
using Adamantium.Mathematics;
using Adamantium.UI.Input;

namespace Adamantium.UI.OSX
{
    public class OSXApplication : Application
    {
        internal override MouseDevice MouseDevice { get; }
        internal override KeyboardDevice KeyboardDevice { get; }

        private IntPtr appDelegate;
        private IntPtr app;

        public OSXApplication()
        {
            Windows.WindowAdded += OnWindowAdded;
            Windows.WindowRemoved -= OnWindowRemoved;
            appDelegate = MacOSInterop.CreateApplicationDelegate();
            app = MacOSInterop.CreateApplication(appDelegate);
            //Services.Add();
        }
        
        private void OnWindowAdded(object sender, WindowEventArgs e)
        {
            MacOSInterop.AddWindowToAppDelegate(appDelegate, e.Window.Handle);
        }
        
        private void OnWindowRemoved(object sender, WindowEventArgs e)
        {
            //MacOSInterop.AddWindowToAppDelegate(appDelegate, e.Window.Handle);
        }
        
        public override void Run()
        {
            try
            {
                MacOSInterop.RunApplication(app);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }







    }
}