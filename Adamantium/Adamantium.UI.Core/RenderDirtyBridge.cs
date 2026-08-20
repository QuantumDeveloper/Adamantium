namespace Adamantium.UI.Core;

/// <summary>
/// Translates what the VISUAL TREE says about itself (<see cref="VisualTreeNotifications"/>) into what the RENDERER needs to
/// know (<see cref="RenderDirty"/>). The one and only place the two vocabularies meet.
///
/// Everything the renderer cares about is decided HERE - that a child arriving changes the paint order, that a recolour costs
/// only a re-bake, that a move needs no re-record. Change any of it and no control changes: they only ever state facts.
/// </summary>
internal static class RenderDirtyBridge
{
    static RenderDirtyBridge()
    {
        // Entering the tree, leaving it, appearing, disappearing: WHICH elements are drawn changes - the paint order.
        VisualTreeNotifications.Attached += RenderDirty.MarkStructural;
        VisualTreeNotifications.Detached += RenderDirty.MarkStructural;
        VisualTreeNotifications.VisibilityChanged += RenderDirty.MarkStructural;

        // Shown or hidden WITHOUT leaving the layout: the paint order is untouched, so it is a content change - of this
        // element and of everything under it, since a hidden element hides its subtree with it.
        VisualTreeNotifications.ShownOrHidden += RenderDirty.MarkSubtreeGeometry;

        // Its clip changed -> the ops for its whole subtree carry a different scissor, so the recorded frame no longer
        // describes this one. STRUCTURAL, like a show/hide: what is re-recorded is not just this element's own draws.
        VisualTreeNotifications.ClipChanged += RenderDirty.MarkStructural;

        // The content it draws is stale -> it must re-render.
        VisualTreeNotifications.ContentInvalidated += RenderDirty.MarkGeometry;

        // The same content, in a different colour -> nothing re-renders; the GPU data is re-baked from the brush.
        VisualTreeNotifications.PaintInvalidated += RenderDirty.MarkPaint;

        // Same content, new place -> no re-record; the world transforms are re-composed.
        VisualTreeNotifications.Moved += RenderDirty.MarkTransform;
        VisualTreeNotifications.SubtreeMoved += RenderDirty.MarkNodeTransform;

        // A theme/DPI swap settles over several passes, and parts of that settle write outside every fact above.
        VisualTreeNotifications.StateSwapStarted += RenderDirty.ForceStructuralUntilSettled;
    }

    /// <summary>Runs the static constructor above, once, before the first fact is announced.</summary>
    internal static void Ensure() { }
}
