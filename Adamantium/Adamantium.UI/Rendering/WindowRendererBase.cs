using System.Diagnostics;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Effects.Generated;
using Adamantium.Vulkan.Core;

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

    // Phase 3.2 record/apply split (docs/RENDER_THREAD_PLAN.md). Default no-ops; ForwardWindowRenderer implements them.
    public virtual void RecordData()
    {

    }

    public virtual void ApplyData()
    {

    }

    // No-op by default: the on-screen renderer keeps its cache across frames (attachment-based reconciliation). A
    // renderer that rebuilds a fresh tree every frame (headless designer) overrides this to drop its units.
    public virtual void ResetCache()
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

    // Render resolution multiplier (1 = on-screen 1:1). The presenter + viewport are sized ClientSize x RenderScale,
    // the projection stays ClientSize - so the designer renders crisply at design x scale (zoom) without changing layout.
    public double RenderScale { get; set; } = 1.0;

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

    // Re-points the renderer at a different window REUSING the existing presenter (just resized to the new window's
    // size) instead of recreating it like SetWindow. The designer switches the previewed window this way - one
    // presenter for the whole session, no render-target churn. First call (no presenter yet) falls back to SetWindow.
    public virtual void Retarget(IWindow window)
    {
        if (window == null) return;
        if (Presenter == null) { SetWindow(window); return; }

        UnsubscribeFromEvents();
        Window = window;
        Window.Renderer = this;
        SubscribeToEvents();
        ResizePresenter((uint)(window.ClientWidth * RenderScale), (uint)(window.ClientHeight * RenderScale));
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
            (uint)(Window.ClientWidth * RenderScale),
            (uint)(Window.ClientHeight * RenderScale),
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
    
    public virtual void Dispose()
    {
        UnsubscribeFromEvents();
        Presenter?.Dispose();
        Presenter = null;
    }
}