using Adamantium.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.UI.Windows
{
    public class WindowNativeSource
    {
        private const int ERROR_CLASS_ALREADY_EXISTS = 1410;

        public WindowNativeSource(
            WindowClassStyle classStyle, 
            WindowStyleEx wndStyleEx,
            WindowStyle wndStyle,
            Int32 locationX,
            Int32 locationY,
            Int32 width,
            Int32 height,
            IntPtr parent) : this(
                Win32Window.DefaultClassName,
                classStyle,
                wndStyleEx,
                wndStyle,
                locationX,
                locationY,
                width,
                height,
                parent)
        {
        }

        public WindowNativeSource(
            string className,
            WindowClassStyle classStyle,
            WindowStyleEx wndStyleEx,
            WindowStyle wndStyle,
            Int32 locationX,
            Int32 locationY,
            Int32 width,
            Int32 height,
            IntPtr parent)
        {
            hooks = new List<WndProcHook>();
            wndProcDelegate = WndProc;
            CreateNativeWindow(
                className,
                classStyle,
                wndStyleEx,
                wndStyle,
                locationX,
                locationY,
                width,
                height,
                parent);
        }

        private WindowNativeSource(IntPtr handle)
        {
            hooks = new List<WndProcHook>();
            wndProcDelegate = WndProc;
            Handle = handle;
        }

        private List<WndProcHook> hooks;
        private Interop.WndProc wndProcDelegate;

        public IntPtr Handle { get; private set; }

        public void AddHook(WndProcHook hook)
        {
            if (!hooks.Contains(hook))
            {
                hooks.Add(hook);
            }
        }

        public void RemoveHook(WndProcHook hook)
        {
            hooks.Remove(hook);
        }

        private IntPtr WndProc(IntPtr hWnd, WindowMessages msg, IntPtr wParam, IntPtr lParam)
        {
            bool handled = false;
            for (int i = 0; i< hooks.Count; ++i)
            {
                var wndProc = hooks[i];
                wndProc(hWnd, msg, wParam, lParam, ref handled);
            }

            if (handled)
            {
                return IntPtr.Zero;
            }

            return Interop.DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        private void CreateAndRegisterWndClass(string className, WindowClassStyle classStyle)
        {
            // Create WNDCLASS
            WndClass wndClass = new WndClass
            {
                style = classStyle,
                lpszClassName = className,
                hCursor = Interop.LoadCursor(IntPtr.Zero, NativeCursors.Arrow),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate)
            };

            ushort classAtom = Interop.RegisterClassW(ref wndClass);

            int lastError = Marshal.GetLastWin32Error();

            if (classAtom == 0 && lastError != ERROR_CLASS_ALREADY_EXISTS)
            {
                throw new Exception("Could not register window class");
            }
        }

        private void CreateNativeWindow(
            string className,
            WindowClassStyle classStyle,
            WindowStyleEx wndStyleEx,
            WindowStyle wndStyle,
            Int32 locationX,
            Int32 locationY,
            Int32 width,
            Int32 height,
            IntPtr parent)
        {
            CreateAndRegisterWndClass(className, classStyle);

            // Create window
            Handle = Interop.CreateWindowExW(
               wndStyleEx,
               className,
               string.Empty,
               wndStyle,
               locationX,
               locationY,
               width,
               height,
               parent,
               IntPtr.Zero,
               IntPtr.Zero,
               IntPtr.Zero
               );
        }

        public void Destroy()
        {
            Interop.DestroyWindow(Handle);
            Handle = IntPtr.Zero;
        }

        public static WindowNativeSource FromHwnd(IntPtr handle)
        {
            return new WindowNativeSource(handle);
        }
    }
}
