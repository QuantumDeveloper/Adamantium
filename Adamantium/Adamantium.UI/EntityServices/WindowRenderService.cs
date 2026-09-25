using System.Diagnostics;
using System.Threading;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.Imaging;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Presentation;
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
    private readonly IGraphicsDevice _injectedDevice;

    public IWindow Window { get; private set; }

    /// <summary>The zoom the content is rendered at; 1 is on-screen 1:1.</summary>
    public double RenderScale => windowRenderer?.RenderScale ?? 1.0;

    /// <summary>A constructor of its own: Activator.CreateInstance matches the argument count exactly and ignores
    /// optional parameters.</summary>
    public WindowRenderService(EntityWorld world, IWindow window) : this(world, window, null)
    {
    }

    /// <summary>Designer path: the whole session shares one injected render device.</summary>
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

        AttachProcessor(new AdornerRenderProcessor());
        AttachProcessor(new PopupRenderProcessor());
    }

    /// <summary>The renderer this service drives; the headless designer renders into a texture instead of a swapchain.</summary>
    protected virtual IWindowRenderer CreateRenderer() =>
        new ForwardWindowRenderer(GraphicsDevice, new RenderUnitFactory(GraphicsDevice, DependencyResolver.Resolve<IResourceFactory>()));

    private void WindowOnRendererChanged(object sender, WindowRendererChangedEventArgs e)
    {
        _pendingRenderer = e.NewRenderer;
    }

    public override void Present()
    {
        var t0 = Stopwatch.GetTimestamp();
        windowRenderer?.Present();
        RuntimeStats.LastPresentMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    public override void UnloadContent()
    {
        GraphicsDevice?.DeviceWaitIdle();
        // A renderer belongs to one device: left on the window, the next service would build on a dead one after a swap.
        Window.RendererChanged -= WindowOnRendererChanged;
        Window.StateChanged -= WindowOnStateChanged;
        if (ReferenceEquals(Window.Renderer, windowRenderer))
        {
            Window.Renderer = null;
        }
        if (ReferenceEquals(Window.DefaultRenderer, windowRenderer))
        {
            Window.DefaultRenderer = null;
        }
        windowRenderer?.Dispose();
        // The designer's injected device belongs to its session.
        if (_injectedDevice == null && GraphicsDevice != null)
            GraphicsDeviceService.MainGraphicsDevice.RemoveDevice(GraphicsDevice);
    }

    public override bool IsUpdateService => true;
    public override EntityServiceType ServiceType => EntityServiceType.Update | EntityServiceType.Render;

    public override void Update(AppTime appTime)
    {
        Window.Update(_themeManager, appTime);
        base.Update(appTime);
    }

    private bool _recordedAtLoopLevel;

    /// <summary>Records this window's packet on the loop thread. False when it could not: the caller must then keep
    /// RenderDirty, or the marks it never recorded are lost.</summary>
    public bool RecordFrame()
    {
        if (windowRenderer is not { IsRendererUpToDate: true })
        {
            _recordedAtLoopLevel = false;
            return false;
        }
        windowRenderer.RecordData();
        _recordedAtLoopLevel = true;
        return true;
    }

    public override bool BeginDraw()
    {
        // Unloaded, but still in the draw list until its removal propagates.
        if (windowRenderer?.Presenter == null) return false;

        // App-global, but windows render one after another.
        Rendering.RenderUnits.AnalyticAa.Enabled = Window?.AnalyticAntialiasing ?? true;
        GraphicsDevice.ClearColor = (Window?.Background as SolidColorBrush)?.Color ?? Colors.Black;
        var trailing = windowRenderer.Presenter?.NeedsRebuild ?? false;

        // Resizing dirties nothing in the tree, so a trailing swapchain must wake the loop itself. Assigned every frame,
        // or it would latch.
        UIApplication.SwapchainTrailing = trailing;

        if (trailing)
        {
            Core.LoopSignal.Request();
        }

        // Rebuilt here, on this thread, so nothing is submitted against a stale swapchain. Suboptimal counts too: that is
        // how the driver reports a stale image once the swapchain says what it does on a mismatch.
        if (windowRenderer.Presenter != null &&
            (!windowRenderer.IsRendererUpToDate
             || windowRenderer.Presenter.LastPresenterState is PresenterState.OutOfDate or PresenterState.Suboptimal
             || trailing))
        {
            windowRenderer.ResizePresenter((uint)(Window.ClientWidth * RenderScale), (uint)(Window.ClientHeight * RenderScale));
        }

        GraphicsDevice.SetRenderTargets(windowRenderer.Presenter.RenderTarget);
        GraphicsDevice.SetDepthBuffer(windowRenderer.Presenter.DepthBuffer);
        GraphicsDevice.MSAALevel = windowRenderer.Presenter.MSAALevel;
        GraphicsDevice.Presenter = windowRenderer.Presenter;
        var beginStart = Stopwatch.GetTimestamp();
        try
        {
            return GraphicsDevice.BeginDraw(beforeRenderPass: _ =>
            {
                // After the fence wait and before the pass, so this frame draws this frame's tree. Only the loop thread
                // records: the render thread would race the loop's own record on the same cache.
                var onRenderThread = Thread.CurrentThread == RenderThreadOptions.RenderThread;
                if (RenderThreadOptions.SingleThreaded || (!_recordedAtLoopLevel && !onRenderThread)) windowRenderer.PrepareData();
                else windowRenderer.ApplyData();
                windowRenderer.PreRender();
                PreRenderProcessors();
            });
        }
        finally
        {
            RuntimeStats.LastBeginDrawMs = Stopwatch.GetElapsedTime(beginStart).TotalMilliseconds;
        }
    }

    public override void Draw(AppTime appTime)
    {
        if (Window.State == WindowState.Minimized)
        {
            pauseEvent.WaitOne();
        }

        windowRenderer?.Render(appTime);
        var t0 = Stopwatch.GetTimestamp();
        DrawProcessors(appTime);
        RuntimeStats.LastProcessorsMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
    }

    public override void EndDraw()
    {
        var t0 = Stopwatch.GetTimestamp();
        GraphicsDevice.EndDraw();
        // A failed acquire leaves nothing to copy into and nothing to present.
        if (GraphicsDevice.HasSwapchainImage)
        {
            GraphicsDevice.BlitImage(GraphicsDevice.CurrentCommandBuffer,
                GraphicsDevice.CurrentRenderTarget.ResolveTexture,
                windowRenderer.Presenter.GetCurrentImage());
        }

        RuntimeStats.LastEndDrawMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;
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

    /// <summary>Designer one-shot frame: the loop's renderer and processors, then a wait for GPU idle so the result can
    /// be read back. Window.Update is the caller's.</summary>
    public bool RenderHeadlessFrame(IWindow window, double renderScale, AppTime time)
    {
        if (!ReferenceEquals(Window, window) || System.Math.Abs(windowRenderer.RenderScale - renderScale) > 1e-9)
        {
            windowRenderer.RenderScale = renderScale;
            RebindWindow(window);
        }

        UpdateProcessors(time);

        if (!BeginDraw())
            throw new System.InvalidOperationException(
                $"GraphicsDevice.BeginDraw failed: {GraphicsDevice.LastFrameError ?? "unknown device error"}");
        windowRenderer.Render(time);
        DrawProcessors(time);

        GraphicsDevice.EndDraw();          // not this.EndDraw(): no swapchain blit
        GraphicsDevice.Submit();
        GraphicsDevice.DeviceWaitIdle();

        // The designer runs no app loop, and without this the per-frame buffer pools are never reset and leak VRAM.
        GraphicsDevice.MainDevice.OnFrameFinished();
        GraphicsDevice.FrameEnded();
        return true;
    }

    /// <summary>Drops the renderer's cached units; the designer calls this on a full (non-reconcile) rebuild.</summary>
    public void ResetFrameCache() => windowRenderer.ResetCache();

    /// <summary>Designer: previews another window on the same device, renderer and presenter.</summary>
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
