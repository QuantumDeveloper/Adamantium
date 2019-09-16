using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;

namespace Adamantium.MacOS
{
    public static class MacOSInterop
    {
        public const string WrapperFramework = "MacOSAppWrapper.framework/MacOSAppWrapper";

        [DllImport(WrapperFramework, EntryPoint = "RunApplication")]
        public static extern int RunApplication(IntPtr app);
        
        [DllImport(WrapperFramework, EntryPoint = "CreateApplication")]
        public static extern IntPtr CreateApplication(IntPtr appDelegate);
        
        [DllImport(WrapperFramework, EntryPoint = "CreateWindow")]
        public static extern IntPtr CreateWindow(Rectangle windowRect, uint windowStyle, string title);
        
        [DllImport(WrapperFramework, EntryPoint = "CreateApplicationDelegate")]
        public static extern IntPtr CreateApplicationDelegate();

        [DllImport(WrapperFramework, EntryPoint = "CreateWindowDelegate")]
        public static extern IntPtr CreateWindowDelegate();
        
        [DllImport(WrapperFramework, EntryPoint = "SetWindowDelegate")]
        public static extern IntPtr SetWindowDelegate(IntPtr window, IntPtr windowDelegate);

        [DllImport(WrapperFramework, EntryPoint = "GetViewPtr")]
        public static extern IntPtr GetViewPtr(IntPtr window);
        
        [DllImport(WrapperFramework, EntryPoint = "AddWindowToAppDelegate")]
        public static extern IntPtr AddWindowToAppDelegate(IntPtr appDelegate, IntPtr window);
        
        [DllImport(WrapperFramework, EntryPoint = "GetDelegateFromApp")]
        public static extern IntPtr GetDelegateFromApp(IntPtr app);

        [DllImport(WrapperFramework, EntryPoint = "AddWindowResizeCallback")]
        public static extern void AddWindowResizeCallback(IntPtr appDelegate, IntPtr resizeCallback);
        
        public delegate void OnWindowWillResize(float width, float height);
    }
}