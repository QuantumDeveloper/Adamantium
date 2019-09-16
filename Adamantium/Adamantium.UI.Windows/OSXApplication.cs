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

        private MacOSInterop.OnWindowWillResize resizeDelegate;
        
        public override void Run()
        {
            try
            {
                resizeDelegate = WindowWillResize;
                var appDelegate = MacOSInterop.CreateApplicationDelegate();
                var app = MacOSInterop.CreateApplication(appDelegate);
                var wndStyle = OSXWindowStyle.Borderless | OSXWindowStyle.Resizable |
                               OSXWindowStyle.Titled |
                               OSXWindowStyle.Miniaturizable | OSXWindowStyle.Closable;
                var wnd = MacOSInterop.CreateWindow(new Rectangle(100, 100, 1280, 720), (uint)wndStyle, "Adamantium window");
                var wndDelegate = MacOSInterop.CreateWindowDelegate();
                MacOSInterop.SetWindowDelegate(wnd, wndDelegate);
                MacOSInterop.AddWindowToAppDelegate(appDelegate, wnd);
                var resizeDelegatePtr = Marshal.GetFunctionPointerForDelegate(resizeDelegate);
                MacOSInterop.AddWindowResizeCallback(wndDelegate, resizeDelegatePtr);
                MacOSInterop.RunApplication(app);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public void WindowWillResize(float width, float height)
        {
            Debug.WriteLine($"Window Resize width: {width}, height: {height}");
        }
        
        
        

        
        

    }
}