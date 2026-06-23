using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering;

namespace Adamantium.UI.EntityServices;

/// <summary>
/// A second render stage, as a real processor in the render service's collection: it draws the window's AdornerLayer
/// (tooling overlays - selection frames etc.) ON TOP of the content in the SAME frame. Built each frame from the
/// window's <see cref="IWindow.Adorners"/> (a flat list, not the content tree); it runs after the content renderer -
/// PreRender dispatches its stroke compute in the beforeRenderPass hook, Draw rasterizes in the render pass. The same
/// code drives runtime and the headless designer because both go through the render service.
/// </summary>
public class AdornerRenderProcessor : EntityProcessor<WindowRenderService>
{
    private RenderCache _cache;

    // Runs after the content renderer (which isn't itself a processor); high so any future overlays order around it.
    public override int Order => 1000;

    protected override void OnAttached()
    {
        var device = AssociatedService.GraphicsDevice;
        var resourceFactory = AssociatedService.EntityWorld.DependencyResolver.Resolve<IResourceFactory>();
        _cache = new RenderCache(new DrawingContext(), new RenderUnitFactory(device, resourceFactory));
    }

    public override void Update(AppTime gameTime)
    {
        if (_cache == null) return;

        var window = AssociatedService.Window;
        var projection = window.GetProjectionMatrix();
        // Built after the window's own layout update this frame: the overlay units come from the window's adorners and
        // are bound to the adorned elements' live WorldTransform.
        _cache.BuildFromComponents(window.Adorners, projection);
        _cache.ProcessCommands(projection);
    }

    public override void PreRender() => _cache?.PreRender();

    public override void Draw(AppTime gameTime) => _cache?.Render();
}
