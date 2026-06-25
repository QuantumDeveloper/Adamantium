using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// Test-only harness that drives the PRODUCTION <see cref="RenderCache"/> to a read-back texture so pixel-level
/// assertions can run without an OS window. It is NOT a second render path - production renders only through the
/// services (<c>WindowRenderService</c> / its headless designer variant). This just wraps the same RenderCache the
/// services use, plus a window-less presenter, so a unit/cache test can render a tree and read the pixels.
/// The presenter is chosen automatically: a real window-less swapchain via VK_EXT_headless_surface where the
/// loader provides it (Linux/Mesa), otherwise a plain offscreen RenderTarget (Windows). Both produce the same image.
/// </summary>
internal sealed class OffscreenTestRenderer : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly RenderCache _renderCache;
    private readonly RenderCache _adornerCache;
    private GraphicsPresenter _presenter;
    private PresenterType _presenterKind;
    private Viewport _viewport;
    private Rect2D _scissor;

    public OffscreenTestRenderer(IGraphicsDevice device, IRenderUnitFactory renderUnitFactory, uint width, uint height,
        MSAALevel msaa = MSAALevel.None)
    {
        _device = device;
        _renderCache = new RenderCache(new DrawingContext(), renderUnitFactory);
        _adornerCache = new RenderCache(new DrawingContext(), renderUnitFactory);
        CreatePresenter(width, height, msaa);
    }

    /// <summary>Colour the target is cleared to before drawing. Default transparent.</summary>
    public Color ClearColor { get; set; } = Colors.Transparent;

    /// <summary>Framework tooling overlays (selection frames etc.) drawn ON TOP of the content in the same frame.
    /// Set from an <c>AdornerLayer.Adorners</c>; null/empty draws nothing.</summary>
    public IReadOnlyList<IUIComponent> Adorners { get; set; }

    public GraphicsPresenter Presenter => _presenter;

    /// <summary>The rendered frame - read this back or <see cref="Save"/> it.</summary>
    public IRenderTarget RenderTarget => _presenter.RenderTarget;

    /// <summary>Which presenter was actually chosen (Headless where the extension exists, else RenderTarget).</summary>
    public PresenterType PresenterKind => _presenterKind;

    private void CreatePresenter(uint width, uint height, MSAALevel msaa)
    {
        _presenterKind = HeadlessSurfaceAvailable() ? PresenterType.Headless : PresenterType.RenderTarget;
        var parameters = new PresentationParameters(_presenterKind, width, height, IntPtr.Zero, msaa);
        _presenter = GraphicsPresenter.Create(_device, parameters, "Offscreen_presenter");
        UpdateViewportScissor(width, height);
    }

    private static bool HeadlessSurfaceAvailable() =>
        Instance.EnumerateInstanceExtensionProperties()
            .Any(e => e.ExtensionName == Constants.VK_EXT_HEADLESS_SURFACE_EXTENSION_NAME);

    private void UpdateViewportScissor(uint width, uint height)
    {
        _viewport = new Viewport { Width = width, Height = height, MinDepth = 0, MaxDepth = 1 };
        _scissor = new Rect2D { Offset = new Offset2D(), Extent = new Extent2D { Width = width, Height = height } };
    }

    /// <summary>
    /// Renders one frame of <paramref name="root"/> into the offscreen target. Returns false if the frame
    /// couldn't begin. On return the GPU is idle, so the target is safe to read back / save.
    /// </summary>
    public bool RenderFrame(IRootVisualComponent root)
    {
        var projection = root.GetProjectionMatrix();
        _renderCache.BuildFromVisualTree(root);
        _renderCache.ProcessCommands(projection, 1.0);   // offscreen test renderer is 1:1 (no designer zoom)

        // Tooling overlays (selection frames) are a SECOND stage built from a flat list, rendered after the content
        // in the same frame so they sit on top. They share the projection and target; only the draw order differs.
        var adorners = Adorners;
        var hasAdorners = adorners is { Count: > 0 };
        if (hasAdorners)
        {
            _adornerCache.BuildFromComponents(adorners, projection);
            _adornerCache.ProcessCommands(projection, 1.0);
        }

        _device.ClearColor = ClearColor;
        _device.SetRenderTargets(_presenter.RenderTarget);
        _device.SetDepthBuffer(_presenter.DepthBuffer);
        _device.MSAALevel = _presenter.MSAALevel;
        _device.Presenter = _presenter;

        // Out-of-render-pass work recorded BEFORE the render pass begins (the same hook the WindowRenderService uses):
        // the GPU stroke expander dispatches its compute here to fill the vertex buffer. Both content and adorner
        // stages dispatch their compute here.
        if (!_device.BeginDraw(beforeRenderPass: _ =>
            {
                _renderCache.PreRender();
                if (hasAdorners) _adornerCache.PreRender();
            })) return false;

        _device.SetViewports(_viewport);
        _device.SetScissors(_scissor);
        _renderCache.Render(_device, _scissor);                    // content
        if (hasAdorners) 
            _adornerCache.Render(_device, _scissor);  // tooling overlays, on top

        _device.EndDraw();
        _device.Submit();
        _presenter.Present();      // no-op for both off-screen presenters
        _device.FrameEnded();

        _device.DeviceWaitIdle();  // ensure the frame is finished before any read-back
        return true;
    }

    /// <summary>Saves the last rendered frame to disk. Reads the resolve texture, not the render target itself:
    /// with MSAA the render target is a multisampled image (not linearly readable); its resolved single-sample copy
    /// is what the render pass produces. With MSAA off ResolveTexture == the render target, so this is correct too.</summary>
    public void Save(string path, ImageFileType fileType) => _presenter.RenderTarget.ResolveTexture.Save(path, fileType);

    /// <summary>Writes the last rendered frame's RAW pixels (native B8G8R8A8, no PNG encode) to <paramref name="path"/>.</summary>
    public void SaveRaw(string path) => _presenter.RenderTarget.ResolveTexture.SaveRaw(path);

    public void Dispose()
    {
        _renderCache.DisposeUnits();
        _adornerCache.DisposeUnits();
        _presenter?.Dispose();
    }
}
