using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Rendering;

public partial class RenderCache
{
    private Matrix4x4F _projectionMatrix;

    // Frame-scoped world-transform memo. WorldTransform is O(depth) live and read many times per unit (ResolveScissor
    // walks every clip ancestor) -> O(depth^2) naive. Transforms are stable within a frame, so compose each ONCE here:
    // World(c) = LocalTransform * World(parent) => O(nodes)/frame. Render-path only; WorldTransform stays live for hit-test.
    private readonly Dictionary<IUIComponent, Matrix4x4F> _worldCache = new();

    // RECORDER-owned frozen layout (the authority, mutated on the update thread). The APPLIER never reads it - it folds
    // each packet's SnapDelta into its own replica (_applySnap), so the threads share no mutable map. Refreshed incrementally.
    private readonly Dictionary<IUIComponent, LayoutSnapshot> _snap = new();

    // APPLIER-owned replica, built ONLY from the deltas of packets it has consumed - what the draw pass actually reads.
    private readonly Dictionary<IUIComponent, LayoutSnapshot> _applySnap = new();

    // --- The recorder -> applier seam (docs/RENDER_THREAD_PLAN.md) ---
    // DOUBLE-BUFFERED: the recorder fills one packet while the applier consumes another, so the loop no longer waits for
    // the GPU frame. Packets are DELTAS, so a queued one may never be DROPPED (a Partial carries only that frame's dirty
    // components) - the applier drains EVERY queued packet in order and draws ONCE, collapsing stale frames at the DRAW.
    private readonly ConcurrentQueue<RenderPacket> _published = new();   // recorded, awaiting the applier
    private readonly ConcurrentBag<RenderPacket> _spare = new();         // consumed, back for reuse
    private RenderPacket _packet;                                        // the one being recorded right now

    private readonly Dictionary<IUIComponent, float> _opacityChain = new();   // memo of OpacityChain, like _worldCache

    // --- Motion-node memos (the O(1)-scroll path) ---
    // NodeOf: the nearest IsRenderMotionNode ancestor (or null). RelWorld: the transform RELATIVE to that node (identity
    // AT the node) - what a node-local bake uses; the shader applies the node's table matrix on top. Cleared with _worldCache.
    private readonly Dictionary<IUIComponent, IUIComponent> _nodeCache = new();
    private readonly Dictionary<IUIComponent, Matrix4x4F> _relWorldCache = new();
    private readonly Dictionary<IUIComponent, int> _nodeRefreshed = new();   // node -> walk version its slot was refreshed
    // Per RECORDING walk: node -> "every drawn unit under it is in a node-aware batch (rect/ellipse with its slot)". A
    // moved node with ANY non-aware content (world-baked text, per-unit draws) can't take the slot-write fast path -> the
    // frame falls back to the full walk.
    private readonly Dictionary<Guid, bool> _nodeAllAware = new();
    // APPLIER-owned: the moved nodes of the packets drained for THIS draw (it rewrites their table matrices, then clears).
    // The recorder must not read it - it takes the frame's moved nodes off RenderDirty into packet.MovedNodes.
    private readonly List<IUIComponent> _movedNodesBuf = new();
    // RECORDER-owned: the components that MOVED this frame - CaptureSnapshot re-freezes exactly their snapshot entries.
    private readonly List<IUIComponent> _movedBuf = new();
    private bool _snapFullCapture;   // adorner build only: re-capture the snapshot from the retained units

    // Frame-scoped clip memo. A unit's scissor = the intersection of every ClipToBounds ancestor's world-space viewport -
    // depends ONLY on the ancestor chain, so all units under one clipping ancestor share it (the old code re-walked per
    // unit). CumulativeClip(c) = (c clips ? c.worldRect : none) ∩ CumulativeClip(parent). Cleared each frame.
    private readonly Dictionary<IUIComponent, Rect?> _clipCache = new();

    // CPU pre-transform text batch aggregator (docs/TEXT_GLYPH_BATCH_PLAN.md §9). Lazy on the first Render with a device
    // (GPU-free test renders never batch). Frame-scoped state lives inside it.
    private TextBatchCollector _textBatch;

    /// <summary>Builds units from a FLAT list of components (the adorner stage), not a tree walk. Components cache by
    /// RenderId as in the tree build; units of components no longer in the list are disposed. For overlays outside the
    /// content tree.</summary>
    // readOnly: emit each component's commands via RenderReadOnly (no IsGeometryValid touch / no RenderDirty mark) instead
    // of Render() - for snapshotting a LIVE, already-valid subtree through this parallel cache without disturbing the
    // window (whose loop a mark would wake into a concurrent, hanging render). Adorners (the default) pass false.
    public void BuildFromComponents(IReadOnlyList<IUIComponent> components, Matrix4x4F projectionMatrix, bool readOnly = false)
    {
        // A FULL rebuild every call. Must record LastBuildKind=Full: the batches' Clean-frame upload-skip reads it, else
        // the overlay batch skips every GPU upload - its SSBO never fills and the whole overlay renders nothing.
        LastBuildKind = RenderBuildKind.Full;
        _commands.Clear();
        ClearOrder();
        _snap.Clear();         // full rebuild -> drop last frame's frozen layout snapshot (else stale overlay positions + unbounded _snap growth)
        _worldCache.Clear();   // new frame: drop last frame's transform + clip memos
        _clipCache.Clear();
        _relWorldCache.Clear();
        _nodeCache.Clear();

        // This build FUSES record and apply (an overlay renders a flat list straight into its groups - it never crosses
        // the render-thread seam), but the snapshot still flows through a packet - the only thing that fills the applier's
        // replica. Rent one, capture into it, fold it in, hand it back.
        _packet = RentPacket();
        _packet.Reset(RenderBuildKind.Full);
        _packet.SnapReset = true;

        var present = new HashSet<Guid>();
        if (components != null)
        {
            long order = 0;
            foreach (var component in components)
            {
                if (component.Visibility != Visibility.Visible) continue;
                present.Add(component.RenderId);

                var wasGeometryValid = component.IsGeometryValid;
                _drawingContextInternal.Clear();
                if (readOnly) 
                    component.RenderReadOnly(_drawingContext); 
                else 
                    component.Render(_drawingContext);
                ProcessRenderCommands(component, _drawingContextInternal.GetDrawCommands(), projectionMatrix, !readOnly && wasGeometryValid, order);
                order += OrderGap;   // the flat list IS the paint order
            }
        }

        // Free the units of any component dropped from the list since the last build.
        List<Guid> stale = null;
        foreach (var id in _groupById.Keys)
            if (!present.Contains(id)) (stale ??= new List<Guid>()).Add(id);
        if (stale != null)
            foreach (var id in stale) RemoveAndDeferDispose(id);

        // Freeze the overlay's snapshot. Its draws never went through a packet, so the capture reads the just-built GROUPS
        // - safe only here (this cache's recorder and applier are the same thread).
        _snapFullCapture = true;
        CaptureSnapshot();

        _applySnap.Clear();
        foreach (var entry in _packet.SnapDelta) _applySnap[entry.Key] = entry.Value;
        _packet.Reset(RenderBuildKind.Clean);
        _spare.Add(_packet);
        _packet = null;
    }

    /// <summary>Disposes every cached unit and empties the cache (caller ensures the GPU is idle first). For the off-screen
    /// designer, which builds a new tree per render: those controls never detach (each owns its root window), so
    /// attachment-based reconciliation can't reclaim them - the designer resets between renders instead.</summary>
    public void DisposeUnits()
    {
        foreach (var group in _groupById.Values)
        {
            foreach (var unit in group.Units)
                unit?.Dispose();
        }

        _groupById.Clear();
        ClearOrder();
        _recordedUnits.Clear();   // the recorder's mirror of the above
        // The designer builds a new tree per render (fresh RenderIds), so the old frozen layout would grow unboundedly.
        _snap.Clear();
        _applySnap.Clear();
    }

    /// <summary>The last packet's projection, captured by the RECORDER from the root visual. The applier uses this, not
    /// the live window (it may be the render thread).</summary>
    public Matrix4x4F AppliedProjection => _projectionMatrix;

    public void ProcessCommands(Matrix4x4F projectionMatrix, double renderScale)
    {
        _renderScale = renderScale;
        _projectionMatrix = projectionMatrix;
        foreach (var group in _groups)
        foreach (var unit in group.Units)
        {
            var transform = World(unit.Component);
            unit.Update(transform, projectionMatrix, renderScale);
        }
    }

    private RenderPacket RentPacket() => _spare.TryTake(out var packet) ? packet : new RenderPacket();

    // Record one component's frozen layout into the recorder's map AND this frame's delta (the applier's replica is built
    // from nothing else). Memoised: an unchanged component is captured once and never re-sent.
    private LayoutSnapshot Snap(IUIComponent c)
    {
        if (_snap.TryGetValue(c, out var s)) return s;
        s = new LayoutSnapshot(c.LocalTransform, c.RenderSize, c.ClipToBounds, c.IsRenderMotionNode, c.RenderParent, (float)c.Opacity, (float)c.SelfOpacity);
        _snap[c] = s;
        _packet.SnapDelta.Add(new KeyValuePair<IUIComponent, LayoutSnapshot>(c, s));
        return s;
    }

    // APPLIER's view: its private replica, folded from the packets' deltas. A miss (impossible for anything drawn) falls
    // back to the live component - the ONE live read left on this side, unreachable in the recorded paths.
    private LayoutSnapshot ApplySnap(IUIComponent c)
    {
        if (_applySnap.TryGetValue(c, out var s)) return s;
        s = new LayoutSnapshot(c.LocalTransform, c.RenderSize, c.ClipToBounds, c.IsRenderMotionNode, c.RenderParent, (float)c.Opacity, (float)c.SelfOpacity);
        _applySnap[c] = s;
        return s;
    }

    // Eagerly freeze the layout of EVERY component the draw/compose pass will read (each unit's component + its ancestor
    // chain + the moved motion nodes), at the END of the record. After this the applier's Snap() lookups all HIT, so the
    // draw never dereferences a live component - the recorder->applier handoff that lets the applier run on its own thread
    // (docs/RENDER_THREAD_PLAN.md). INCREMENTAL: the snapshot PERSISTS; only what changed this frame is re-frozen (the
    // packet's re-records, moved nodes, moved components - RenderDirty names them; an ancestor moving changes only the
    // WORLD memo, dropped separately). A Full drops the snapshot and re-freezes everything from its packet. The group walk
    // survives only for the flat adorner build (no packet, recorder + applier inline on the loop thread).
    private void CaptureSnapshot()
    {
        if (_snapFullCapture)
        {
            foreach (var group in _groups)
            foreach (var unit in group.Units)
                for (var c = unit.Component; c != null && !_snap.ContainsKey(c); c = c.RenderParent)
                    Snap(c);
            _snapFullCapture = false;
        }

        // Element opacity lives IN the snapshot (unlike a brush recolour, which re-bakes from the brush BY REFERENCE), so a
        // paint-dirty component whose opacity ACTUALLY changed must re-publish its entry - on any build kind. Gated on a
        // real change so the common brush pulse (~470 cards/frame) re-freezes nothing.
        RenderDirty.SnapshotPaintInto(_opacityCheckBuf);
        foreach (var c in _opacityCheckBuf)
            if (IsDrawn(c) && (!_snap.TryGetValue(c, out var f) || f.Opacity != (float)c.Opacity || f.SelfOpacity != (float)c.SelfOpacity))
                RefreshSnapshot(c);

        if (_packet.Kind == RenderBuildKind.Full)
        {
            // A FULL walk re-records the whole scene to rebuild the paint ORDER - it does NOT mean the whole scene's LAYOUT
            // changed. Freeze only what a mark says changed, plus first-seen components (Snap fills those lazily).
            // Unnameable changes already cleared the snapshot in RecordFullFrame, so every entry below is genuinely new.
            foreach (var draw in _packet.Draws)
                for (var c = draw.Component; c != null && !_snap.ContainsKey(c); c = c.RenderParent)
                    Snap(c);

            foreach (var component in _geometryDirtyBuffer) RefreshSnapshot(component);   // size / clip may have changed
            foreach (var component in _structuralBuf) RefreshSnapshot(component);         // VisualParent may have changed
            foreach (var node in _movedNodesCapture) RefreshSnapshot(node);
            foreach (var moved in _movedBuf) RefreshSnapshot(moved);
            return;
        }

        // Re-recorded this frame (the dirty/newly-spliced components of a Partial or a Structural).
        foreach (var draw in _packet.Draws) RefreshSnapshot(draw.Component);

        // EVERY geometry-dirty component, not just re-recorded ones: a component can change SIZE without its recorded
        // geometry going stale (RenderSize marks it dirty but leaves IsGeometryValid true -> the record SKIPS it), and a
        // snapshot only re-frozen for what was recorded keeps drawing it at the old size (grid gaps/overlaps). Only what is
        // actually DRAWN: a component that left the drawn set had its entry dropped on purpose (re-frozen when it returns);
        // re-adding it would resurrect it and publish a delta for something the applier just freed.
        foreach (var component in _geometryDirtyBuffer)
            if (IsDrawn(component)) RefreshSnapshot(component);

        // A component that kept its units but MOVED (VisualParent changed) - nothing else would re-freeze it.
        foreach (var rerank in _packet.Reranks) RefreshSnapshot(rerank.Key);
        // Moved this frame. A stale moved-MOTION-NODE entry is the classic O(1)-path regression: World composes it from
        // LAST frame's LocalTransform, so a tilting tile never moves and a flip sticks at its old angle (the angle lives
        // only here). Read off THIS frame's packet: _movedNodesBuf is the APPLIER's copy.
        foreach (var node in _packet.MovedNodes) RefreshSnapshot(node);
        foreach (var moved in _movedBuf) RefreshSnapshot(moved);
    }

    // Re-freeze ONE component's entry (it changed this frame, so the ContainsKey early-out must NOT keep the old one), then
    // walk its ancestor chain lazily - those didn't change unless they are themselves in a changed set.
    private void RefreshSnapshot(IUIComponent component)
    {
        if (component == null) return;
        var snapshot = new LayoutSnapshot(component.LocalTransform, component.RenderSize, component.ClipToBounds,
            component.IsRenderMotionNode, component.RenderParent, (float)component.Opacity, (float)component.SelfOpacity);
        _snap[component] = snapshot;
        // ...AND publish it: the delta is the ONLY source of the applier's replica, so updating the recorder's map alone
        // leaves the applier composing from the PREVIOUS transform (a tilting tile never moves). Snap() below publishes on
        // its own only for a NEW entry, so this force-refresh (overwriting an existing one) must publish explicitly.
        _packet.SnapDelta.Add(new KeyValuePair<IUIComponent, LayoutSnapshot>(component, snapshot));
        for (var c = component.RenderParent; c != null && !_snap.ContainsKey(c); c = c.RenderParent)
            Snap(c);
    }

    private Matrix4x4F World(IUIComponent c)
    {
        if (_worldCache.TryGetValue(c, out var m)) return m;
        var s = ApplySnap(c);
        m = s.RenderParent != null ? s.LocalTransform * World(s.RenderParent) : s.LocalTransform;
        _worldCache[c] = m;
        return m;
    }

    /// <summary>Rebase the snapshot so <paramref name="element"/> sits at the ORIGIN: freeze it with an IDENTITY local
    /// transform and NO render parent, so <see cref="World"/> stops there and every descendant composes RELATIVE to it. For
    /// an off-screen snapshot of a LIVE element into an element-sized target (projection 0,0..size): the geometry AND the
    /// per-unit clip scissors then share that same 0-based space. Without it, World() runs on up to the window root, so the
    /// scissors stay in absolute window coords and <see cref="ToFramebufferScissor"/> clamps the whole subtree to the
    /// target's edge. Call AFTER BuildFromComponents, BEFORE ProcessCommands.</summary>
    public void RebaseToOrigin(IUIComponent element)
    {
        if (element == null) return;
        var s = ApplySnap(element);
        _applySnap[element] = new LayoutSnapshot(Matrix4x4F.Identity, s.RenderSize, s.ClipToBounds, false, null, s.Opacity, s.SelfOpacity);
        _worldCache.Clear();    // drop any absolute transforms/clips memoised during the build so ProcessCommands recomputes rebased
        _relWorldCache.Clear();
        _clipCache.Clear();
        _nodeCache.Clear();
    }

    // Effective alpha the bake folds into a unit's colour: SelfOpacity x the OPACITY chain (own Opacity x every
    // ancestor's). Reads ONLY the frozen snapshot - no live property, so no lock/box, render-thread safe
    // (see hot-paths-must-not-use-property-system). Cheaper than World (scalar mul, not matrix).
    private float EffectiveOpacity(IUIComponent c)
    {
        var s = ApplySnap(c);
        return s.SelfOpacity * OpacityChain(c);
    }

    private float OpacityChain(IUIComponent c)
    {
        if (_opacityChain.TryGetValue(c, out var v)) return v;
        var s = ApplySnap(c);
        v = s.RenderParent != null ? s.Opacity * OpacityChain(s.RenderParent) : s.Opacity;
        _opacityChain[c] = v;
        return v;
    }

    private IUIComponent NodeOf(IUIComponent c)
    {
        if (c == null) return null;
        if (_nodeCache.TryGetValue(c, out var n)) return n;
        var s = ApplySnap(c);
        n = s.IsMotionNode ? c : NodeOf(s.RenderParent);
        _nodeCache[c] = n;
        return n;
    }

    private Matrix4x4F RelWorld(IUIComponent c)
    {
        if (_relWorldCache.TryGetValue(c, out var m)) return m;
        var s = ApplySnap(c);
        m = s.IsMotionNode
            ? Matrix4x4F.Identity
            : (s.RenderParent is { } p ? s.LocalTransform * RelWorld(p) : s.LocalTransform);
        _relWorldCache[c] = m;
        return m;
    }

    // True when a matrix only scales and translates, so an axis-aligned rect stays one under it and the bake can fold it
    // into the instance's bounds. Rotation or shear puts numbers in M12/M21, and no axis-aligned rect can carry those.
    private static bool IsAxisAligned(in Matrix4x4F m)
    {
        const float eps = 1e-4f;   // same threshold the batch collectors use when they check a bake
        return Math.Abs(m.M12) <= eps && Math.Abs(m.M21) <= eps;
    }

    // A unit's bake transform + transform-table slot: node-local + the node's slot when under a motion node (matrix
    // refreshed once per walk), else its OWN slot holding the world. NOTHING is baked into an instance any more.
    private Matrix4x4F ResolveBake(IGraphicsDevice device, IUIComponent component, Matrix4x4F world, out int slot)
    {
        var node = NodeOf(component);
        if (node == null)
        {
            // The world goes to the TABLE, never into the instance's bounds. It used to fold in ("slot 0 = identity")
            // whenever it was axis-aligned, which was almost everything - and that fold is what made a matrix rewrite
            // move only PART of the frame: content that rides a slot follows it the moment the slot is written, while
            // a world-baked instance keeps drawing where it was baked until something re-bakes it. Two ways of saying
            // where a thing is, updated on different schedules, is a desync waiting for a frame to land between them.
            // Now there is one way. Costs a slot per drawn element; SetMatrix skips the write when nothing changed, so
            // a still frame is still free.
            slot = _transformTable.AcquireSlot(component.RenderId);
            _transformTable.SetMatrix(device, slot, world);
            return Matrix4x4F.Identity;
        }
        var rel = RelWorld(component);
        // Rotated/sheared UNDER a motion node (a spinner inside a scrolling list): the node's slot carries the NODE's
        // matrix, and node-local bounds would have to carry the rotation - which an axis-aligned rect cannot. Give this
        // unit its own slot holding its FULL world instead, and tell the node it can no longer move everything by
        // writing its own slot (this unit must be re-baked when the node moves, exactly as the per-unit path it replaces).
        if (!IsAxisAligned(rel))
        {
            slot = _transformTable.AcquireSlot(component.RenderId);
            _transformTable.SetMatrix(device, slot, World(component));
            MarkNodeNotAware(component);
            return Matrix4x4F.Identity;
        }

        slot = _transformTable.AcquireSlot(node.RenderId);
        if (!_nodeRefreshed.TryGetValue(node, out var v) || v != _walkVersion)
        {
            _nodeRefreshed[node] = _walkVersion;
            _transformTable.SetMatrix(device, slot, World(node));
        }
        _nodeAllAware.TryAdd(node.RenderId, true);
        return rel;
    }

    // A unit under a motion node drew a path its slot matrix can't move (world-baked text, per-unit, gradient for now) ->
    // the node loses the slot-write fast path this frame (recorded per walk).
    private void MarkNodeNotAware(IUIComponent component)
    {
        var node = NodeOf(component);
        if (node != null) _nodeAllAware[node.RenderId] = false;
    }

    // Apply the moved nodes' new matrices (64B each) before a replay-based draw; stale position memos drop and rebuild
    // lazily O(dirty). Returns false when ANY moved node has non-aware retained content - the caller full-walks.
    private bool RefreshMovedNodes(IGraphicsDevice device)
    {
        if (_movedNodesBuf.Count == 0) return true;
        foreach (var node in _movedNodesBuf)
            if (!_nodeAllAware.GetValueOrDefault(node.RenderId, false))
                return false;
        // Positions moved -> drop the WORLD memos (rebuilt lazily from _snap). NOT _snap: the recorder already re-captured
        // the moved nodes' fresh LocalTransform this frame (CaptureSnapshot). NOT the clip memo either: a clip is the
        // ClipToBounds ancestors' viewport (a scroll viewport, a panel), which a node moving INSIDE it never changes, and
        // recomputing it from live ancestor Bounds mid-relayout produced a transiently-wrong viewport that CULLED on-screen
        // tiles for one frame (the hover "empty cell"). A move that DOES change a viewport (resize/maximize) is structural
        // -> a full walk, which clears every memo.
        _worldCache.Clear();
        _relWorldCache.Clear();
        foreach (var node in _movedNodesBuf)
            if (_transformTable.TryGetSlot(node.RenderId, out var slot))
                _transformTable.SetMatrix(device, slot, World(node));
        _movedNodesBuf.Clear();
        return true;
    }

    private Rect? CumulativeClip(IUIComponent c)
    {
        if (c == null) return null;
        if (_clipCache.TryGetValue(c, out var cached)) return cached;
        var s = ApplySnap(c);
        // An adorner skips its TARGET's own clip and, above it, obeys only the VIEWPORTS - see ClippedByRenderParent.
        var parentClip = c.ClippedByRenderParent || s.RenderParent == null
            ? CumulativeClip(s.RenderParent)
            : AdornerClip(ApplySnap(s.RenderParent).RenderParent, c);
        var result = parentClip;
        if (s.ClipToBounds)
        {
            var rect = new Rect(0, 0, s.RenderSize.Width, s.RenderSize.Height).TransformToAABB(World(c));
            result = parentClip is { } p ? p.Intersect(rect) : rect;
        }
        _clipCache[c] = result;
        return result;
    }

    /// <summary>The clip an ADORNER inherits from above its target: only the ancestors that call themselves viewports
    /// (<see cref="IUIComponent.ClipsAdorners"/>), never the ordinary ClipToBounds boxes on the way. Those boxes exist
    /// to keep CONTENT inside them, and shaving the focus ring on each one left it unusable with any standoff - every
    /// card, tab strip and docking panel took a bite. Not memoized: it walks per adorner, and adorners are counted in
    /// ones per frame (the ring, a hover cue) rather than in thousands like content.</summary>
    private Rect? AdornerClip(IUIComponent node, IUIComponent adorner)
    {
        // The viewport is widened by what the adorner is entitled to draw outside its target: otherwise a control
        // standing flush against the edge of a scroll area wears a shaved ring, which is exactly what the standoff
        // exists to avoid. Bounded by the standoff itself, so a row scrolled further than that is still cut.
        var standoff = (adorner as Controls.Adorners.Adorner)?.ClipStandoff ?? 0;

        Rect? result = null;
        for (var n = node; n != null; n = ApplySnap(n).RenderParent)
        {
            var s = ApplySnap(n);
            if (!s.ClipToBounds || !n.ClipsAdorners) continue;

            var rect = new Rect(0, 0, s.RenderSize.Width, s.RenderSize.Height).TransformToAABB(World(n));
            if (standoff > 0)
            {
                rect = new Rect(rect.X - standoff, rect.Y - standoff,
                    rect.Width + standoff * 2, rect.Height + standoff * 2);
            }

            result = result is { } r ? r.Intersect(rect) : rect;
        }

        return result;
    }
}


