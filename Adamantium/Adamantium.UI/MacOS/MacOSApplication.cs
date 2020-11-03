using System;
using System.Threading;
using Adamantium.MacOS;
using Adamantium.UI.Input;

namespace Adamantium.UI.MacOS
{
    public class MacOSApplication : ApplicationBase
    {
        internal override MouseDevice MouseDevice { get; }
        internal override KeyboardDevice KeyboardDevice { get; }

        private IntPtr appDelegate;
        private IntPtr app;
        private Thread renderThread;

        public MacOSApplication()
        {
            Windows.WindowAdded += OnWindowAdded;
            Windows.WindowRemoved -= OnWindowRemoved;
            appDelegate = MacOSInterop.CreateApplicationDelegate();
            app = MacOSInterop.CreateApplication(appDelegate);
            //Services.Add();
            renderThread = new Thread(RenderThread);
            //renderThread.Start();
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
                //var appDelegate = MacOSInterop.CreateApplicationDelegate();
                //var app = MacOSInterop.CreateApplication(appDelegate);
                //var wndStyle = OSXWindowStyle.Borderless | OSXWindowStyle.Resizable |
                //               OSXWindowStyle.Titled |
                //               OSXWindowStyle.Miniaturizable | OSXWindowStyle.Closable;
//                var wnd = MacOSInterop.CreateWindow(new Rectangle(100, 100, 1280, 720), (uint)wndStyle, "Adamantium window");
//                var wndDelegate = MacOSInterop.CreateWindowDelegate();
//                MacOSInterop.SetWindowDelegate(wnd, wndDelegate);
//                MacOSInterop.AddWindowToAppDelegate(appDelegate, wnd);
//                MacOSInterop.ShowWindow(wnd);
                
//                var mainWindow = Window.New();
//                mainWindow.Width = 1280;
//                mainWindow.Height = 720;
//                MainWindow = mainWindow;
//                mainWindow.Show();
//                MacOSInterop.AddWindowToAppDelegate(appDelegate, mainWindow.Handle);


                renderThread.Start();
                MacOSInterop.RunApplication(app);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private void RenderThread()
        {
            while(IsRunning)
            {
                RunUpdateDrawBlock();
            }
        }
    }
}