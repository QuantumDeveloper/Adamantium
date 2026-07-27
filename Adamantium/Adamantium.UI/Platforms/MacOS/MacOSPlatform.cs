using System;
using System.Threading;
using Adamantium.Core.DependencyInjection;
using Adamantium.MacOS;
using Adamantium.UI.Core;

namespace Adamantium.UI.Platforms.MacOS;

public class MacOSPlatform : IApplicationPlatform
{
    private IntPtr appDelegate;
    private IntPtr app;
        
    public MacOSPlatform()
    {
        appDelegate = MacOSInterop.CreateApplicationDelegate();
        app = MacOSInterop.CreateApplication(appDelegate);
        Cursor.Platform = new MacOSCursors();   // NSCursor shapes for the neutral CursorType catalog
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

    // TODO(macOS): NSWindow.windowNumberAtPoint:belowWindowWithWindowNumber: -> the window number, mapped back to our
    // handle. Zero until then, which makes the drag fall back to client-bounds containment.
    public IntPtr WindowFromScreenPoint(Adamantium.Mathematics.Vector2 point) => IntPtr.Zero;

    public static void Initialize(IContainerRegistry resolver)
    {
        resolver.RegisterSingleton<IApplicationPlatform, MacOSPlatform>();
    }
}