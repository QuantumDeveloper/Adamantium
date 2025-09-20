using System.Threading;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Extensions;
using Adamantium.UI.Rendering;

namespace Adamantium.UI.EntityServices;

public class WindowRenderService : UiRenderService
{
    private IWindowRenderer windowRenderer;
    private IWindowRenderer _pendingRenderer;
    private IThemeManager _themeManager;
    private readonly AutoResetEvent pauseEvent;

    public IWindow Window { get; }
    
    public WindowRenderService(EntityWorld world, IWindow window)
        : base(world)
    {
        Window = window;
        _themeManager = DependencyResolver.Resolve<IThemeManager>();
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
        GraphicsDevice = GraphicsDeviceService.CreateRenderDevice();
        GraphicsDevice.ClearColor = Colors.CornflowerBlue;

        windowRenderer = Window.Renderer ?? new ForwardWindowRenderer(GraphicsDevice, new RenderUnitFactory(GraphicsDevice, DependencyResolver.Resolve<IResourceFactory>()));
        windowRenderer.SetWindow(Window);
        Window.DefaultRenderer = windowRenderer;
        Window.RendererChanged += WindowOnRendererChanged;
    }
    
    private void WindowOnRendererChanged(object sender, WindowRendererChangedEventArgs e)
    {
        _pendingRenderer = e.NewRenderer;
    }
    
    public override void Present()
    {
        windowRenderer?.Present();
    }

    public override void UnloadContent()
    {
        windowRenderer?.Dispose();
    }

    public override bool IsUpdateService => true;
    public override EntityServiceType ServiceType => EntityServiceType.Update | EntityServiceType.Render;

    public override void Update(AppTime gameTime)
    {
        Window.Update(_themeManager, gameTime);
    }
    
    public override bool BeginDraw()
    {
        GraphicsDevice.SetRenderTargets(windowRenderer.Presenter.RenderTarget);
        GraphicsDevice.SetDepthBuffer(windowRenderer.Presenter.DepthBuffer);
        GraphicsDevice.MSAALevel = windowRenderer.Presenter.MSAALevel;
        GraphicsDevice.Presenter = windowRenderer.Presenter;
        return base.BeginDraw();
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
    
    public override void EndDraw()
    {
        GraphicsDevice.EndDraw();
        windowRenderer.PrepareData();
        GraphicsDevice.BlitImage(GraphicsDevice.CurrentRenderTarget.ResolveTexture,
            windowRenderer.Presenter.GetCurrentImage());
    }

    public override void FrameEnded()
    {
        base.FrameEnded();
        GraphicsDevice.FrameEnded();
        windowRenderer.OnFrameEnded();
        if (!windowRenderer.IsRendererUpToDate)
        {
            windowRenderer.ResizePresenter((uint)Window.ClientWidth, (uint)Window.ClientHeight);
        }

        if (_pendingRenderer == null) return;
        
        windowRenderer = _pendingRenderer;
        _pendingRenderer = null;
    }
}