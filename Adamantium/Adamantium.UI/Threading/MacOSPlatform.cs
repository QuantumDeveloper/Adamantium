using System;
using System.Threading;
using Adamantium.Core.DependencyInjection;
using Adamantium.MacOS;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Threading;

internal class MacOSPlatform : IApplicationPlatform
{
    private IntPtr appDelegate;
    private IntPtr app;
        
    public MacOSPlatform()
    {
        appDelegate = MacOSInterop.CreateApplicationDelegate();
        app = MacOSInterop.CreateApplication(appDelegate);
    }
        
    public void Run(CancellationToken token)
    {
        MacOSInterop.RunApplication(app);
    }

    public void AddWindow(IWindow window)
    {
        if (window == null) return;
            
        MacOSInterop.AddWindowToAppDelegate(appDelegate, window.Handle);
    }

    public bool IsOnUIThread { get; }
    public void Signal()
    {
        throw new NotImplementedException();
    }

    public event Action Signaled;

    public static void Initialize()
    {
        AdamantiumDependencyResolver.Current.RegisterSingleton<IApplicationPlatform, MacOSPlatform>();
    }
}