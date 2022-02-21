using System;
using System.Runtime.InteropServices;
using Adamantium.Core.DependencyInjection;
using Adamantium.Core.Events;
using Adamantium.MacOS;
using Adamantium.UI.AggregatorEvents;
using Adamantium.UI.Controls;
using Adamantium.UI.Threading;
using Rectangle = Adamantium.Mathematics.Rectangle;

namespace Adamantium.UI.MacOS;

public class MacOSWindowWorker : AdamantiumComponent, IWindowWorkerService
{
    private WindowBase window;
    private IntPtr windowDelegate;
        
    private MacOSInterop.OnWindowWillResize willResizeDelegate;
    private MacOSInterop.OnWindowDidResize didResizeDelegate;
    private MacOSPlatform macOsApp;
    private IEventAggregator eventAggregator;

    public MacOSWindowWorker()
    {
        willResizeDelegate = OnWindowWillResize;
        didResizeDelegate = OnWindowDidResize;
        macOsApp = AdamantiumDependencyResolver.Current.Resolve<IApplicationPlatform>() as MacOSPlatform;
        eventAggregator = AdamantiumDependencyResolver.Current.Resolve<IEventAggregator>();
    }

    public void SetWindow(WindowBase window)
    {
        this.window = window;
        var wndStyle = OSXWindowStyle.Borderless | 
                       OSXWindowStyle.Resizable |
                       OSXWindowStyle.Titled |
                       OSXWindowStyle.Miniaturizable | 
                       OSXWindowStyle.Closable;
        this.window.Handle = MacOSInterop.CreateWindow(
            new Rectangle((int)window.Left, 0, (int)window.Width, (int)window.Height),  
            (uint)wndStyle, 
            window.Title);
        this.window.SurfaceHandle = MacOSInterop.GetViewPtr(this.window.Handle);

        windowDelegate = MacOSInterop.CreateWindowDelegate();
        MacOSInterop.SetWindowDelegate(window.Handle, windowDelegate);
        macOsApp.AddWindow(window);

        window.ClientWidth = (uint) window.Width;
        window.ClientHeight = (uint) window.Height;

        MacOSInterop.AddWindowDidResizeCallback(windowDelegate,
            Marshal.GetFunctionPointerForDelegate(didResizeDelegate));
            
        this.window.OnApplyTemplate();
        eventAggregator.GetEvent<WindowAddedEvent>().Publish(this.window);
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
        Console.WriteLine("Did resize");
        window.Width = (int)current.Width;
        window.Height = (int)current.Height;

        var size = MacOSInterop.GetViewSize(window.Handle);
        window.ClientWidth = (uint)size.Width;
        window.ClientHeight = (uint) size.Height;
    }

    public static implicit operator IntPtr(MacOSWindowWorker worker)
    {
        return worker.windowDelegate;
    }
}