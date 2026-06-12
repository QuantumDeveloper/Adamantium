using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Imaging;
using Adamantium.UI.Core.Graphics;

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

    protected override PresenterType PresenterKind => PresenterType.Headless;

    /// <summary>Saves the last rendered frame (the headless swapchain image) to disk.</summary>
    public void SaveFrame(string path, ImageFileType fileType) =>
        Presenter.GetCurrentImage().Save(path, fileType);
}
