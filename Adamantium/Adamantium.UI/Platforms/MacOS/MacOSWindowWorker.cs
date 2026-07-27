using System;
using System.Runtime.InteropServices;
using Adamantium.Core.Events;
using Adamantium.MacOS;
using Adamantium.Mathematics;
using Adamantium.UI.AggregatorEvents;
using Adamantium.UI.Controls.Internals;
using Adamantium.UI.Core;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.UI.Platforms.MacOS;

public class MacOSWindowWorker : AdamantiumComponent, IWindowWorkerService
{
    private IWindow window;
    private IntPtr windowDelegate;
        
    private MacOSInterop.OnWindowWillResize willResizeDelegate;
    private MacOSInterop.OnWindowDidResize didResizeDelegate;
    private readonly MacOSPlatform macOsApp;
    private IEventAggregator eventAggregator;

    public MacOSWindowWorker(IUIContext context)
    {
        willResizeDelegate = OnWindowWillResize;
        didResizeDelegate = OnWindowDidResize;
        macOsApp = context.Resolve<IApplicationPlatform>() as MacOSPlatform;
        eventAggregator = context.Resolve<IEventAggregator>();
        UIContext = context;
    }

    public void SetWindow(IWindow window)
    {
        this.window = window;
        var wndStyle = OSXWindowStyle.Borderless | 
                       OSXWindowStyle.Resizable |
                       OSXWindowStyle.Titled |
                       OSXWindowStyle.Miniaturizable | 
                       OSXWindowStyle.Closable;
         var handle = MacOSInterop.CreateWindow(
            new Rectangle((int)window.Left, 0, (int)window.Width, (int)window.Height),  
            (uint)wndStyle, 
            window.Title);
         this.window.SetHandle(handle);
        this.window.SetSurfaceHandle(MacOSInterop.GetViewPtr(this.window.Handle));

        windowDelegate = MacOSInterop.CreateWindowDelegate();
        MacOSInterop.SetWindowDelegate(window.Handle, windowDelegate);
        macOsApp.AddWindow(window);

        window.ClientWidth = (uint) window.Width;
        window.ClientHeight = (uint) window.Height;

        MacOSInterop.AddWindowDidResizeCallback(windowDelegate,
            Marshal.GetFunctionPointerForDelegate(didResizeDelegate));
            
        // Same seam as Win32: every window offers itself as a native drop target. A no-op until a macOS
        // INativeDragDrop (NSDraggingDestination on the content view) is registered - the engine side is already done.
        Input.DragDrop.RegisterNativeDropTarget(this.window);

        this.window.OnApplyTemplate();
        eventAggregator.GetEvent<WindowCreatedEvent>().Publish(this.window);
        this.window.OnSourceInitialized();
        MacOSInterop.ShowWindow(window.Handle);
    }

    private void OnWindowWillResize(SizeF current, SizeF future)
    {
        window.Width = (int)future.Width;
        window.Height = (int)future.Height;

        var size = MacOSInterop.GetViewSize(window.Handle);
        window.ClientWidth = (uint)size.Width;
        window.ClientHeight = (uint) size.Height;

    }
        
    private void OnWindowDidResize(SizeF current)
    {
        window.Width = (int)current.Width;
        window.Height = (int)current.Height;

        var size = MacOSInterop.GetViewSize(window.Handle);
        window.ClientWidth = (uint)size.Width;
        window.ClientHeight = (uint) size.Height;
    }

    public void SetTitle(string title)
    {
        
    }

    public void ShowWindow(WindowState windowState)
    {
        
    }

    public void HideWindow()
    {
        throw new NotImplementedException();
    }

    // TODO(macOS): NSApplication.activate + [NSWindow makeKeyAndOrderFront:]. No-op until the macOS window pipeline lands.
    public void Activate()
    {
    }

    // OS-level mouse capture so a press-drag keeps tracking once the pointer leaves the window (the shared "when" is in
    // MouseDevice.SyncOsMouseCapture - already wired here so macOS won't re-hit the Win32 capture bug once its mouse
    // input is implemented). TODO(macOS): NSWindow has no SetCapture; route drag tracking via the window's tracking
    // area / an NSEvent local monitor (or rely on AppKit delivering mouseDragged/mouseUp to the mouseDown view) when
    // the macOS mouse pipeline lands. No-op until then (macOS mouse input is not processed yet).
    public void SetMouseCapture(bool capture)
    {
    }

    // TODO(macOS): relative mouse mode (CGAssociateMouseAndMouseCursorPosition + hide cursor) for game mouse-look.
    public void SetRelativeMouseMode(bool enabled, Adamantium.Mathematics.Vector2 restoreScreen)
    {
    }

    // TODO(macOS): start an AppKit window drag (NSWindow performWindowDragWithEvent:) for custom-chrome caption drag.
    // No-op until the macOS input/window pipeline lands.
    public void BeginMoveDrag()
    {
    }

    public IUIContext UIContext { get; }

    public static implicit operator IntPtr(MacOSWindowWorker worker)
    {
        return worker.windowDelegate;
    }
}