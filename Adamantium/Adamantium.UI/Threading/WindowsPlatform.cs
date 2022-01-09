using System;
using System.Threading;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.Engine.Core;
using Adamantium.UI.Windows;
using Adamantium.Win32;

namespace Adamantium.UI.Threading;

public class WindowsPlatform : IApplicationPlatform
{
    private Thread uiThread;
    private DispatcherWin32NativeSourceWrapper window;
    private uint dispatchMessage;
    private IService appService;

    public WindowsPlatform()
    {
        uiThread = Thread.CurrentThread;
        window = new DispatcherWin32NativeSourceWrapper();
        window.AddHook(WndProc);
        dispatchMessage = Messages.RegisterWindowMessage("DispatcherProcessingMessage");
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

    public static void Initialize()
    {
        AdamantiumServiceLocator.Current.RegisterSingleton<IApplicationPlatform, WindowsPlatform>();
    }
}