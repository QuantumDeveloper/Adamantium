using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering;

namespace Adamantium.UI.EntityServices;

/// <summary>
/// The popup stage: draws the open popups' children (tooltips, in-window popups) ON TOP of the content AND the adorner
/// overlay, in the SAME frame, within the window. Each frame it asks the window to re-evaluate popup placements
/// (<see cref="IWindow.LayoutPopups"/>) so a popup follows a moving target, then builds + renders their subtrees. Runs
/// like the adorner stage (PreRender dispatches stroke compute in beforeRenderPass; Draw rasterizes in the render pass).
/// </summary>
public class PopupRenderProcessor : EntityProcessor<WindowRenderService>
{
    private RenderCache _cache;

    // After the adorner stage (1000) so popups/tooltips sit on top of everything, including selection frames.
    public override int Order => 2000;

    protected override void OnAttached()
    {
        var device = AssociatedService.GraphicsDevice;
        var resourceFactory = AssociatedService.EntityWorld.DependencyResolver.Resolve<IResourceFactory>();
        _cache = new RenderCache(new DrawingContext(), new RenderUnitFactory(device, resourceFactory));
    }

    public override void Update(AppTime gameTime) { }   // building moved to PreRender (after the fence wait) - see below

    // Build + prepare the overlay HERE, inside the beforeRenderPass hook, AFTER BeginDraw's fence wait - the GPU is done
    // with this frame slot, so (re)allocating this stage's GPU buffers + text render targets can't race an in-flight
    // submit. Building it in Update (BEFORE the fence) raced the GPU and lost the device the moment a popup's text RT was
    // reallocated mid-flight (the value badge re-rasterizing as it changed). This mirrors the content renderer's
    // PrepareData, which also builds in beforeRenderPass - so the popup stage is now synchronized with the main stage.
    public override void PreRender()
    {
        if (_cache == null) return;

        var window = AssociatedService.Window;
        var projection = window.GetProjectionMatrix();
        // Re-evaluate popup positions from their targets' CURRENT world positions (follow a moving target), then build
        // the overlay from the laid-out popup subtrees and prepare it (stroke compute / text rasterization).
        window.LayoutPopups();
        _cache.BuildFromComponents(Flatten(window.PopupRoots), projection);
        _cache.ProcessCommands(projection, AssociatedService.RenderScale);
        _cache.PreRender();
    }

    public override void Draw(AppTime gameTime) => _cache?.Render();

    // Pre-order flatten of each popup subtree: BuildFromComponents renders a flat list in order, so a parent must come
    // before its children for correct layering. (The children are already measured/arranged by LayoutPopups.)
    private static IReadOnlyList<IUIComponent> Flatten(IReadOnlyList<IUIComponent> roots)
    {
        var list = new List<IUIComponent>();
        if (roots != null)
            foreach (var root in roots)
                FlattenInto(root, list);
        return list;
    }

    private static void FlattenInto(IUIComponent component, List<IUIComponent> list)
    {
        if (component.Visibility != Visibility.Visible) return;
        list.Add(component);
        foreach (var child in component.VisualChildren)
            FlattenInto(child, list);
    }
}
