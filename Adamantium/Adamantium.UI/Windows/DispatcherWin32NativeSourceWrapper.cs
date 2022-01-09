using System;

namespace Adamantium.UI.Windows;

public class DispatcherWin32NativeSourceWrapper : Win32NativeWindowWrapper
{
    private const string MessageWindowName = "AdamantiumMessageWindow";
        
    public DispatcherWin32NativeSourceWrapper() : 
        base(
            $"{MessageWindowName} {Guid.NewGuid()}",
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