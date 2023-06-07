using System.Diagnostics;
using System.Threading;
using Adamantium.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.UI.Controls;
using Adamantium.UI.Events;
using Adamantium.UI.Extensions;
using Adamantium.UI.Rendering;
using Adamantium.UI.RoutedEvents;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Processors;

public class WindowService : UiService
{
    //private Mutex mutex = new Mutex(false, "adamantiumMutex");
        
    private PresentationParameters parameters;
    private IWindowRenderer windowRenderer;
    private IWindowRenderer _pendingRenderer;
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

        if (Window.Renderer == null)
        {
            windowRenderer = new ForwardWindowRenderer(GraphicsDevice);
            windowRenderer.SetWindow(Window);
            Window.Renderer = windowRenderer;
        }
        else
        {
            windowRenderer = Window.Renderer;
            windowRenderer.SetWindow(Window);
        }
        
        Window.RendererChanged += WindowOnRendererChanged;
    }

    private void WindowOnRendererChanged(object sender, WindowRendererChangedEventArgs e)
    {
        _pendingRenderer = e.NewRenderer;
    }

    public override void UnloadContent()
    {
        windowRenderer?.Dispose();
    }
        
    public override void Update(AppTime gameTime)
    {
        Window.Update(gameTime);
    }

    public override bool BeginDraw()
    {
        return Window.Visibility == Visibility.Visible;
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
            windowRenderer?.Render(gameTime);
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
        
        if (!windowRenderer.IsRendererUpToDate)
        {
            windowRenderer.ResizePresenter(parameters);
        }

        if (_pendingRenderer != null)
        {
            windowRenderer = _pendingRenderer;
            _pendingRenderer = null;
        }
    }
}