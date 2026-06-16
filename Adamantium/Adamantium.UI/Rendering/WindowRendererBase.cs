using System.Diagnostics;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Effects.Generated;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

public abstract class WindowRendererBase : IWindowRenderer
{
    protected Viewport Viewport { get; set; }
    protected Rect2D Scissor { get; set; }
    protected Rect2D ClipRect { get; set; }
    protected Matrix4x4F ProjectionMatrix { get; set; }
    protected IGraphicsDevice GraphicsDevice { get; }
    
    protected IRenderUnitFactory RenderUnitFactory { get; } 
    protected PresentationParameters Parameters { get; set; }
    
    protected UIBasicEffect UiEffect { get; set; }

    public virtual void PrepareData()
    {
        
    }

    public GraphicsPresenter Presenter { get; private set; }
    
    protected WindowRendererBase(IGraphicsDevice device, IRenderUnitFactory renderUnitFactory)
    {
        Viewport = new Viewport();
        Scissor = new Rect2D();
        ClipRect = new Rect2D();
        ClipRect.Offset = new Offset2D();
        ClipRect.Extent = new Extent2D();
        GraphicsDevice = device;
        DrawingContext = new DrawingContext();
        RenderUnitFactory = renderUnitFactory;
    }
    
    protected IWindow Window { get; set; }

    public IDrawingContext DrawingContext { get; }
    public bool IsRendererUpToDate { get; protected set; }
    public bool FirstFrameProcessed { get; private set; }

    protected virtual void UnsubscribeFromEvents()
    {
        
    }

    protected virtual void SubscribeToEvents()
    {
        
    }
    
    public virtual void SetWindow(IWindow window)
    {
        if (window == null) return;
            
        UnsubscribeFromEvents();
        Window = window;
        Window.Renderer = this;
        FillParameters();
        SubscribeToEvents();
        InitializeWindowResources();
    }

    /// <summary>
    /// The presenter kind this renderer builds. Defaults to the on-screen swapchain; a headless renderer
    /// overrides it to <see cref="PresenterType.Headless"/>, in which case the surface is window-less.
    /// </summary>
    protected virtual PresenterType PresenterKind => PresenterType.Swapchain;

    private void FillParameters()
    {
        Parameters = new PresentationParameters(
            PresenterKind,
            (uint)Window.ClientWidth,
            (uint)Window.ClientHeight,
            Window.SurfaceHandle,
            Window.MSAALevel
        )
        {
            HInstanceHandle = Process.GetCurrentProcess().Handle
        };

        Presenter = GraphicsPresenter.Create(GraphicsDevice, Parameters, "Window_presenter");
    }

    protected virtual void InitializeWindowResources()
    {
        
    }

    public abstract void Render(AppTime appTime);

    public abstract void PreRender();


    public void ResizePresenter(PresentationParameters parameters)
    {
        Presenter.Resize(parameters);
        IsRendererUpToDate = true;
    }

    public void ResizePresenter(uint width, uint height)
    {
        Presenter.Resize(width, height);
        IsRendererUpToDate = true;
    }

    public virtual void OnFrameEnded()
    {
        FirstFrameProcessed = true;
    }

    public virtual void Present()
    {
        Presenter?.Present();
        
        if (Window.ShouldDisplayWindow)
        {
            // Dispatcher.CurrentDispatcher.Invoke(() =>
            // {
            //     Window.Show();
            // });
            Window.UIContext.UIApplication.ExecuteOnUIThread(() => Window.Show());
        }
    }
    
    public void Dispose()
    {
        
    }
}