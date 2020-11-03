using System;
using System.Runtime.InteropServices;
using Adamantium.MacOS;
using Adamantium.Mathematics;

namespace Adamantium.UI.MacOS
{
    public class MacOSWindowWorker : DependencyComponent
    {
        private MacOSWindow window;
        private IntPtr windowDelegate;
        
        private MacOSInterop.OnWindowWillResize willResizeDelegate;
        private MacOSInterop.OnWindowDidResize didResizeDelegate;

        public MacOSWindowWorker()
        {
            willResizeDelegate = OnWindowWillResize;
            didResizeDelegate = OnWindowDidResize;
        }

        public void SetWindow(MacOSWindow window)
        {
            this.window = window;
            var wndStyle = OSXWindowStyle.Borderless | OSXWindowStyle.Resizable |
                           OSXWindowStyle.Titled |
                           OSXWindowStyle.Miniaturizable | OSXWindowStyle.Closable;
            this.window.Handle = MacOSInterop.CreateWindow(new Rectangle((int)window.Left, 0, (int)window.Width, (int)window.Height),  (uint)wndStyle, "Adamantium engine");
            windowDelegate = MacOSInterop.CreateWindowDelegate();
            MacOSInterop.SetWindowDelegate(window.Handle, windowDelegate);

            window.ClientWidth = (int) window.Width;
            window.ClientHeight = (int) window.Height;

//            MacOSInterop.AddWindowWillResizeResizeCallback(windowDelegate,
//                Marshal.GetFunctionPointerForDelegate(willResizeDelegate));

            MacOSInterop.AddWindowDidResizeCallback(windowDelegate,
                Marshal.GetFunctionPointerForDelegate(didResizeDelegate));
            
            this.window.ApplyTemplate();
            Application.Current.Windows.Add(this.window);
            this.window.OnSourceInitialized();
            MacOSInterop.ShowWindow(window.Handle);
        }

        private void OnWindowWillResize(SizeF current, SizeF future)
        {
            window.Width = (int)future.Width;
            window.Height = (int)future.Height;

            var size = MacOSInterop.GetViewSize(window.Handle);
            window.ClientWidth = (int)size.Width;
            window.ClientHeight = (int) size.Height;

            if (window.ClientWidth != 0 && window.ClientHeight != 0)
            {
                var oldSize = new Size(current.Width, current.Height); 
                window.OnClientSizeChanged(new SizeChangedEventArgs(new Size(window.ClientWidth, window.ClientHeight), oldSize, true, true));
            }

        }
        
        private void OnWindowDidResize(SizeF current)
        {
            Console.WriteLine("Did resize");
            window.Width = (int)current.Width;
            window.Height = (int)current.Height;

            var size = MacOSInterop.GetViewSize(window.Handle);
            window.ClientWidth = (int)size.Width;
            window.ClientHeight = (int) size.Height;

            if (window.ClientWidth != 0 && window.ClientHeight != 0)
            {
                var oldSize = new Size(current.Width, current.Height); 
                window.OnClientSizeChanged(new SizeChangedEventArgs(new Size(window.ClientWidth, window.ClientHeight), oldSize, true, true));
            }

        }

        public static implicit operator IntPtr(MacOSWindowWorker worker)
        {
            return worker.windowDelegate;
        }
    }
}