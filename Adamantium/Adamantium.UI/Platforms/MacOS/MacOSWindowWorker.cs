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

    public IUIContext UIContext { get; }

    public static implicit operator IntPtr(MacOSWindowWorker worker)
    {
        return worker.windowDelegate;
    }
}