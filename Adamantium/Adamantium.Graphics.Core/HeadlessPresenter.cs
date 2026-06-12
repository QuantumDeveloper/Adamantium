using Adamantium.Graphics.Core.Presentation;

namespace Adamantium.Graphics.Core
{
    /// <summary>
    /// A real swapchain presenter backed by a window-less VK_EXT_headless_surface. It behaves exactly like
    /// <see cref="SwapChainGraphicsPresenter"/> (acquire / present / readback, and matches the device's
    /// <c>is SwapChainGraphicsPresenter</c> checks), but renders with no OS window - for offscreen output
    /// such as the AUML live designer. The surface is chosen by <see cref="PresenterType.Headless"/> in the
    /// presentation parameters, which routes <c>GetOrCreateSurface</c> to the headless branch.
    /// </summary>
    public class HeadlessPresenter : SwapChainGraphicsPresenter
    {
        public HeadlessPresenter(IGraphicsDevice graphicsDevice, PresentationParameters description, string name = "")
            : base(graphicsDevice, description, name)
        {
        }
    }
}
