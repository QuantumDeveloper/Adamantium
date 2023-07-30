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

public class WindowRenderService : UiRenderService
{
    private PresentationParameters parameters;
    private IWindowRenderer windowRenderer;
    private IWindowRenderer _pendingRenderer;
    private readonly AutoResetEvent pauseEvent;

    public IWindow Window { get; }
    
    public WindowRenderService(EntityWorld world, IWindow window)
        : base(world)
    {
        Window = window;
        Window.StateChanged += WindowOnStateChanged;
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

        windowRenderer = Window.Renderer ?? new ForwardWindowRenderer(GraphicsDevice);
        windowRenderer.SetWindow(Window);
        Window.DefaultRenderer = windowRenderer;
        
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

    public override bool IsUpdateService => true;

    public override void Update(AppTime gameTime)
    {
        Window.Update(gameTime);
    }

    public override void Draw(AppTime gameTime)
    {
        if (Window.State == WindowState.Minimized)
        {
            pauseEvent.WaitOne();
        }

        base.Draw(gameTime);

        windowRenderer?.Render(gameTime);
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