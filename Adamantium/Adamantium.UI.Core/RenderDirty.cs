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
        // Locked: writers run on MORE than the render-loop thread - a PARALLEL arrange pass (VirtualizingPanel) as each
        // tile's size settles, AND Dispatcher operations (e.g. an Image frame-timer's InvalidateRender) which execute on
        // the Win32 message-pump thread, NOT the render loop. HashSet is not thread-safe, so every read/clear the build
        // does must take THIS lock too (see SnapshotGeometryInto/GeometryCount/Clear) - a lock-free Add racing the build's
        // enumeration corrupted the set (NRE + "concurrent update" + a stuck-dirty FPS collapse). The scalar counters below
        // stay lock-free (a lost increment only mis-counts a diagnostic).
        if (component == null) return;
        lock (GeometrySet) GeometrySet.Add(component);
        LoopSignal.Request();   // the scene changed - the loop owes a frame
    }

    /// <summary>Atomically snapshot the geometry-dirty set into <paramref name="buffer"/> under the same lock
    /// <see cref="MarkGeometry"/> writes with, so a concurrent mark (parallel arrange / a Dispatcher-thread invalidation)
    /// can't corrupt the enumeration. The set itself is NOT cleared here (the build clears it via <see cref="Clear"/>).</summary>
    public static void SnapshotGeometryInto(List<IUIComponent> buffer)
    {
        buffer.Clear();
        lock (GeometrySet) buffer.AddRange(GeometrySet);
    }

    /// <summary>The geometry-dirty count, read under the write lock (safe against a concurrent mark).</summary>
    public static int GeometryCount { get { lock (GeometrySet) return GeometrySet.Count; } }

    // WHICH components moved this frame (a Bounds move, a RenderTransform tick). The global <see cref="_transform"/> flag
    // above says "something moved" - enough to drop the derived world/clip memos - but the render's FROZEN layout snapshot
    // needs identities: a component's snapshot entry (its LocalTransform) is stale exactly when the component itself moved,
    // and nothing else's is (a descendant's LOCAL transform is unchanged by an ancestor's move - only the composed WORLD
    // transform is, and that is a memo). Recording the movers lets the recorder refresh O(moved) snapshot entries instead
    // of dropping the whole snapshot and re-capturing it from the RENDER-side retained groups every scroll frame - which is
    // both O(scene) and a cross-thread read of render-owned state (docs/RENDER_THREAD_PLAN.md Phase 3.3).
    private static readonly HashSet<IUIComponent> MovedSet = new();

    // A move whose COMPONENT we can't name (a Transform ticking while it is not assigned as anyone's RenderTransform, so
    // Transform.Owner is null). Then the incremental refresh above cannot know what went stale, and the recorder falls back
    // to re-capturing the whole snapshot for that frame. Correctness over cleverness: an unnameable mover is rare (an
    // orphaned/re-assigned transform), and the fallback is exactly the pre-incremental behaviour.
    private static bool _transformUnknown;

    /// <summary>Records that <paramref name="component"/> MOVED (world transforms must be re-baked; no re-record). Pass the
    /// component that moved - null only when the mover genuinely has no owner (see <see cref="IsTransformUnknown"/>).</summary>
    public static void MarkTransform(IUIComponent component)
    {
        _transform = true;
        LoopSignal.Request();
        if (component == null) { _transformUnknown = true; return; }
        lock (MovedSet) MovedSet.Add(component);   // locked: movers arrive from the parallel arrange too (see MarkGeometry)
    }

    /// <summary>Atomically snapshot the moved components into <paramref name="buffer"/> (same lock/race as
    /// <see cref="SnapshotGeometryInto"/>).</summary>
    public static void SnapshotMovedInto(List<IUIComponent> buffer)
    {
        buffer.Clear();
        lock (MovedSet) buffer.AddRange(MovedSet);
    }

    /// <summary>True when something moved that <see cref="MarkTransform"/> could not name - the frozen layout snapshot
    /// can't be refreshed incrementally this frame and must be re-captured wholesale.</summary>
    public static bool IsTransformUnknown => _transformUnknown;

    // MOTION NODES that moved this frame (their subtrees translate as a unit - a scrolled panel). Unlike the global
    // _transform flag, a node move doesn't invalidate anyone's baked geometry: instances under the node reference its
    // transform-table slot, so the render just rewrites the node's matrix (64 bytes) and replays - THE O(1)-scroll path.
    private static readonly HashSet<IUIComponent> NodeSet = new();

    /// <summary>Records that a MOTION NODE moved (its table slot must be rewritten; nothing re-bakes/re-records).
    /// Locked for the same parallel-arrange reason as <see cref="MarkGeometry"/>.</summary>
    public static void MarkNodeTransform(IUIComponent node)
    {
        LoopSignal.Request();
        if (node == null) return;
        lock (NodeSet) NodeSet.Add(node);
    }

    /// <summary>The moved motion nodes (valid until <see cref="Clear"/>).</summary>
    public static IReadOnlyCollection<IUIComponent> MovedNodes => NodeSet;

    /// <summary>Atomically snapshot the moved-node set into <paramref name="buffer"/> under the same lock
    /// <see cref="MarkNodeTransform"/> writes with (same race as <see cref="SnapshotGeometryInto"/>).</summary>
    public static void SnapshotNodesInto(List<IUIComponent> buffer)
    {
        buffer.Clear();
        lock (NodeSet) buffer.AddRange(NodeSet);
    }

    // WHICH components entered or left the drawn set this frame (a visual child added/removed, a Visibility toggle). Recorded
    // like MarkTransform's movers, and for the same reason: knowing the identities is what will let the recorder splice just
    // those components instead of re-recording the whole tree. Today NOTHING reads this - the recorder still does the full walk
    // it always did - so this step is pure bookkeeping and cannot change behaviour. It only makes the next one possible.
    private static readonly HashSet<IUIComponent> StructuralSet = new();

    // A structural change nobody could name (a caller with no component to hand). The incremental path can then know nothing
    // about what changed, so it must not be taken at all.
    private static bool _structuralUnknown;

    /// <summary>Records a structural change (add/remove/visibility) of <paramref name="component"/> - it entered or left the
    /// drawn set, so the paint-order list must be rebuilt. Pass null ONLY when the change genuinely cannot be attributed to a
    /// component (see <see cref="IsStructuralUnknown"/>).</summary>
    public static void MarkStructural(IUIComponent component = null)
    {
        _structural = true;
        LoopSignal.Request();
        TotalStructuralMarks++;
        if (component == null) { _structuralUnknown = true; return; }
        lock (StructuralSet) StructuralSet.Add(component);   // locked: marks arrive from the parallel arrange too
    }

    /// <summary>Atomically snapshot the structurally-changed components (same lock/race as <see cref="SnapshotGeometryInto"/>).</summary>
    public static void SnapshotStructuralInto(List<IUIComponent> buffer)
    {
        buffer.Clear();
        lock (StructuralSet) buffer.AddRange(StructuralSet);
    }

    /// <summary>How many components are structurally marked, read under the write lock.</summary>
    public static int StructuralCount { get { lock (StructuralSet) return StructuralSet.Count; } }

    /// <summary>True when the recorder cannot know WHAT entered or left the drawn set, and so must re-walk the whole tree.
    /// Two ways that happens:
    /// <list type="bullet">
    /// <item>a structural change nobody could name (<see cref="MarkStructural"/> with no component);</item>
    /// <item>a multi-frame state SWAP is settling (<see cref="ForceStructuralUntilSettled"/> - a theme swap, a DPI change).
    /// That is the whole point of that flag: such a cascade re-styles, re-resolves keyed resources and re-layouts over several
    /// passes, and SOME of its writes never route through a RenderDirty mark at all. An incremental record would splice
    /// nothing and leave every retained unit stale - the window would keep drawing the pre-swap scene.</item>
    /// </list></summary>
    public static bool IsStructuralUnknown => _structuralUnknown || _forceUntilSettled || _finalForcedBuild;

    /// <summary>Force full structural rebuilds until the layout signals it has fully settled (see
    /// <see cref="_forceUntilSettled"/>). Call when starting a multi-frame state swap (theme, DPI).</summary>
    public static void ForceStructuralUntilSettled() { _forceUntilSettled = true; LoopSignal.Request(); }

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

    /// <summary>True while a multi-frame structural state swap (resize / DPI / theme) is still settling. The Phase 3.2
    /// decoupled render path uses this as a lightweight barrier: it records such a window INLINE (record + apply together
    /// in BeginDraw) instead of at loop level, so the packet can't straddle the swap's per-frame relayout + presenter /
    /// projection finalisation and desync (chrome left at the old size while dirty content re-records at the new one).</summary>
    public static bool IsSettlingStructural => _forceUntilSettled || _finalForcedBuild;

    /// <summary>The geometry-dirty components to re-render this build (only valid until <see cref="Clear"/>).</summary>
    public static IReadOnlyCollection<IUIComponent> Geometry => GeometrySet;

    /// <summary>Reset after a build has consumed the dirty state.</summary>
    public static void Clear()
    {
        // Clear the HashSets under the SAME locks their writers (MarkGeometry/MarkNodeTransform/MarkTransform) take - a
        // lock-free Clear racing a concurrent Add (parallel arrange / Dispatcher-thread invalidation) corrupted the set.
        lock (GeometrySet) GeometrySet.Clear();
        lock (NodeSet) NodeSet.Clear();
        lock (MovedSet) MovedSet.Clear();
        lock (StructuralSet) StructuralSet.Clear();
        _transform = false;
        _transformUnknown = false;
        _structural = false;
        _structuralUnknown = false;
        _finalForcedBuild = false;   // the post-settle walk ran; forcing ends (_forceUntilSettled survives Clear by design)
    }
}
