using System;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

/// <summary>
/// Renders a visual tree off-screen (no OS window) into a render target whose pixels can be read back - e.g.
/// the AUML live designer feeding an image to the editor extension. The presenter is chosen automatically: a
/// real window-less swapchain via VK_EXT_headless_surface where the loader/driver provides it (Linux/Mesa,
/// software ICDs), otherwise a plain offscreen RenderTarget (Windows and everywhere else). Both produce the
/// same readable image; only the Linux path goes through the headless extension.
/// </summary>
public sealed class OffscreenRenderer : IDisposable
{
    private readonly IGraphicsDevice _device;
    private readonly RenderCache _renderCache;
    private GraphicsPresenter _presenter;
    private PresenterType _presenterKind;
    private Viewport _viewport;
    private Rect2D _scissor;

    public OffscreenRenderer(IGraphicsDevice device, IRenderUnitFactory renderUnitFactory, uint width, uint height,
        MSAALevel msaa = MSAALevel.None)
    {
        _device = device;
        _renderCache = new RenderCache(new DrawingContext(), renderUnitFactory);
        CreatePresenter(width, height, msaa);
    }

    /// <summary>Colour the target is cleared to before drawing. Default transparent.</summary>
    public Color ClearColor { get; set; } = Colors.Transparent;

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
        _renderCache.BuildFromVisualTree(root);
        _renderCache.ProcessCommands(root.GetProjectionMatrix());

        _device.ClearColor = ClearColor;
        _device.SetRenderTargets(_presenter.RenderTarget);
        _device.SetDepthBuffer(_presenter.DepthBuffer);
        _device.MSAALevel = _presenter.MSAALevel;
        _device.Presenter = _presenter;

        if (!_device.BeginDraw()) return false;

        _device.SetViewports(_viewport);
        _device.SetScissors(_scissor);
        _renderCache.Render();

        _device.EndDraw();
        _device.Submit();
        _presenter.Present();      // no-op for both off-screen presenters
        _device.FrameEnded();

        _device.DeviceWaitIdle();  // ensure the frame is finished before any read-back
        return true;
    }

    /// <summary>Saves the last rendered frame to disk.</summary>
    public void Save(string path, ImageFileType fileType) => _presenter.RenderTarget.Save(path, fileType);

    public void Resize(uint width, uint height)
    {
        _presenter.Resize(new PresentationParameters(_presenterKind, width, height, IntPtr.Zero, _presenter.MSAALevel));
        UpdateViewportScissor(width, height);
    }

    /// <summary>
    /// Frees the cached render units (their GPU buffers) without tearing down the presenter. Call between renders
    /// when each frame is a fresh visual tree (the AUML designer) so units don't accumulate. The caller must
    /// ensure the GPU is idle - <see cref="RenderFrame"/> leaves it idle on return.
    /// </summary>
    public void ResetCache() => _renderCache.DisposeUnits();

    public void Dispose()
    {
        _renderCache.DisposeUnits();
        _presenter?.Dispose();
    }
}
