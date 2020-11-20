using System;

namespace Adamantium.UI.Windows
{
    public class DispatcherWin32NativeSourceWrapper : Win32NativeWindowWrapper
    {
        public DispatcherWin32NativeSourceWrapper() : 
            base(
                0, 
                0, 
                0, 
                0, 
                0, 
                0, 
                0, 
                IntPtr.Zero)
        {
        }
    }
}