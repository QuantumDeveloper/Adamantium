using System.Diagnostics;
using System.Threading;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.Imaging;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
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
    // Optional shared render device (designer: reuse ONE device for the whole session). Null => the service creates its
    // own (the runtime path, unchanged).
    private readonly IGraphicsDevice _injectedDevice;

    public IWindow Window { get; private set; }

    /// <summary>The viewport zoom the content is rendered at (designer zoom; 1 = on-screen 1:1). The adorner overlay
    /// stage renders at the same scale so its analytic AA (fringe width) matches the content.</summary>
    public double RenderScale => windowRenderer?.RenderScale ?? 1.0;

    // Runtime path: EntityWorld.CreateService resolves the service via Activator.CreateInstance, which matches a
    // constructor by EXACT argument count (it ignores optional/default parameters). The runtime constructs this with
    // (world, window), so that 2-arg form needs its OWN constructor - an optional 3rd param would not be found.
    public WindowRenderService(EntityWorld world, IWindow window) : this(world, window, null)
    {
    }

    // Designer path: a shared render device is injected (see DesignerRenderService) so the whole session reuses one
    // device instead of creating one per window.
    public WindowRenderService(EntityWorld world, IWindow window, IGraphicsDevice renderDevice)
        : base(world)
    {
        Window = window;
        _injectedDevice = renderDevice;
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
        GraphicsDevice = _injectedDevice ?? GraphicsDeviceService.CreateRenderDevice();

        windowRenderer = Window.Renderer ?? CreateRenderer();
        windowRenderer.SetWindow(Window);
        Window.DefaultRenderer = windowRenderer;
        Window.RendererChanged += WindowOnRendererChanged;

        // Tooling overlays (AdornerLayer: selection frames etc.) are a second stage in the processor collection,
        // drawn on top of the content in the same frame. Same for runtime and the headless designer.
        AttachProcessor(new AdornerRenderProcessor());
        // The popup stage (tooltips, in-window popups) draws on top of the content AND the adorners (higher Order).
        AttachProcessor(new PopupRenderProcessor());
    }

    // The renderer this service drives. The headless (designer) service overrides this to render into a texture
    // instead of a swapchain - the only difference is the presenter kind (read-back is identical: ResolveTexture).
    protected virtual IWindowRenderer CreateRenderer() =>
        new ForwardWindowRenderer(GraphicsDevice, new RenderUnitFactory(GraphicsDevice, DependencyResolver.Resolve<IResourceFactory>()));
    
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
        base.Update(gameTime);   // processors' Update: the adorner stage builds its overlay cache from the fresh layout
    }
    
    public override bool BeginDraw()
    {
        // Refresh the analytic-AA switch from this window before recording (PreRender reads it in the beforeRenderPass
        // hook). Windows render serially, so this app-global flag is correct per window.
        Rendering.RenderUnits.AnalyticAa.Enabled = Window?.AnalyticAntialiasing ?? true;
        // Backdrop = the window's Background (theme-owned), read each frame so a theme swap / runtime change reflects it.
        GraphicsDevice.ClearColor = (Window?.Background as SolidColorBrush)?.Color ?? Colors.Black;
        GraphicsDevice.SetRenderTargets(windowRenderer.Presenter.RenderTarget);
        GraphicsDevice.SetDepthBuffer(windowRenderer.Presenter.DepthBuffer);
        GraphicsDevice.MSAALevel = windowRenderer.Presenter.MSAALevel;
        GraphicsDevice.Presenter = windowRenderer.Presenter;
        // Record shared-surface latch copies BEFORE BeginRendering so this frame's compositing samples the freshly
        // latched private textures (zero latency), and the copies aren't recorded inside the render pass.
        return GraphicsDevice.BeginDraw(beforeRenderPass: _ =>
        {
            // Build the content render cache HERE - inside beforeRenderPass, which runs after BeginDraw's fence wait
            // (the GPU is done with this slot, so buffer updates/frees can't race it) and after this frame's layout
            // Update, but BEFORE PreRender and the render pass. So the cache the renderer draws this frame reflects
            // THIS frame's visual tree. It used to be built in EndDraw - one frame AFTER it was drawn - so a container
            // realized/collapsed this frame lagged the draw by a frame: an item entering the window had no unit yet (a
            // one-frame hole) and one leaving still had a stale unit (a one-frame ghost overlapping with a foreign item).
            windowRenderer.PrepareData();
            windowRenderer.PreRender();
            PreRenderProcessors();   // adorner stage compute (stroke expander) before the render pass
        });
    }

    public override void Draw(AppTime gameTime)
    {
        if (Window.State == WindowState.Minimized)
        {
            pauseEvent.WaitOne();
        }

        windowRenderer?.Render(gameTime);   // content first
        var t0 = Stopwatch.GetTimestamp();
        DrawProcessors(gameTime);           // then processors (adorner overlays) on top, same frame
        RuntimeStats.LastProcessorsMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    public override void EndDraw()
    {
        GraphicsDevice.EndDraw();
        // The content cache is built in BeginDraw (beforeRenderPass) now, not here: it must reflect the frame BEFORE
        // that frame is drawn, not one frame late. EndDraw only finalizes and blits the rendered frame to the swapchain.
        GraphicsDevice.BlitImage(GraphicsDevice.CurrentCommandBuffer,
            GraphicsDevice.CurrentRenderTarget.ResolveTexture,
            windowRenderer.Presenter.GetCurrentImage());
    }

    public override void FrameEnded()
    {
        base.FrameEnded();
        GraphicsDevice.FrameEnded();
        windowRenderer.OnFrameEnded();
        if (!windowRenderer.IsRendererUpToDate)
        {
            windowRenderer.ResizePresenter((uint)(Window.ClientWidth * RenderScale), (uint)(Window.ClientHeight * RenderScale));
        }

        if (_pendingRenderer == null) return;

        windowRenderer = _pendingRenderer;
        _pendingRenderer = null;
    }

    // --- Headless one-shot rendering (designer) -------------------------------------------------------------------
    // Same shape as the runtime loop: BeginDraw builds the content cache (in beforeRenderPass, after the fence wait)
    // and PreRenders it, then Render draws it. The headless one-shot additionally waits for GPU idle afterwards (so the
    // result texture is safe to read back and frees can't race the GPU). It uses the SAME renderer + processors as the
    // loop and does NOT call the loop's EndDraw (no swapchain blit). Window.Update is the caller's responsibility.
    public bool RenderHeadlessFrame(IWindow window, double renderScale, AppTime time)
    {
        // Bind/resize to the requested window + scale, reusing device + presenter (resize, not recreate). Needed only
        // when the window or scale actually changed - a live reconcile keeps the same window + scale, so it's skipped.
        if (!ReferenceEquals(Window, window) || System.Math.Abs(windowRenderer.RenderScale - renderScale) > 1e-9)
        {
            windowRenderer.RenderScale = renderScale;
            RebindWindow(window);
        }

        UpdateProcessors(time);          // adorner stage builds its overlay cache from the current layout

        // shared setup + beforeRenderPass: builds the content cache (PrepareData) and PreRenders it, then the
        // processors - same as the runtime path. Throw (not a bare false) with the device's real reason so the
        // designer reports it instead of an opaque "render failed".
        if (!BeginDraw())
            throw new System.InvalidOperationException(
                $"GraphicsDevice.BeginDraw failed: {GraphicsDevice.LastFrameError ?? "unknown device error"}");
        windowRenderer.Render(time);      // content
        DrawProcessors(time);              // adorner overlays on top

        GraphicsDevice.EndDraw();          // not this.EndDraw(): no next-frame rebuild, no swapchain blit
        GraphicsDevice.Submit();
        GraphicsDevice.DeviceWaitIdle();   // frame finished -> the resolve texture is safe to read back

        // Complete the frame the way the runtime loop does. The headless designer does NOT run the app loop that
        // normally raises FrameFinished, and WITHOUT it the per-frame dynamic buffer pools are never Reset() - every
        // frame's draws keep allocating fresh 2 MB GPU pages, so a streamed/zoomed preview leaks VRAM until the device
        // runs out (the "dies after a few zooms" regression). Raise it here, then advance the frame slot (cycling the
        // deferred-dispose queues), exactly like the on-screen path. Safe now: the GPU is idle.
        GraphicsDevice.MainDevice.OnFrameFinished();
        GraphicsDevice.FrameEnded();
        return true;
    }

    /// <summary>Drops the renderer's cached units; the designer calls this on a full (non-reconcile) rebuild.</summary>
    public void ResetFrameCache() => windowRenderer.ResetCache();

    /// <summary>Designer: switch the previewed window WITHOUT recreating the device/renderer/presenter - the renderer
    /// re-points and resizes its presenter to the new window (see <see cref="IWindowRenderer.Retarget"/>), and the
    /// adorner processor follows because it reads this service's Window. The runtime never calls this (it binds once).</summary>
    public void RebindWindow(IWindow window)
    {
        Window = window;
        windowRenderer.Retarget(window);
    }

    /// <summary>Writes the last rendered frame as raw B8G8R8A8 (no encode) - the designer's per-frame transport.</summary>
    public void SaveFrameRaw(string path) => windowRenderer.Presenter.RenderTarget.ResolveTexture.SaveRaw(path);

    /// <summary>Saves the last rendered frame to an image file.</summary>
    public void SaveFrame(string path, ImageFileType fileType) => windowRenderer.Presenter.RenderTarget.ResolveTexture.Save(path, fileType);
}