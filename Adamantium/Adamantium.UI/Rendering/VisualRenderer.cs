using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Markup;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Core.Rendering;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

/// <summary>
/// Renders a visual into a texture off-screen - the engine's analog of UWP's <c>RenderTargetBitmap</c>. Feed it a live
/// (detached / AUML-loaded) visual, or a LIVE on-screen element, and it drives the PRODUCTION <see cref="RenderCache"/>
/// into a window-less render target, reads the result back on the CPU and hands back a device-independent bitmap an
/// <c>Image</c>/<c>DrawImage</c> can draw. The shared foundation for the drag-drop ghost, VisualBrush/DrawingBrush bakes,
/// and previews/thumbnails (docs/DRAG_DROP_PLAN.md). One-shot: the GPU resources are freed before returning.
///
/// Each call builds its OWN <see cref="RenderCache"/> (parallel to any window's). That is SAFE: the parallel cache only
/// READS the global <c>RenderDirty</c> (its <c>Clear()</c> is the window loop's, never a build's), and its unit/snapshot
/// state are instance fields - so a bake never disturbs a live window's render.
/// </summary>
public sealed class VisualRenderer : IVisualRenderer
{
    private readonly IGraphicsDeviceService _deviceService;
    private readonly IResourceFactory _resourceFactory;
    private readonly object _deviceLock = new();
    private IGraphicsDevice _device;
    private IRenderUnitFactory _renderUnitFactory;

    // DI singleton. Its render device has its own queue / command pool / fences, but SHARES the one VkDevice with the window
    // loop's device - so an off-screen draw must not run device-wide barriers (DeviceWaitIdle) or submit concurrently with
    // the render thread; the live-snapshot path serialises the GPU half onto the render thread for exactly that reason (see
    // the queue note below).
    public VisualRenderer(IGraphicsDeviceService deviceService, IResourceFactory resourceFactory)
    {
        _deviceService = deviceService;
        _resourceFactory = resourceFactory;
    }

    // The dedicated render device is created on FIRST actual use, not in the ctor: the app loop resolves this singleton and
    // calls the queue drains every frame, and an app that never renders a visual off-screen should not pay for a second
    // render device. Double-checked so the loop-thread record and render-thread draw can't both create one.
    private void EnsureDevice()
    {
        if (_device != null) return;
        lock (_deviceLock)
        {
            if (_device != null) 
                return;
            
            var device = _deviceService.CreateRenderDevice();
            _renderUnitFactory = new RenderUnitFactory(device, _resourceFactory);
            _device = device;   // publish LAST: _device != null gates the fast path, so the factory must be ready first
        }
    }

    /// <summary>
    /// Renders <paramref name="visual"/> at <paramref name="size"/> (logical) x <paramref name="scale"/> (device px) into a
    /// fresh off-screen target. The visual is HOSTED (reparented) under an off-screen root, so pass a fresh / detached tree.
    /// </summary>
    public ImageSource Render(IUIComponent visual, Size size, double scale = 1.0, Color? clearColor = null)
    {
        var built = BuildDetachedCache(visual, size, scale);
        return DrawToBitmap(built.Cache, built.Width, built.Height, clearColor ?? Colors.Transparent);
    }

    // The DEVICE-FREE half of a detached render: host the visual, lay it out and record it into a parallel cache. Split
    // out so the queued path (RequestRender) can run it on the loop thread and leave only the GPU half to the render one.
    private (RenderCache Cache, uint Width, uint Height) BuildDetachedCache(IUIComponent visual, Size size, double scale)
    {
        EnsureDevice();
        var root = new VisualRoot(visual, size.Width, size.Height);
        var layoutSize = new Size(size.Width, size.Height);
        ((IMeasurableComponent)root).Measure(layoutSize);
        ((IMeasurableComponent)root).Arrange(new Rect(layoutSize));

        var cache = new RenderCache(new DrawingContext(), _renderUnitFactory);
        cache.BuildFromVisualTree(root);
        cache.ProcessCommands(root.GetProjectionMatrix(), scale);

        return (cache, (uint)Math.Max(1, size.Width * scale), (uint)Math.Max(1, size.Height * scale));
    }

    /// <summary>Parses AUML markup into a fresh visual and renders it (UWP <c>XamlReader.Load</c> + RenderTargetBitmap).</summary>
    public ImageSource Render(string aumlText, Size size, double scale = 1.0, Color? clearColor = null)
    {
        return AumlLoader.Load(aumlText).Root is IUIComponent visual ? Render(visual, size, scale, clearColor) : null;
    }

    /// <summary>Renders <paramref name="visual"/> at its OWN desired size (measured unconstrained) - the WPF/UWP
    /// RenderTargetBitmap default when no size is given, so the whole element is captured, never clipped.</summary>
    public ImageSource Render(IUIComponent visual, double scale = 1.0, Color? clearColor = null)
    {
        ((IMeasurableComponent)visual).Measure(new Size(4096, 4096));
        var d = ((IMeasurableComponent)visual).DesiredSize;
        return Render(visual, new Size(Math.Max(1, d.Width), Math.Max(1, d.Height)), scale, clearColor);
    }

    /// <summary>Parses AUML and renders it at the tree's own desired size (auto-fit, never clipped).</summary>
    public ImageSource Render(string aumlText, double scale = 1.0, Color? clearColor = null)
    {
        return AumlLoader.Load(aumlText).Root is IUIComponent visual ? Render(visual, scale, clearColor) : null;
    }

    // ------------------------------------------------------------------------------------------------------------------
    // LIVE snapshot: a two-stage producer/consumer split across the loop and render threads.
    //
    // A live element is SHARED with the window's render, and the two render devices SHARE ONE VkDevice, so the snapshot has
    // two constraints that pull in opposite directions:
    //   * READING the live tree (RenderReadOnly, world transforms) must happen where nothing mutates it - the LOOP thread,
    //     quiescent after Update (the render thread only ever reads the recorded packets, never the live components).
    //   * The GPU DRAW (submit / readback on the shared VkDevice) must be serialised with the window's render - the RENDER
    //     thread, which alone owns the device. Submitting from the loop thread, or a device-wide DeviceWaitIdle, races the
    //     render thread's submits and LOSES the device (TDR).
    //
    // So it mirrors the window's own record/apply split:
    //   1. RECORD  (loop thread, RecordPendingSnapshots): read the live subtree and build a parallel RenderCache. This is
    //      device-free - exactly like the window's RecordFrame - so it is safe off the render thread.
    //   2. DRAW    (render thread, DrawPendingSnapshots): the GPU half - draw the built cache into an off-screen RT, read it
    //      back, deliver the bitmap on the UI thread.
    //
    // Both queues drain every frame from the app loop (RecordPendingSnapshots on the loop thread, DrawPendingSnapshots on the
    // render thread), so a no-op drain on an empty queue is the idle cost - a single TryDequeue miss, no device touched.
    // ------------------------------------------------------------------------------------------------------------------
    private readonly ConcurrentQueue<(IUIComponent visual, Action<ImageSource> onReady)> _recordQueue = new();
    private readonly ConcurrentQueue<(IUIComponent visual, Size size, double scale, Color clear, Action<ImageSource> onReady)> _detachedQueue = new();
    private readonly ConcurrentQueue<(RenderCache cache, uint width, uint height, Color clear, Action<ImageSource> onReady)> _drawQueue = new();

    /// <summary>UI-thread API: request a snapshot of a LIVE on-screen element. It is recorded on the loop thread and drawn on
    /// the render thread (never racing or hanging the window's render); <paramref name="onReady"/> is invoked back on the UI
    /// thread with the resulting bitmap (or null if the element has no visible size). Wakes the loop to pick it up.</summary>
    public void RequestSnapshot(IUIComponent visual, Action<ImageSource> onReady)
    {
        if (visual == null || onReady == null) return;
        _recordQueue.Enqueue((visual, onReady));
        LoopSignal.Request();
    }

    /// <summary>Queue a DETACHED visual, split the same way <see cref="RequestSnapshot"/> is. Rendering it synchronously
    /// instead submits and waits on the SHARED VkDevice from whatever thread asked, racing the window's own frame - and
    /// the picture that came back was intermittently wrong.</summary>
    public void RequestRender(IUIComponent visual, Size size, double scale, Color? clearColor, Action<ImageSource> onReady)
    {
        if (visual == null || onReady == null)
        {
            return;
        }

        _detachedQueue.Enqueue((visual, size, scale, clearColor ?? Colors.Transparent, onReady));
        LoopSignal.Request();
    }

    /// <summary>LOOP-thread stage 1: read each queued live subtree and build its render cache (device-free), then hand the
    /// built cache to the render thread's draw queue. Called by the app loop every frame at a quiescent boundary; a no-op
    /// when nothing is queued.</summary>
    public void RecordPendingSnapshots()
    {
        while (_detachedQueue.TryDequeue(out var detached))
        {
            var built = BuildDetachedCache(detached.visual, detached.size, detached.scale);
            _drawQueue.Enqueue((built.Cache, built.Width, built.Height, detached.clear, detached.onReady));
            LoopSignal.Request();
        }

        while (_recordQueue.TryDequeue(out var request))
        {
            EnsureDevice();
            var built = BuildLiveCache(request.visual);
            if (built == null)
            {
                var cb = request.onReady;
                UIAppContext.Current?.Dispatcher.Post(() => cb(null));
                continue;
            }
            _drawQueue.Enqueue((built.Value.cache, built.Value.width, built.Value.height, Colors.Transparent, request.onReady));
            LoopSignal.Request();   // ensure a frame runs so the render thread drains the draw queue
        }
    }

    /// <summary>RENDER-thread stage 2: draw each recorded cache into an off-screen RT, read it back and deliver the bitmap on
    /// the UI thread. Called by the render thread (which alone owns the shared VkDevice) at a frame boundary.</summary>
    public void DrawPendingSnapshots()
    {
        while (_drawQueue.TryDequeue(out var request))
        {
            var image = DrawToBitmap(request.cache, request.width, request.height, request.clear);
            var callback = request.onReady;
            UIAppContext.Current?.Dispatcher.Post(() => callback(image));
        }
    }

    /// <summary>
    /// Stage-1 record: builds a parallel <see cref="RenderCache"/> for a LIVE, on-screen element WITHOUT reparenting it or
    /// disturbing the window. It flattens the element's subtree into a flat component list and records it through the
    /// adorner-stage path (read-only over the live tree): each component keeps its own live world transform (composed up the
    /// real <c>RenderParent</c> chain), and the projection is offset by the element's world origin so the subtree lands at
    /// the render-target origin. DEVICE-FREE (no GPU work) - safe on the loop thread. Null if the element has no visible size.
    /// </summary>
    private (RenderCache cache, uint width, uint height)? BuildLiveCache(IUIComponent element)
    {
        if (element == null) return null;
        var size = element.Bounds.Size;
        if (size.Width < 1 || size.Height < 1) return null;

        // Record at the owning window's device-pixel density (physical px), so the snapshot is as crisp as on screen.
        double scale = 1.0;
        bool rooted = false;
        for (IUIComponent n = element; n != null; n = n.VisualParent)
            if (n is IWindow { Renderer: { } r }) { scale = r.RenderScale; rooted = true; break; }
        if (!rooted && UIApplication.Current?.Windows is { } windows)   // DETACHED (e.g. a drag ghost's DragTemplate): use the
        {                                                              // scale of the window UNDER THE CURSOR, so it's crisp on
            var screen = Adamantium.UI.Core.Input.Mouse.ScreenCoordinates;   // the monitor the drag is actually on.
            foreach (var w in windows)
                if (w is IWindow { Renderer: { } r } win)
                {
                    var c = win.PointToClient(screen);
                    if (c.X >= 0 && c.Y >= 0 && c.X <= win.ClientWidth && c.Y <= win.ClientHeight) { scale = r.RenderScale; break; }
                    scale = r.RenderScale;   // fallback to whatever window we have if none contains the cursor
                }
        }

        // Flatten the subtree (parent BEFORE children = paint order: parent underneath, children on top).
        var list = new List<IUIComponent>();
        FlattenSubtree(element, list);

        // Project the ELEMENT-LOCAL space (0,0..size) and rebase the subtree so the element sits at the origin
        // (RebaseToOrigin below). Geometry AND the per-unit clip scissors then live in this one 0-based space; offsetting
        // only the projection would leave the scissors in absolute window coords, clamping the whole subtree to the edge.
        var projection = Matrix4x4F.OrthoOffCenter(0, (float)size.Width, 0, (float)size.Height, -100000f, 100000f);

        // readOnly: emit each live component's commands via RenderReadOnly (no IsGeometryValid / RenderDirty touch), so the
        // window's loop is never woken into a concurrent render. The subtree is already valid, so ordinary Render() would
        // no-op - this replays OnRender read-only into the parallel cache. BuildFromComponents + ProcessCommands are both
        // device-free (units hold CPU geometry until DrawToBitmap uploads them), so this whole method is safe off-device.
        var cache = new RenderCache(new DrawingContext(), _renderUnitFactory);
        cache.BuildFromComponents(list, projection, readOnly: true);
        cache.RebaseToOrigin(element);
        cache.ProcessCommands(projection, scale);

        var width = (uint)Math.Max(1, size.Width * scale);
        var height = (uint)Math.Max(1, size.Height * scale);
        return (cache, width, height);
    }

    private static void FlattenSubtree(IUIComponent component, List<IUIComponent> list)
    {
        if (component.Visibility != Visibility.Visible) return;
        list.Add(component);
        foreach (var child in component.VisualChildren) FlattenSubtree(child, list);
    }

    // Draws an already-built cache into a fresh off-screen RT and reads it back to a CPU bitmap (device-INDEPENDENT: the
    // dedicated render device's GPU texture can't be sampled by a window's device). One-shot - frees the GPU resources here.
    private ImageSource DrawToBitmap(RenderCache cache, uint width, uint height, Color clear)
    {
        EnsureDevice();
        var presenter = GraphicsPresenter.Create(_device,
            new PresentationParameters(PresenterType.RenderTarget, width, height, IntPtr.Zero, MSAALevel.None),
            "VisualRenderer_presenter");

        _device.ClearColor = clear;
        _device.SetRenderTargets(presenter.RenderTarget);
        _device.SetDepthBuffer(presenter.DepthBuffer);
        _device.MSAALevel = presenter.MSAALevel;
        _device.Presenter = presenter;

        var viewport = new Viewport { Width = width, Height = height, MinDepth = 0, MaxDepth = 1 };
        var scissor = new Rect2D { Offset = new Offset2D(), Extent = new Extent2D { Width = width, Height = height } };

        if (!_device.BeginDraw(beforeRenderPass: _ => cache.PreRender()))
        {
            cache.DisposeUnits();
            presenter.Dispose();
            return null;
        }

        _device.SetViewports(viewport);
        _device.SetScissors(scissor);
        cache.Render(_device, scissor);
        _device.EndDraw();
        _device.Submit();
        presenter.Present();      // no-op for the off-screen render-target presenter
        _device.FrameEnded();
        _device.DeviceWaitIdle(); // the frame is finished -> the texture is safe to read back

        var format = presenter.SurfaceFormat;
        using var hostImage = presenter.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)hostImage.TotalSizeInBytes];
        System.Runtime.InteropServices.Marshal.Copy(hostImage.DataPointer, pixels, 0, pixels.Length);

        cache.DisposeUnits();
        presenter.Dispose();

        return new BitmapSource(width, height, 1, 1, format, pixels);
    }
}
