using Adamantium.Graphics.Core.Presentation;

namespace Adamantium.Graphics.Core;

/// <summary>A swapchain presenter on a window-less VK_EXT_headless_surface, for offscreen output such as the AUML
/// designer; chosen by <see cref="PresenterType.Headless"/>.</summary>
public class HeadlessPresenter : SwapChainGraphicsPresenter
{
    public HeadlessPresenter(IGraphicsDevice graphicsDevice, PresentationParameters description, string name = "")
        : base(graphicsDevice, description, name)
    {
    }
}