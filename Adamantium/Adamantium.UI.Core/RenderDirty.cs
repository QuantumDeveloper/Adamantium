using System.Collections.Generic;

namespace Adamantium.UI.Core;

/// <summary>
/// Per-frame render dirty registry for the render-cache redesign (docs/RENDER_CACHE_REDESIGN.md §4a/§4i). Instead of
/// re-walking the whole visual tree every frame, invalidation records WHAT changed here, and the render cache re-does
/// only that:
/// <list type="bullet">
/// <item><b>Geometry</b> - components whose RECORDED draw output changed (colour/size/shape/opacity/visibility). Only
/// these re-render (a partial rebuild); every other unit is kept as-is.</item>
/// <item><b>Transform</b> - something MOVED (a <c>Bounds</c> / <c>RenderTransform</c> change). The recorded geometry is
/// unchanged, so nothing re-renders; the render pass just re-bakes the world transforms (drop the frame-scoped memo).</item>
/// <item><b>Structural</b> - a visual child was added/removed, or a partial re-render changed a component's draw-command
/// COUNT (so the retained paint-order list no longer matches). Forces a full walk to rebuild that list.</item>
/// </list>
/// A frame with none of these set is fully clean: the cache re-draws last frame's retained units with ~0 CPU.
/// </summary>
/// <remarks>
/// CONSERVATIVE by construction: over-marking only costs a redundant re-render/rebuild (correct, just slower); the only
/// unsafe outcome - a real change marking NOTHING - can't happen because every mutation path marks at least one of the
/// three (and a running property animation marks Geometry each tick). Single-threaded (UI/render thread).
/// </remarks>
public static class RenderDirty
{
    private static readonly HashSet<IUIComponent> GeometrySet = new();
    private static bool _transform;
    private static bool _structural;

    // Force full structural rebuilds while a multi-frame state swap SETTLES (a theme swap, a DPI change): those cascades
    // re-style, re-resolve keyed resources (brushes) and re-layout over SEVERAL passes - spread further by the layout
    // frame budget - and some of their writes don't route through a RenderDirty mark (the restyle/ResourcesChanged
    // flush), so a Clean-frame op-replay would keep showing the stale build until an unrelated mark (a hover) forced a
    // walk. "Settled" is signalled by the LAYOUT itself (NotifyLayoutQuiescent - a pass that found NO work: every queue
    // empty), not by a frame count: a heavy tree under a tight budget keeps forcing for exactly as long as it drains, a
    // light one stops after a couple of frames. All the swap's activity flows through the layout pass (binding flush at
    // its start, style/measure/arrange drains, the resource flush at its end), and the pass runs BEFORE the frame's
    // render build - so the quiescent frame's OWN build (still forced, via _finalForcedBuild) is guaranteed to see even
    // the writes made by the LAST pass's end-of-pass resource flush. Then the flag clears in Clear().
    private static bool _forceUntilSettled;
    private static bool _finalForcedBuild;

    // Monotonic test hook (like MeasurableUIComponent.TotalMeasureCalls): recycling-ring tests assert ZERO structural
    // marks per continuous-scroll step, which is how they catch attach/detach/Visibility churn headlessly.
    public static long TotalStructuralMarks;

    /// <summary>Records that <paramref name="component"/>'s recorded geometry changed - it will re-render.</summary>
    public static void MarkGeometry(IUIComponent component)
    {
        // Locked: a PARALLEL arrange pass (VirtualizingPanel) calls this concurrently as each tile's size settles;
        // HashSet is not thread-safe so a lock-free Add could corrupt it. Uncontended in the single-threaded case (the
        // common path). The scalar counters below stay lock-free (a lost increment only mis-counts a diagnostic).
        if (component == null) return;
        lock (GeometrySet) GeometrySet.Add(component);
    }

    /// <summary>Records that something moved (world transforms must be re-baked; no re-record).</summary>
    public static void MarkTransform() => _transform = true;

    // MOTION NODES that moved this frame (their subtrees translate as a unit - a scrolled panel). Unlike the global
    // _transform flag, a node move doesn't invalidate anyone's baked geometry: instances under the node reference its
    // transform-table slot, so the render just rewrites the node's matrix (64 bytes) and replays - THE O(1)-scroll path.
    private static readonly HashSet<IUIComponent> NodeSet = new();

    /// <summary>Records that a MOTION NODE moved (its table slot must be rewritten; nothing re-bakes/re-records).
    /// Locked for the same parallel-arrange reason as <see cref="MarkGeometry"/>.</summary>
    public static void MarkNodeTransform(IUIComponent node)
    {
        if (node == null) return;
        lock (NodeSet) NodeSet.Add(node);
    }

    /// <summary>The moved motion nodes (valid until <see cref="Clear"/>).</summary>
    public static IReadOnlyCollection<IUIComponent> MovedNodes => NodeSet;

    /// <summary>Records a structural change (add/remove/command-count change) - the paint-order list must be rebuilt.</summary>
    public static void MarkStructural() { _structural = true; TotalStructuralMarks++; }

    /// <summary>Force full structural rebuilds until the layout signals it has fully settled (see
    /// <see cref="_forceUntilSettled"/>). Call when starting a multi-frame state swap (theme, DPI).</summary>
    public static void ForceStructuralUntilSettled() => _forceUntilSettled = true;

    /// <summary>Called by the layout manager after a pass that found NO work (every queue empty) - the settle signal for
    /// <see cref="ForceStructuralUntilSettled"/>. The current frame's build stays forced (the final walk, which follows
    /// this pass and so sees every settle write); after it, forcing ends.</summary>
    public static void NotifyLayoutQuiescent()
    {
        if (!_forceUntilSettled) return;
        _forceUntilSettled = false;
        _finalForcedBuild = true;
    }

    /// <summary>Any dirty state at all (else the frame is fully clean).</summary>
    public static bool HasWork => _structural || _transform || GeometrySet.Count > 0 || NodeSet.Count > 0 || _forceUntilSettled || _finalForcedBuild;

    public static bool IsStructural => _structural || _forceUntilSettled || _finalForcedBuild;
    public static bool IsTransform => _transform;

    /// <summary>The geometry-dirty components to re-render this build (only valid until <see cref="Clear"/>).</summary>
    public static IReadOnlyCollection<IUIComponent> Geometry => GeometrySet;

    /// <summary>Reset after a build has consumed the dirty state.</summary>
    public static void Clear()
    {
        GeometrySet.Clear();
        NodeSet.Clear();
        _transform = false;
        _structural = false;
        _finalForcedBuild = false;   // the post-settle walk ran; forcing ends (_forceUntilSettled survives Clear by design)
    }
}
