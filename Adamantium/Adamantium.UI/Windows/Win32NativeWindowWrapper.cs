using Adamantium.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.UI.Windows
{
    public class Win32NativeWindowWrapper : DispatcherComponent, IDisposable
    {
        private const int ERROR_CLASS_ALREADY_EXISTS = 1410;

        public Win32NativeWindowWrapper(
            WindowClassStyle classStyle, 
            WindowStyleEx wndStyleEx,
            WindowStyle wndStyle,
            Int32 locationX,
            Int32 locationY,
            Int32 width,
            Int32 height,
            IntPtr parent) : this(
                Guid.NewGuid().ToString(),
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

        public Win32NativeWindowWrapper(
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

        private Win32NativeWindowWrapper(IntPtr handle)
        {
            hooks = new List<WndProcHook>();
            wndProcDelegate = WndProc;
            Handle = handle;
        }

        private List<WndProcHook> hooks;
        private Win32Interop.WndProc wndProcDelegate;

        public IntPtr Handle { get; private set; }

        public void AddHook(WndProcHook hook)
        {
            if (!hooks.Contains(hook))
            {
                hooks.Insert(0, hook);
            }
        }

        public void RemoveHook(WndProcHook hook)
        {
            hooks.Remove(hook);
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            IntPtr result = IntPtr.Zero;
            bool handled = false;
            for (int i = 0; i< hooks.Count; ++i)
            {
                var wndProc = hooks[i];
                result = wndProc(hWnd, msg, wParam, lParam, ref handled);
                
                if (handled) break;
            }

            if (handled)
            {
                return result;
            }

            return Win32Interop.DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        private void CreateAndRegisterWndClass(string className, WindowClassStyle classStyle)
        {
            // Create WNDCLASS
            WndClass wndClass = new WndClass
            {
                style = classStyle,
                lpszClassName = className,
                hCursor = Win32Interop.LoadCursor(IntPtr.Zero, NativeCursors.Arrow),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate)
            };

            ushort classAtom = Win32Interop.RegisterClassW(ref wndClass);

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
            Handle = Win32Interop.CreateWindowExW(
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
            Win32Interop.DestroyWindow(Handle);
            Handle = IntPtr.Zero;
        }

        public static Win32NativeWindowWrapper FromHwnd(IntPtr handle)
        {
            return new Win32NativeWindowWrapper(handle);
        }

        private void ReleaseUnmanagedResources()
        {
            Destroy();
        }

        protected virtual void Dispose(bool disposing)
        {
            ReleaseUnmanagedResources();
            if (disposing)
            {
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Win32NativeWindowWrapper()
        {
            Dispose(false);
        }
    }
}
