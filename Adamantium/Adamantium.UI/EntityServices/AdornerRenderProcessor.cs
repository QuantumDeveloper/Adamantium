using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Adorners;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;

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
            // An adorner on something hosted by a POPUP is drawn by the popup stage instead, right behind the popup it
            // belongs to. Drawn here it would sit under every overlay (a dialog's focus ring vanished entirely); drawn
            // last, above them all, it floated over the overlays stacked on top of its own (a ring from the window
            // hanging over four overlay windows). A decoration belongs in the layer of the thing it decorates.
            if (IsHostedOnOverlay(adorner, window)) 
                continue;
            
            LayoutAdorner(adorner);
            Flatten(adorner, _flat);
        }

        _cache.BuildFromComponents(_flat, projection);
        _cache.ProcessCommands(projection, AssociatedService.RenderScale);
        _cache.PreRender();
    }

    /// <summary>Is what this adorner decorates hosted on the window's popup overlay (rather than in its content)?</summary>
    internal static bool IsHostedOnOverlay(IUIComponent adorner, IWindow window)
    {
        if (adorner is not Adorner { AdornedElement: { } target }) return false;

        foreach (var root in window.PopupRoots)
        {
            for (IUIComponent node = target; node != null; node = node.VisualParent)
                if (ReferenceEquals(node, root)) return true;
        }

        return false;
    }

    /// <summary>Themes + lays out a frame adorner and flattens its subtree into <paramref name="list"/>. Shared with the
    /// popup stage, which draws the adorners of what IT hosts so they stack with it.</summary>
    internal static void Collect(IUIComponent adorner, List<IUIComponent> list)
    {
        LayoutAdorner(adorner);
        Flatten(adorner, list);
    }

    private readonly List<IUIComponent> _flat = new();

    // Apply the theme once so the adorner's ControlTemplate resolves, then lay it out every frame (the element can
    // move/resize). A frame (selection / hover) fills its element; a BADGE (a key tip) measures to its own content and
    // asks where to sit - it hangs off an edge. If the theme has no template, Template stays null and the adorner falls
    // back to its own OnRender, so the designer frames never disappear.
    private static void LayoutAdorner(IUIComponent adorner)
    {
        if (adorner is not Adorner a || a.AdornedElement == null)
            return;

        if (!a.ThemeApplied)
        {
            a.ThemeApplied = true;
            if (UIApplication.Current?.ThemeManager is { CurrentTheme: { } theme } manager)
                manager.ApplyTheme(theme, a);
        }

        if (a.Template == null) return;

        if (a.FillsAdornedBounds)
        {
            var bounds = a.AdornedBounds;
            ((IMeasurableComponent)a).Measure(bounds.Size, true);
            ((IMeasurableComponent)a).Arrange(bounds, true);
            return;
        }

        var measurable = (IMeasurableComponent)a;
        measurable.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity), true);
        measurable.Arrange(a.PlaceIn(measurable.DesiredSize), true);
    }

    private static void Flatten(IUIComponent component, List<IUIComponent> list)
    {
        if (component == null || component.Visibility != Visibility.Visible)
            return;

        list.Add(component);
        foreach (var child in component.VisualChildren)
            Flatten(child, list);
    }

    /// <summary>Draws the overlay WITH the device and a full-window scissor - the same way the popup stage does.
    /// <para>The parameterless <c>Render()</c> is the GPU-FREE overload (device null): it starts none of the batch
    /// collectors, so everything that draws through a batch - which is every themed Border, i.e. the whole focus ring -
    /// was silently dropped. Measured: the ring was built, themed, sized and positioned correctly every frame and put
    /// not one pixel on the screen. The designer and the offscreen tests always passed a device, which is why the stage
    /// looked healthy everywhere except in a running application.</para></summary>
    public override void Draw(AppTime gameTime)
    {
        if (_cache == null)
            return;

        var window = AssociatedService.Window;
        var scale = AssociatedService.RenderScale;
        var scissor = new Rect2D
        {
            Offset = new Offset2D(),
            Extent = new Extent2D { Width = (uint)(window.ClientWidth * scale), Height = (uint)(window.ClientHeight * scale) }
        };
        AssociatedService.GraphicsDevice.SetScissors(scissor);
        _cache.Render(AssociatedService.GraphicsDevice, scissor);
    }
}
