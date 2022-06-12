using System.Diagnostics;
using System.Threading;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.UI.Controls;
using Adamantium.UI.Rendering;
using Adamantium.UI.RoutedEvents;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Processors;

public class WindowService : UiService
{
    //private Mutex mutex = new Mutex(false, "adamantiumMutex");
        
    private PresentationParameters parameters;
    private IWindowRenderer windowRenderer;
    private readonly AutoResetEvent pauseEvent;

    protected IWindow Window { get; }
    
    protected IGraphicsDeviceService GraphicsDeviceService { get; }
    
    protected GraphicsDevice GraphicsDevice { get; set; }
    
    public WindowService(EntityWorld world, IWindow window)
        : base(world)
    {
        Window = window;
        Window.StateChanged += WindowOnStateChanged;
        GraphicsDeviceService = world.DependencyResolver.Resolve<IGraphicsDeviceService>();
        CreateResources();
        pauseEvent = new AutoResetEvent(false);
    }

    private void WindowOnStateChanged(object sender, StateChangedEventArgs e)
    {
        if (Window.State is WindowState.Maximized or WindowState.Normal)
        {
            pauseEvent.Set();
        }
    }

    private void CreateResources()
    {
        parameters = new PresentationParameters(
            PresenterType.Swapchain,
            (uint)Window.ClientWidth,
            (uint)Window.ClientHeight,
            Window.SurfaceHandle,
            Window.MSAALevel
        )
        {
            HInstanceHandle = Process.GetCurrentProcess().Handle
        };
            
        GraphicsDevice = GraphicsDeviceService.CreateRenderDevice(@parameters);
        GraphicsDevice.ClearColor = Colors.White;
        GraphicsDevice.AddDynamicStates(DynamicState.Viewport, DynamicState.Scissor);

        windowRenderer = new WindowRenderer(GraphicsDevice);
        var renderer = (WindowRenderer)windowRenderer;
        renderer.Parameters = parameters;
        windowRenderer.SetWindow(Window);
    }

    public override void UnloadContent()
    {
    }
        
    public override void Update(AppTime gameTime)
    {
        Window.Update();
    }

    public override bool BeginDraw()
    {
        return IsVisible;
    }

    public override void Draw(AppTime gameTime)
    {
        if (Window.State == WindowState.Minimized)
        {
            pauseEvent.WaitOne();
        }

        base.Draw(gameTime);
            
        if (windowRenderer == null) return;
            
        if (GraphicsDevice.BeginDraw(1, 0))
        {
            windowRenderer.Render();
        }
    }
        
    public override void EndDraw()
    {
        base.EndDraw();
        GraphicsDevice.EndDraw();
    }

    public override void DisplayContent()
    {
        base.DisplayContent();
        GraphicsDevice.Present(parameters);
    }

    public override void FrameEnded()
    {
        base.FrameEnded();
        parameters.Width = (uint)Window.ClientWidth;
        parameters.Height = (uint)Window.ClientHeight;
        
        if (windowRenderer.IsWindowUpToDate)
        {
            windowRenderer.ResizePresenter(parameters);
        }
    }
}