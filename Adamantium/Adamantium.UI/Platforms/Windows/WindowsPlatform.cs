using System;
using System.Threading;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.Win32;

namespace Adamantium.UI.Platforms.Windows;

public class WindowsPlatform : IApplicationPlatform
{
    private Thread uiThread;
    private DispatcherWin32NativeSourceWrapper window;
    private uint dispatchMessage;

    public WindowsPlatform()
    {
        uiThread = Thread.CurrentThread;
        window = new DispatcherWin32NativeSourceWrapper();
        window.AddHook(WndProc);
        dispatchMessage = Messages.RegisterWindowMessage("DispatcherProcessingMessage");
        Clipboard.Current = new WindowsClipboard();   // swap the in-process default for the real OS clipboard
        Cursor.Platform = new WindowsCursors();       // IDC_* shapes for the neutral CursorType catalog

        // Live pointer/key state + the user's double-click speed - one object, three contracts.
        var input = new WindowsInput();
        Mouse.Platform = input;
        Keyboard.Platform = input;
        PlatformSettings.Platform = input;
        DesktopWallpaper.Platform = new WindowsDesktopWallpaper();   // what Mica shows: the picture behind the WINDOW
        WindowsOle.Initialize();   // OLE on THIS (the UI) thread - the precondition for OS drag-drop on every window
    }

    public void Run(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            while (Messages.PeekMessage(out var msg, IntPtr.Zero, 0, 0, PeekMessageFlag.Remove))
            {
                Messages.TranslateMessage(ref msg);
                Messages.DispatchMessage(ref msg);
            }
        }
    }
        
    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        WindowMessages message = (WindowMessages) msg;
        if (message == WindowMessages.Destroy)
        {
                
        }
        else if (msg == dispatchMessage)
        {
            Signaled?.Invoke();
        }
            
        return IntPtr.Zero;
    }

    public bool IsOnUIThread => uiThread == Thread.CurrentThread;
    
    public void Signal()
    {
        Messages.PostMessage(window.Handle, dispatchMessage, IntPtr.Zero, IntPtr.Zero);
    }

    public event Action Signaled;

    public IntPtr WindowFromScreenPoint(Adamantium.UI.Core.PixelPoint point) =>
        Win32Interop.WindowFromPoint(new Adamantium.Mathematics.NativePoint((int)point.X, (int)point.Y));

    public static void Initialize(IContainerRegistry resolver)
    {
        resolver.RegisterSingleton<IApplicationPlatform, WindowsPlatform>();
    }
}