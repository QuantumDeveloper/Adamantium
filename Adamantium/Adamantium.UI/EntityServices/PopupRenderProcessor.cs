using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.ECS;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;

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
        // Re-evaluate popup positions from their targets' CURRENT world positions (follow a moving target) - cheap, and
        // it is what flags dirty content (a re-measure clears IsGeometryValid) that the rebuild gate below reads.
        window.LayoutPopups();

        var flat = Flatten(window.PopupRoots, window);
        // Rebuild (component walk + rasterization) only when the open set / geometry / a popup's position changed.
        if (OverlayChanged(flat))
        {
            _cache.BuildFromComponents(flat, projection);
            _cache.ProcessCommands(projection, AssociatedService.RenderScale);
        }

        // But PreRender (GPU-stroke compute) runs EVERY frame, like the content stage: a re-record promotes a stroke's
        // vertex buffer to a per-frame ring, and each slot must be refilled before Draw reads it, else it smears a frame.
        _cache.PreRender();
    }

    private HashSet<Guid> _prevIds = new();
    private readonly Dictionary<Guid, Vector3F> _prevPos = new();

    // True if the overlay's rendered result could differ from last frame: the set of drawn components changed, any of
    // them has invalid geometry (content changed / re-measured), or any moved (target followed, animation). Walks the
    // flattened popups once reading flags + transforms - far cheaper than rebuilding + re-rasterizing them every frame.
    private bool OverlayChanged(IReadOnlyList<IUIComponent> flat)
    {
        var changed = false;
        var ids = new HashSet<Guid>();
        foreach (var c in flat)
        {
            ids.Add(c.RenderId);
            if (!c.IsGeometryValid) changed = true;
            var pos = c.WorldTransform.TranslationVector;
            if (!_prevPos.TryGetValue(c.RenderId, out var prev) || !prev.Equals(pos)) { changed = true; _prevPos[c.RenderId] = pos; }
        }
        if (!changed && !ids.SetEquals(_prevIds)) changed = true;
        if (changed)
        {
            _prevIds = ids;
            if (_prevPos.Count > ids.Count)   // drop transforms of components no longer shown
                foreach (var gone in new List<Guid>(_prevPos.Keys)) if (!ids.Contains(gone)) _prevPos.Remove(gone);
        }
        return changed;
    }

    // Render the overlay with the SAME device + full-window scissor the content pass uses, so the popup layer runs the
    // FULL render path: the SDF item-background BATCH (a rounded-rect fill+stroke - e.g. a menu / SlidePanel card's border,
    // now drawn as an SDF-batched pen instead of a separate ring), per-unit ClipToBounds scissor, and the off-clip cull.
    // The old device-less Render() skipped ALL of that, so a batchable popup rect fell to the fill-only per-unit path and
    // its border vanished (and flickered as it batched in some frames but not others).
    public override void Draw(AppTime gameTime)
    {
        if (_cache == null) return;
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

    // Pre-order flatten of each popup subtree: BuildFromComponents renders a flat list in order, so a parent must come
    // before its children for correct layering. (The children are already measured/arranged by LayoutPopups.)
    // Each popup root, then the ADORNERS of what it hosts - so a focus ring inside an overlay is drawn with that overlay:
    // above its own card, and below any overlay stacked on top of it. The adorner stage skips exactly these (it draws
    // before the popups, where they would be buried); everything decorating the window's CONTENT stays there.
    private static IReadOnlyList<IUIComponent> Flatten(IReadOnlyList<IUIComponent> roots, IWindow window)
    {
        var list = new List<IUIComponent>();
        if (roots == null) return list;

        foreach (var root in roots)
        {
            FlattenInto(root, list);
            foreach (var adorner in window.Adorners)
            {
                if (AdornsSomethingIn(adorner, root)) AdornerRenderProcessor.Collect(adorner, list);
            }
        }

        return list;
    }

    private static bool AdornsSomethingIn(IUIComponent adorner, IUIComponent root)
    {
        if (adorner is not Adamantium.UI.Controls.Adorners.Adorner { AdornedElement: { } target }) return false;

        for (IUIComponent node = target; node != null; node = node.VisualParent)
            if (ReferenceEquals(node, root)) return true;

        return false;
    }

    private static void FlattenInto(IUIComponent component, List<IUIComponent> list)
    {
        if (component.Visibility != Visibility.Visible) return;
        list.Add(component);
        foreach (var child in component.VisualChildren)
            FlattenInto(child, list);
    }
}
