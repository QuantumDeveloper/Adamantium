using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.UI.Controls.Adorners;
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

    public override void Update(AppTime gameTime) { }   // building moved to PreRender (after the fence wait) - see below

    // Build the overlay HERE, inside the beforeRenderPass hook, AFTER BeginDraw's fence wait - the GPU is done with this
    // frame slot, so (re)allocating this stage's GPU buffers can't race an in-flight submit. Building it in Update (BEFORE
    // the fence) did GPU work in the update phase, which is a use-after-free hazard once the render thread runs concurrently
    // with update. This mirrors PopupRenderProcessor, which moved its build here for the same reason. The overlay units come
    // from the window's adorners, bound to the adorned elements' live WorldTransform (still valid - after this frame's layout).
    public override void PreRender()
    {
        if (_cache == null) return;

        var window = AssociatedService.Window;
        var projection = window.GetProjectionMatrix();

        // Flatten each adorner's SUBTREE, not just the adorner: a raw Adorner draws itself (no children) but a templatable
        // adorner hosts a styled control tree, and BuildFromComponents renders a flat list - so its content must be in it.
        _flat.Clear();
        foreach (var adorner in window.Adorners)
        {
            LayoutFrameAdorner(adorner);
            Flatten(adorner, _flat);
        }
        _cache.BuildFromComponents(_flat, projection);
        _cache.ProcessCommands(projection, AssociatedService.RenderScale);
        _cache.PreRender();
    }

    private readonly List<IUIComponent> _flat = new();

    // A frame adorner (selection / hover) wraps its whole element: apply the theme once so its ControlTemplate resolves,
    // then size it to the adorned element's bounds every frame (the element can move/resize). If the theme has no template
    // for it, Template stays null and the adorner falls back to its own OnRender - so the designer frames never disappear.
    private static void LayoutFrameAdorner(IUIComponent adorner)
    {
        if (adorner is not Adorner a || !a.FillsAdornedBounds || a.AdornedElement == null) return;

        if (!a.ThemeApplied)
        {
            a.ThemeApplied = true;
            if (UIApplication.Current?.ThemeManager is { CurrentTheme: { } theme } manager) manager.ApplyTheme(theme, a);
        }

        if (a.Template != null)
        {
            var bounds = a.AdornedBounds;
            ((IMeasurableComponent)a).Measure(bounds.Size, true);
            ((IMeasurableComponent)a).Arrange(bounds, true);
        }
    }

    private static void Flatten(IUIComponent component, List<IUIComponent> list)
    {
        if (component == null || component.Visibility != Visibility.Visible) return;
        list.Add(component);
        foreach (var child in component.VisualChildren) Flatten(child, list);
    }

    public override void Draw(AppTime gameTime) => _cache?.Render();
}
