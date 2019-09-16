using System;
using System.Diagnostics;
using Adamantium.MacOS;
using Adamantium.Mathematics;

namespace Adamantium.UI.Windows
{
    public class OSXWindowWorker : DependencyComponent
    {
        private OSXWindow window;
        private IntPtr windowDelegate;
        
        private MacOSInterop.OnWindowWillResize resizeDelegate;
        
        private void WindowWillResize(float width, float height)
        {
            Debug.WriteLine($"Window Resize width: {width}, height: {height}");
        }

        public void SetWindow(OSXWindow window)
        {
            this.window = window;
            var wndStyle = OSXWindowStyle.Borderless | OSXWindowStyle.Resizable |
                           OSXWindowStyle.Titled |
                           OSXWindowStyle.Miniaturizable | OSXWindowStyle.Closable;
            this.window.Handle = MacOSInterop.CreateWindow(new Rectangle(0, 0, 1280, 720),  (uint)wndStyle, "Adamantium engine");
            windowDelegate = MacOSInterop.CreateWindowDelegate();
        }
    }
}