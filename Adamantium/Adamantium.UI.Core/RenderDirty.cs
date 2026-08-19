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
public sealed class RenderDirtyScope
{
    private readonly HashSet<IUIComponent> GeometrySet = new();
    private bool _transform;
    private bool _structural;

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
    private bool _forceUntilSettled;
    private bool _finalForcedBuild;

    // Monotonic test hook (like MeasurableUIComponent.TotalMeasureCalls): recycling-ring tests assert ZERO structural
    // marks per continuous-scroll step, which is how they catch attach/detach/Visibility churn headlessly.
    public long TotalStructuralMarks;

    /// <summary>Records that <paramref name="component"/>'s recorded geometry changed - it will re-render.</summary>
    public void MarkGeometry(IUIComponent component)
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
    public void SnapshotGeometryInto(List<IUIComponent> buffer)
    {
        buffer.Clear();
        lock (GeometrySet) buffer.AddRange(GeometrySet);
    }

    /// <summary>The geometry-dirty count, read under the write lock (safe against a concurrent mark).</summary>
    public int GeometryCount { get { lock (GeometrySet) return GeometrySet.Count; } }

    // PAINT-dirty: the component draws the SAME thing, in the same shape, with a different colour/brush/opacity (a hover, a
    // selection, a theme fade, an animated brush). Its recorded draw commands are unchanged and so are its units - only the
    // GPU data those units bake from the brush is stale. So it is NOT geometry-dirty: the recorder must not re-render it, and
    // the applier only re-bakes what it already holds.
    //
    // The distinction is what makes an animated SHARED brush affordable. Everything painting with it changes at once (the
    // pulsing skeleton cards: ~470 of them), and treating that as geometry meant re-running OnRender, rebuilding every draw
    // command, re-reconciling every unit and re-publishing every frozen layout entry - for one number that moved.
    private readonly HashSet<IUIComponent> PaintSet = new();

    /// <summary>Records that only <paramref name="component"/>'s PAINT changed - same shape, same commands, new colour.</summary>
    public void MarkPaint(IUIComponent component)
    {
        if (component == null) return;
        lock (PaintSet) PaintSet.Add(component);
        LoopSignal.Request();
    }

    /// <summary>Atomically snapshot the paint-dirty set (same locking discipline as <see cref="SnapshotGeometryInto"/>).</summary>
    public void SnapshotPaintInto(List<IUIComponent> buffer)
    {
        buffer.Clear();
        lock (PaintSet) buffer.AddRange(PaintSet);
    }

    public int PaintCount { get { lock (PaintSet) return PaintSet.Count; } }

    // NOTE on what is deliberately NOT here: assigning a DIFFERENT brush to a property (a hover, a selection, a focus ring)
    // still goes through the ordinary geometry path and re-records that element. It could be made cheaper - the recorded
    // command holds the old brush by reference, and both brushes are known at the change, so the renderer could swap the
    // reference inside the payloads it already holds. It is not worth it: that is an O(1) event (one row, one button), and
    // buying it would mean the render cache reaching into the media types to mutate them - coupling the control layer to the
    // renderer's internals for no measurable gain. The O(N) case - one SHARED brush animating under thousands of elements -
    // is the one that mattered, and it needs none of that (the object is the same; only its value moved).

    // WHICH components moved this frame (a Bounds move, a RenderTransform tick). The global <see cref="_transform"/> flag
    // above says "something moved" - enough to drop the derived world/clip memos - but the render's FROZEN layout snapshot
    // needs identities: a component's snapshot entry (its LocalTransform) is stale exactly when the component itself moved,
    // and nothing else's is (a descendant's LOCAL transform is unchanged by an ancestor's move - only the composed WORLD
    // transform is, and that is a memo). Recording the movers lets the recorder refresh O(moved) snapshot entries instead
    // of dropping the whole snapshot and re-capturing it from the RENDER-side retained groups every scroll frame - which is
    // both O(scene) and a cross-thread read of render-owned state (docs/RENDER_THREAD_PLAN.md Phase 3.3).
    private readonly HashSet<IUIComponent> MovedSet = new();

    // A move whose COMPONENT we can't name (a Transform ticking while it is not assigned as anyone's RenderTransform, so
    // Transform.Owner is null). Then the incremental refresh above cannot know what went stale, and the recorder falls back
    // to re-capturing the whole snapshot for that frame. Correctness over cleverness: an unnameable mover is rare (an
    // orphaned/re-assigned transform), and the fallback is exactly the pre-incremental behaviour.
    private bool _transformUnknown;

    /// <summary>Records that <paramref name="component"/> MOVED (world transforms must be re-baked; no re-record). Pass the
    /// component that moved - null only when the mover genuinely has no owner (see <see cref="IsTransformUnknown"/>).</summary>
    public void MarkTransform(IUIComponent component)
    {
        _transform = true;
        LoopSignal.Request();
        if (component == null) { _transformUnknown = true; return; }
        lock (MovedSet) MovedSet.Add(component);   // locked: movers arrive from the parallel arrange too (see MarkGeometry)
    }

    /// <summary>Atomically snapshot the moved components into <paramref name="buffer"/> (same lock/race as
    /// <see cref="SnapshotGeometryInto"/>).</summary>
    public void SnapshotMovedInto(List<IUIComponent> buffer)
    {
        buffer.Clear();
        lock (MovedSet) buffer.AddRange(MovedSet);
    }

    /// <summary>True when something moved that <see cref="MarkTransform"/> could not name - the frozen layout snapshot
    /// can't be refreshed incrementally this frame and must be re-captured wholesale.</summary>
    public bool IsTransformUnknown => _transformUnknown;

    // MOTION NODES that moved this frame (their subtrees translate as a unit - a scrolled panel). Unlike the global
    // _transform flag, a node move doesn't invalidate anyone's baked geometry: instances under the node reference its
    // transform-table slot, so the render just rewrites the node's matrix (64 bytes) and replays - THE O(1)-scroll path.
    private readonly HashSet<IUIComponent> NodeSet = new();

    /// <summary>Records that a MOTION NODE moved (its table slot must be rewritten; nothing re-bakes/re-records).
    /// Locked for the same parallel-arrange reason as <see cref="MarkGeometry"/>.</summary>
    public void MarkNodeTransform(IUIComponent node)
    {
        LoopSignal.Request();
        if (node == null) return;
        lock (NodeSet) NodeSet.Add(node);
    }

    /// <summary>The moved motion nodes (valid until <see cref="Clear"/>).</summary>
    public IReadOnlyCollection<IUIComponent> MovedNodes => NodeSet;

    /// <summary>Atomically snapshot the moved-node set into <paramref name="buffer"/> under the same lock
    /// <see cref="MarkNodeTransform"/> writes with (same race as <see cref="SnapshotGeometryInto"/>).</summary>
    public void SnapshotNodesInto(List<IUIComponent> buffer)
    {
        buffer.Clear();
        lock (NodeSet) buffer.AddRange(NodeSet);
    }

    // WHICH components entered or left the drawn set this frame (a visual child added/removed, a Visibility toggle). Recorded
    // like MarkTransform's movers, and for the same reason: knowing the identities is what will let the recorder splice just
    // those components instead of re-recording the whole tree. Today NOTHING reads this - the recorder still does the full walk
    // it always did - so this step is pure bookkeeping and cannot change behaviour. It only makes the next one possible.
    private readonly HashSet<IUIComponent> StructuralSet = new();

    // A structural change nobody could name (a caller with no component to hand). The incremental path can then know nothing
    // about what changed, so it must not be taken at all.
    private bool _structuralUnknown;

    /// <summary>Records a structural change (add/remove/visibility) of <paramref name="component"/> - it entered or left the
    /// drawn set, so the paint-order list must be rebuilt. Pass null ONLY when the change genuinely cannot be attributed to a
    /// component (see <see cref="IsStructuralUnknown"/>).</summary>
    // Something LEFT the visual tree. Its cached groups are withdrawn by the renderer's reconcile, which used to run only
    // inside a full walk - and full walks are rare by design now, so a view that left kept its place in the paint order and
    // the retained op stream went on re-issuing it: the tab that was left drawn on top of the tab that replaced it.
    private long _detachGeneration;

    public void MarkDetached()
    {
        System.Threading.Interlocked.Increment(ref _detachGeneration);
        // ...and ask for a frame: withdrawing it happens in a BUILD, and on an otherwise idle scene there is no next
        // build to ride on - the departed view would simply stay on screen until something else asked for one.
        LoopSignal.Request();
    }

    public long DetachGeneration => System.Threading.Interlocked.Read(ref _detachGeneration);

    public void MarkStructural(IUIComponent component = null)
    {
        _structural = true;
        LoopSignal.Request();
        TotalStructuralMarks++;
        if (component == null) { _structuralUnknown = true; return; }
        lock (StructuralSet) StructuralSet.Add(component);   // locked: marks arrive from the parallel arrange too
    }

    /// <summary>Atomically snapshot the structurally-changed components (same lock/race as <see cref="SnapshotGeometryInto"/>).</summary>
    public void SnapshotStructuralInto(List<IUIComponent> buffer)
    {
        buffer.Clear();
        lock (StructuralSet) buffer.AddRange(StructuralSet);
    }

    /// <summary>How many components are structurally marked, read under the write lock.</summary>
    public int StructuralCount { get { lock (StructuralSet) return StructuralSet.Count; } }

    /// <summary>True when the recorder cannot know WHAT entered or left the drawn set, and so must re-walk the whole tree.
    /// Two ways that happens:
    /// <list type="bullet">
    /// <item>a structural change nobody could name (<see cref="MarkStructural"/> with no component);</item>
    /// <item>a multi-frame state SWAP is settling (<see cref="ForceStructuralUntilSettled"/> - a theme swap, a DPI change).
    /// That is the whole point of that flag: such a cascade re-styles, re-resolves keyed resources and re-layouts over several
    /// passes, and SOME of its writes never route through a RenderDirty mark at all. An incremental record would splice
    /// nothing and leave every retained unit stale - the window would keep drawing the pre-swap scene.</item>
    /// </list></summary>
    public bool IsStructuralUnknown => _structuralUnknown || _forceUntilSettled || _finalForcedBuild;

    /// <summary>Force full structural rebuilds until the layout signals it has fully settled (see
    /// <see cref="_forceUntilSettled"/>). Call when starting a multi-frame state swap (theme, DPI).</summary>
    public void ForceStructuralUntilSettled() { _forceUntilSettled = true; LoopSignal.Request(); }

    /// <summary>Called by the layout manager after a pass that found NO work (every queue empty) - the settle signal for
    /// <see cref="ForceStructuralUntilSettled"/>. The current frame's build stays forced (the final walk, which follows
    /// this pass and so sees every settle write); after it, forcing ends.</summary>
    public void NotifyLayoutQuiescent()
    {
        if (!_forceUntilSettled) return;
        _forceUntilSettled = false;
        _finalForcedBuild = true;
    }

    /// <summary>Any dirty state at all (else the frame is fully clean).</summary>
    public bool HasWork => _structural || _transform || GeometrySet.Count > 0 || PaintSet.Count > 0
                                  || NodeSet.Count > 0 || _forceUntilSettled || _finalForcedBuild;

    public bool IsStructural => _structural || _forceUntilSettled || _finalForcedBuild;
    public bool IsTransform => _transform;

    /// <summary>True while a multi-frame structural state swap (resize / DPI / theme) is still settling. The Phase 3.2
    /// decoupled render path uses this as a lightweight barrier: it records such a window INLINE (record + apply together
    /// in BeginDraw) instead of at loop level, so the packet can't straddle the swap's per-frame relayout + presenter /
    /// projection finalisation and desync (chrome left at the old size while dirty content re-records at the new one).</summary>
    public bool IsSettlingStructural => _forceUntilSettled || _finalForcedBuild;

    /// <summary>The geometry-dirty components to re-render this build (only valid until <see cref="Clear"/>).</summary>
    public IReadOnlyCollection<IUIComponent> Geometry => GeometrySet;

    /// <summary>Reset after a build has consumed the dirty state.</summary>
    public void Clear()
    {
        // Clear the HashSets under the SAME locks their writers (MarkGeometry/MarkNodeTransform/MarkTransform) take - a
        // lock-free Clear racing a concurrent Add (parallel arrange / Dispatcher-thread invalidation) corrupted the set.
        lock (GeometrySet) GeometrySet.Clear();
        lock (PaintSet) PaintSet.Clear();
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
