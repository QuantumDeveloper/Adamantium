using System;
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
/// (detached / AUML-loaded) visual and a size; it lays the visual out, drives the PRODUCTION <see cref="RenderCache"/>
/// into a window-less render target, and hands back a <see cref="RenderedVisualImage"/> an <c>Image</c>/<c>DrawImage</c>
/// can draw. This is the shared foundation for the drag-drop ghost, VisualBrush/DrawingBrush bakes, and previews/thumbnails
/// (see docs/DRAG_DROP_PLAN.md Phase 0). One-shot: the returned image owns its GPU resources and frees them on dispose.
///
/// Each call builds its OWN <see cref="RenderCache"/> (parallel to any window's), so recording a visual never touches
/// another cache's units. Hosting reparents the visual under an off-screen <see cref="VisualRoot"/>, so pass a fresh /
/// detached tree here; a live on-screen element is baked without reparenting (Phase 1).
/// </summary>
public sealed class VisualRenderer : IVisualRenderer
{
    private readonly IGraphicsDevice _device;
    private readonly IRenderUnitFactory _renderUnitFactory;

    // DI-constructed (singleton). A DEDICATED render device (like the headless designer's) so an off-screen bake never
    // contends with the main window loop's device; created lazily-enough because the singleton is resolved on first use,
    // well after the main device is up.
    public VisualRenderer(IGraphicsDeviceService deviceService, IResourceFactory resourceFactory)
    {
        _device = deviceService.CreateRenderDevice();
        _renderUnitFactory = new RenderUnitFactory(_device, resourceFactory);
    }

    /// <summary>
    /// Renders <paramref name="visual"/> at <paramref name="size"/> (logical) x <paramref name="scale"/> (device px) into a
    /// fresh off-screen target. Returns the drawable image, or null if the frame could not begin.
    /// </summary>
    public ImageSource Render(IUIComponent visual, Size size, double scale = 1.0, Color? clearColor = null)
    {
        var width = (uint)Math.Max(1, size.Width * scale);
        var height = (uint)Math.Max(1, size.Height * scale);

        // Lay the visual out at its target size under an off-screen root (the root supplies ClientWidth/Height -> projection).
        var root = new VisualRoot(visual, size.Width, size.Height);
        var layoutSize = new Size(size.Width, size.Height);
        ((IMeasurableComponent)root).Measure(layoutSize);
        ((IMeasurableComponent)root).Arrange(new Rect(layoutSize));

        var presenter = GraphicsPresenter.Create(_device,
            new PresentationParameters(PresenterType.RenderTarget, width, height, IntPtr.Zero, MSAALevel.None),
            "VisualRenderer_presenter");

        var cache = new RenderCache(new DrawingContext(), _renderUnitFactory);

        var projection = root.GetProjectionMatrix();
        cache.BuildFromVisualTree(root);
        cache.ProcessCommands(projection, scale);

        _device.ClearColor = clearColor ?? Colors.Transparent;
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
        _device.DeviceWaitIdle(); // the frame is finished -> the texture is safe to sample / read back

        // Read the baked pixels back to the CPU and return a device-INDEPENDENT bitmap. The off-screen render device is a
        // DEDICATED one, so its GPU texture cannot be sampled by the (different) window device that will DRAW the result -
        // the CPU round-trip makes the image displayable anywhere, and keeps this truly one-shot (the GPU resources are
        // freed here, not handed out to live on).
        var format = presenter.SurfaceFormat;
        using var hostImage = presenter.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)hostImage.TotalSizeInBytes];
        System.Runtime.InteropServices.Marshal.Copy(hostImage.DataPointer, pixels, 0, pixels.Length);

        cache.DisposeUnits();
        presenter.Dispose();

        return new BitmapSource(width, height, 1, 1, format, pixels);
    }

    /// <summary>Parses AUML markup into a fresh visual and renders it (UWP <c>XamlReader.Load</c> + RenderTargetBitmap).</summary>
    public ImageSource Render(string aumlText, Size size, double scale = 1.0, Color? clearColor = null)
    {
        // A brand-new, detached tree - the clean, no-reparent, no-shared-marks case.
        return AumlLoader.Load(aumlText).Root is IUIComponent visual
            ? Render(visual, size, scale, clearColor)
            : null;
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
}
