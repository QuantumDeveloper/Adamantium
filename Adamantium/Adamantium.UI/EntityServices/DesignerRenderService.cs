using Adamantium.ECS;
using Adamantium.Graphics.Core;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering;

namespace Adamantium.UI.EntityServices;

/// <summary>
/// The headless variant of <see cref="WindowRenderService"/> for the designer: the SAME render path (content renderer
/// + processor collection, incl. the adorner stage) but rendering into a texture instead of a swapchain. The only
/// difference is the presenter kind (see <see cref="HeadlessWindowRenderer"/>). Drive a frame with
/// <see cref="WindowRenderService.RenderHeadlessFrame"/> and read it via SaveFrameRaw / SaveFrame.
/// </summary>
public class DesignerRenderService : WindowRenderService
{
    public DesignerRenderService(EntityWorld world, IWindow window, IGraphicsDevice renderDevice)
        : base(world, window, renderDevice)
    {
    }

    protected override IWindowRenderer CreateRenderer() =>
        new HeadlessWindowRenderer(GraphicsDevice, new RenderUnitFactory(GraphicsDevice, DependencyResolver.Resolve<IResourceFactory>()));
}
