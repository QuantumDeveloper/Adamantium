using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Imaging;
using Adamantium.UI.Core.Graphics;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

/// <summary>
/// A window renderer that draws into a window-less <see cref="PresenterType.Headless"/> swapchain instead of
/// an on-screen one. Reuses the entire <see cref="ForwardWindowRenderer"/> path (render cache, frame loop);
/// only the presenter kind differs. Plug it in via <c>window.Renderer = new HeadlessWindowRenderer(...)</c>
/// (the existing renderer-swap hook) to render a visual tree off-screen - e.g. the AUML live designer feeding
/// pixels to the editor extension.
/// </summary>
public class HeadlessWindowRenderer : ForwardWindowRenderer
{
    public HeadlessWindowRenderer(IGraphicsDevice device, IRenderUnitFactory renderUnitFactory)
        : base(device, renderUnitFactory)
    {
    }

    // A real headless surface where the loader/driver provides VK_EXT_headless_surface (Linux/Mesa, software ICDs),
    // otherwise a plain offscreen RenderTarget (Windows and everywhere else). Both produce the same readable texture.
    protected override PresenterType PresenterKind =>
        HeadlessSurfaceAvailable() ? PresenterType.Headless : PresenterType.RenderTarget;

    private static bool HeadlessSurfaceAvailable() =>
        Instance.EnumerateInstanceExtensionProperties()
            .Any(e => e.ExtensionName == Constants.VK_EXT_HEADLESS_SURFACE_EXTENSION_NAME);

    /// <summary>Saves the last rendered frame to disk. Reads the resolve texture (MSAA-safe; equals the render target
    /// when MSAA is off), matching the on-screen read-back path.</summary>
    public void SaveFrame(string path, ImageFileType fileType) =>
        Presenter.RenderTarget.ResolveTexture.Save(path, fileType);
}
