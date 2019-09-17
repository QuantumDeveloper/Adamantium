using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Adamantium.MacOS;
using Adamantium.Mathematics;

namespace Adamantium.UI.Windows
{
    public class OSXWindowWorker : DependencyComponent
    {
        private OSXWindow window;
        private IntPtr windowDelegate;
        
        private MacOSInterop.OnWindowWillResize willResizeDelegate;

        public OSXWindowWorker()
        {
            willResizeDelegate = OnWindowWillResize;
        }

        public void SetWindow(OSXWindow window)
        {
            this.window = window;
            var wndStyle = OSXWindowStyle.Borderless | OSXWindowStyle.Resizable |
                           OSXWindowStyle.Titled |
                           OSXWindowStyle.Miniaturizable | OSXWindowStyle.Closable;
            this.window.Handle = MacOSInterop.CreateWindow(new Rectangle((int)window.Left, 0, window.ClientWidth, window.ClientHeight),  (uint)wndStyle, "Adamantium engine");
            windowDelegate = MacOSInterop.CreateWindowDelegate();
            MacOSInterop.SetWindowDelegate(window.Handle, windowDelegate);

            MacOSInterop.AddWindowWillResizeResizeCallback(windowDelegate,
                Marshal.GetFunctionPointerForDelegate(willResizeDelegate));
            
            this.window.ApplyTemplate();
            Application.Current.Windows.Add(this.window);
            this.window.OnSourceInitialized();
            MacOSInterop.ShowWindow(window.Handle);
        }

        private void OnWindowWillResize(float width, float height)
        {
            window.Width = (int)width;
            window.Height = (int)height;

        }

        public static implicit operator IntPtr(OSXWindowWorker worker)
        {
            return worker.windowDelegate;
        }
    }
}