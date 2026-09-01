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
    private readonly Dictionary<IUIComponent, int> _opacitySlotCache = new();   // memo of OpacitySlotOf, same lifetime

    // Set when a fade slot is handed out for the first time - the instances beneath it still carry the old index, so the
    // frame has to walk instead of patch. Cleared by the walk that acts on it.
    private bool _fadeSlotJustCreated;

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
    // ...and WHO they are. A "no" used to be a bare bool, so a node that could not move all of its content by writing
    // one matrix surrendered the whole frame to the walk - and one turned label inside a sliding view is enough for
    // that (measured on the stand: "ViewboxView <- Border", every frame of every slide involving that tab). Knowing
    // the stragglers by name turns the refusal into a carry: the node moves its subtree by its slot, and the few units
    // that hold their own full world are re-baked beside it, exactly as an ordinary mover's subtree is.
    private readonly Dictionary<Guid, HashSet<IUIComponent>> _nodeStragglers = new();
    // APPLIER-owned: the moved nodes of the packets drained for THIS draw (it rewrites their table matrices, then clears).
    // The recorder must not read it - it takes the frame's moved nodes off RenderDirty into packet.MovedNodes.
    private readonly List<IUIComponent> _movedNodesBuf = new();
    // APPLIER-owned: this packet's movers whose change of PLACE the retained stream survives.
    private readonly HashSet<IUIComponent> _forgivenMoves = new();
    // ...and the subset whose change of SIZE it survives too - the ones with no clip under them to change shape.
    private readonly HashSet<IUIComponent> _forgivenResize = new();
    // The nodes whose matrices THIS frame rewrote - the replay re-points the per-unit draws under them.
    private readonly HashSet<IUIComponent> _movedNodeOwners = new();
    // Motion nodes that moved because a node ABOVE them did - they carry their own world, so they need writing too.
    private readonly List<IUIComponent> _nestedMovedNodes = new();
    // APPLIER-owned: the ORDINARY movers of the packets drained for this draw (RefreshMovedComponents writes their
    // subtrees' slots, then clears). Filled only for movers the applier forgave.
    private readonly List<IUIComponent> _movedOwnersBuf = new();
    // The components THIS frame re-baked for a move - the replay re-points the per-unit draws among them.
    private readonly HashSet<IUIComponent> _movedOwners = new();
    private readonly List<IUIComponent> _movedSubtree = new();   // ...in visit order, so the re-bake is one flat pass
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
        // Letters that finished rasterizing since the last build have to be taken into THIS cache's units first. An
        // overlay never runs ApplyFrame - this method fuses record and apply - so without the call here the adoption
        // only ever happened for the window's content cache, and a popup's text kept the empty run it was first built
        // with. Rebuilding alone does NOT fix that: the run lives on the text unit and is re-frozen by the adoption,
        // not by the walk. Together with the gate now asking about arrivals, this is what puts a late glyph on screen.
        AdoptReadyGlyphs();

        // A FULL rebuild every call. Must record LastBuildKind=Full: the batches' Clean-frame upload-skip reads it, else
        // the overlay batch skips every GPU upload - its SSBO never fills and the whole overlay renders nothing.
        LastBuildKind = RenderBuildKind.Full;
        _commands.Clear();
        ClearOrder();
        _snap.Clear();         // full rebuild -> drop last frame's frozen layout snapshot (else stale overlay positions + unbounded _snap growth)
        _worldCache.Clear();   // new frame: drop last frame's transform + clip memos
        _clipCache.Clear();
        _clipSlotCache.Clear();
        _clipShapeCache.Clear();
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
                ProcessRenderCommands(component, _drawingContextInternal.GetDrawCommands(), projectionMatrix, !readOnly && wasGeometryValid, order, component.RenderClones);
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
    /// <summary>Release every GPU resource this cache owns - the batch rings and the transform table - for a window that
    /// is going away. Separate from <see cref="DisposeUnits"/> on purpose: that one drops the units and is called on
    /// every designer re-render, where the collectors should stay.</summary>
    public void DisposeDeviceResources()
    {
        var device = _renderUnitFactory?.GraphicsDevice;
        if (device == null) return;

        _rectBatch?.DisposeGpuResources(device);
        _ellipseBatch?.DisposeGpuResources(device);
        _polygonBatch?.DisposeGpuResources(device);
        _textBatch?.DisposeGpuResources(device);
        _gradientRectBatch?.DisposeGpuResources(device);
        _gradientEllipseBatch?.DisposeGpuResources(device);
        _patternBatch?.DisposeGpuResources(device);
        _fractalBatch?.DisposeGpuResources(device);
        _texRectBatch?.DisposeGpuResources(device);
        _materialBatch?.DisposeGpuResources(device);
        _haloUnder?.DisposeGpuResources(device);
        _haloOver?.DisposeGpuResources(device);
        _haloLivingUnder?.DisposeGpuResources(device);
        _haloLivingOver?.DisposeGpuResources(device);
        _instancedFill?.Dispose();
        _transformTable?.Dispose();

        _rectBatch = null;
        _ellipseBatch = null;
        _polygonBatch = null;
        _textBatch = null;
        _gradientRectBatch = null;
        _gradientEllipseBatch = null;
        _patternBatch = null;
        _fractalBatch = null;
        _texRectBatch = null;
        _materialBatch = null;
        _haloUnder = null;
        _haloOver = null;
        _haloLivingUnder = null;
        _haloLivingOver = null;
        _instancedFill = null;
        _transformTable = null;
    }

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

    /// <summary>Test hook: snapshot entries actually handed to the applier. It is the number a retained frame lives or
    /// dies by - the applier reads ANY entry as "the layout moved under the recorded stream" and refuses to replay it,
    /// so an idle frame publishing entries costs the whole scene its retained path (measured: 28 fps against ~320).</summary>
    internal static long SnapshotEntriesPublished { get; private set; }

    private void PublishSnapshot(IUIComponent component, LayoutSnapshot snapshot)
    {
        SnapshotEntriesPublished++;
        Core.Diagnostics.RuntimeStats.LastSnapPublished++;
        _packet.SnapDelta.Add(new KeyValuePair<IUIComponent, LayoutSnapshot>(component, snapshot));
    }

    // Only a CLIP has rounded corners worth freezing - everything else paints its own and needs nothing here.
    private static Vector4F ClipRadiiOf(IUIComponent c) => c.ClipToBounds ? c.ClipRadii : Vector4F.Zero;

    // Record one component's frozen layout into the recorder's map AND this frame's delta (the applier's replica is built
    // from nothing else). Memoised: an unchanged component is captured once and never re-sent.
    private LayoutSnapshot Snap(IUIComponent c)
    {
        if (_snap.TryGetValue(c, out var s)) return s;
        s = new LayoutSnapshot(c.LocalTransform, c.RenderSize, c.ClipToBounds, c.IsRenderMotionNode, c.RenderParent,
            (float)c.Opacity, (float)c.SelfOpacity, ClipRadiiOf(c));
        _snap[c] = s;
        PublishSnapshot(c, s);
        return s;
    }

    // APPLIER's view: its private replica, folded from the packets' deltas. A miss (impossible for anything drawn) falls
    // back to the live component - the ONE live read left on this side, unreachable in the recorded paths.
    private LayoutSnapshot ApplySnap(IUIComponent c)
    {
        if (_applySnap.TryGetValue(c, out var s)) return s;
        s = new LayoutSnapshot(c.LocalTransform, c.RenderSize, c.ClipToBounds, c.IsRenderMotionNode, c.RenderParent,
            (float)c.Opacity, (float)c.SelfOpacity, ClipRadiiOf(c));

        // ...but a part the teardown DESTROYED is not cached. This miss-fallback is the third way into the map and the
        // one that kept re-adding what the sweep had just removed: 39 dead controls a swap survived both a sweep taking
        // 762 out and a guard on the packet path. Answer the caller, hold nothing.
        if (c is Core.FundamentalUIComponent { IsDiscarded: true }) return s;

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
        _refreshedThisCapture.Clear();

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
        var opacityStart = System.Diagnostics.Stopwatch.GetTimestamp();
        Dirty.SnapshotPaintInto(_opacityCheckBuf);
        foreach (var c in _opacityCheckBuf)
            if (IsDrawn(c) && (!_snap.TryGetValue(c, out var f) || f.Opacity != (float)c.Opacity || f.SelfOpacity != (float)c.SelfOpacity))
                RefreshSnapshot(c);
        Core.Diagnostics.RuntimeStats.LastSnapOpacityMs = System.Diagnostics.Stopwatch.GetElapsedTime(opacityStart).TotalMilliseconds;

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
        var drawsStart = System.Diagnostics.Stopwatch.GetTimestamp();
        foreach (var draw in _packet.Draws) RefreshSnapshot(draw.Component);
        Core.Diagnostics.RuntimeStats.LastSnapDrawsMs = System.Diagnostics.Stopwatch.GetElapsedTime(drawsStart).TotalMilliseconds;
        var dirtyStart = System.Diagnostics.Stopwatch.GetTimestamp();

        // EVERY geometry-dirty component, not just re-recorded ones: a component can change SIZE without its recorded
        // geometry going stale (RenderSize marks it dirty but leaves IsGeometryValid true -> the record SKIPS it), and a
        // snapshot only re-frozen for what was recorded keeps drawing it at the old size (grid gaps/overlaps). Only what is
        // actually DRAWN: a component that left the drawn set had its entry dropped on purpose (re-frozen when it returns);
        // re-adding it would resurrect it and publish a delta for something the applier just freed.
        foreach (var component in _geometryDirtyBuffer)
        {
            // Cheapest question first: IsDrawn walks the ancestor chain, and most of this set was already re-frozen by
            // the packet's draws just above.
            if (_refreshedThisCapture.Contains(component)) continue;
            if (IsDrawn(component)) RefreshSnapshot(component);
        }
        Core.Diagnostics.RuntimeStats.LastSnapDirtyMs = System.Diagnostics.Stopwatch.GetElapsedTime(dirtyStart).TotalMilliseconds;

        var tailStart = System.Diagnostics.Stopwatch.GetTimestamp();

        // A component that kept its units but MOVED (VisualParent changed) - nothing else would re-freeze it.
        foreach (var rerank in _packet.Reranks) RefreshSnapshot(rerank.Key);
        // Moved this frame. A stale moved-MOTION-NODE entry is the classic O(1)-path regression: World composes it from
        // LAST frame's LocalTransform, so a tilting tile never moves and a flip sticks at its old angle (the angle lives
        // only here). Read off THIS frame's packet: _movedNodesBuf is the APPLIER's copy.
        foreach (var node in _packet.MovedNodes) RefreshSnapshot(node);
        foreach (var moved in _movedBuf) RefreshSnapshot(moved);
        Core.Diagnostics.RuntimeStats.LastSnapTailMs = System.Diagnostics.Stopwatch.GetElapsedTime(tailStart).TotalMilliseconds;
    }

    // Re-freeze ONE component's entry (it changed this frame, so the ContainsKey early-out must NOT keep the old one), then
    // walk its ancestor chain lazily - those didn't change unless they are themselves in a changed set.
    // The sets CaptureSnapshot re-freezes from OVERLAP heavily - a resized tile is in the packet's draws, in the
    // geometry-dirty set AND in the moved set, so it was re-frozen three times. The repeats are pure waste: nothing
    // between them can change the component (CaptureSnapshot only reads), so the second computation always produced the
    // value the first one stored and published nothing. Measured at 10ms of a 26ms freeze on a 4K tile drag.
    private readonly HashSet<IUIComponent> _refreshedThisCapture = new();

    private void RefreshSnapshot(IUIComponent component)
    {
        if (component == null || !_refreshedThisCapture.Add(component)) return;
        var snapshot = new LayoutSnapshot(component.LocalTransform, component.RenderSize, component.ClipToBounds,
            component.IsRenderMotionNode, component.RenderParent, (float)component.Opacity, (float)component.SelfOpacity,
            ClipRadiiOf(component));

        // ...AND publish it: the delta is the ONLY source of the applier's replica, so updating the recorder's map alone
        // leaves the applier composing from the PREVIOUS transform (a tilting tile never moves). Snap() below publishes on
        // its own only for a NEW entry, so this force-refresh (overwriting an existing one) must publish explicitly.
        // But ONLY when it really changed. A dirty mark says "look at me", not "I moved": a component can be re-frozen
        // with the same transform, size and clip it already had, and republishing that is not free - the applier reads
        // ANY delta entry as "the layout moved under the recorded stream" and refuses the clean-frame replay for the whole
        // scene. Measured on the 60k view: ~15 unchanged entries per frame, two thirds of frames falling back to the full
        // walk, 35 ms a frame instead of a replay - 28 fps where the retained path gives hundreds.
        if (!_snap.TryGetValue(component, out var previous) || !previous.Equals(snapshot))
        {
            _snap[component] = snapshot;
            PublishSnapshot(component, snapshot);
        }

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
        _applySnap[element] = new LayoutSnapshot(Matrix4x4F.Identity, s.RenderSize, s.ClipToBounds, false, null,
            s.Opacity, s.SelfOpacity, s.ClipRadii);
        _worldCache.Clear();    // drop any absolute transforms/clips memoised during the build so ProcessCommands recomputes rebased
        _relWorldCache.Clear();
        _clipCache.Clear();
        _clipSlotCache.Clear();
        _clipShapeCache.Clear();
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

    // WHERE this element's alpha comes from at draw time: the opacity slot of the nearest ancestor-or-self that fades,
    // whose own record composes with the next one up. The instance carries this INDEX and nothing else about opacity, so
    // a fade is a handful of float writes and the subtree under it is never re-baked.
    //
    // A slot already handed out is KEPT even while its element is fully opaque: dropping it at 1.0 would relink every
    // descendant instance the moment an animation passed through opaque - a structural change, i.e. exactly the re-bake
    // this exists to avoid - and animations pass through 1.0 constantly.
    private int OpacitySlotOf(IGraphicsDevice device, IUIComponent c)
    {
        if (c == null || _transformTable == null) return -1;
        if (_opacitySlotCache.TryGetValue(c, out var cached)) return cached;

        var s = ApplySnap(c);
        var parent = s.RenderParent != null ? OpacitySlotOf(device, s.RenderParent) : -1;

        int slot;
        var had = _transformTable.TryGetOpacitySlot(c.RenderId, out _);
        if (!had && s.Opacity >= 1f)
        {
            slot = parent;   // draws at its parent's alpha - no link of its own
        }
        else
        {
            // A slot that did not exist a moment ago is a STRUCTURAL event: every instance under this element carries
            // the index it read on the last walk, which said "nothing above me fades". They have to be re-baked once to
            // pick the new index up, and the frame that creates the slot is the only one that knows it happened.
            // Asked here rather than guessed on the UI side ("was it opaque before?"), which misses an element that was
            // already translucent or an animation that does not start at 1.
            if (!had) _fadeSlotJustCreated = true;

            slot = _transformTable.AcquireOpacitySlot(c.RenderId);
            _transformTable.SetAlpha(device, slot, s.Opacity);
            _transformTable.SetOpacityParent(device, slot, parent);

            // Just allocated, and the buffer this frame draws from was sized BEFORE that: an instance carrying this
            // index would have the shader read past the allocation. Draw at the parent's alpha for one frame; the next
            // walk finds the slot live.
            if (!_transformTable.IsSlotLive(slot)) slot = parent;
        }

        _opacitySlotCache[c] = slot;
        return slot;
    }

    // This unit's family READS the element's alpha from its opacity slot, so its colour must not carry the chain as
    // well - or the fade lands twice and the element comes out too dark. Called by those branches only; everything else
    // keeps the chain in its colour, because its shader pass cannot reach the table on this driver (see GlyphItem).
    private void FadeBySlot(Core.Graphics.IRenderUnit u)
    {
        if (u.Component == null) return;

        u.SetEffectiveOpacity(ApplySnap(u.Component).SelfOpacity);
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
    /// <summary>Where a unit's geometry is baked, and which transform-table slot carries the rest.
    /// <para>A CLONE (§4o) folds its offset into the BAKE, never into the slot. Its own slot holding an absolute world
    /// was the first attempt, and it broke the scroll: content moves by rewriting ONE node matrix without re-walking, so
    /// clone slots kept the world they were baked with and drifted a frame or two behind the tiles - the band along the
    /// realize frontier, wider the faster the scroll. In the bake the offset is per-INSTANCE (each clone has its own
    /// bounds anyway), so clones ride the node's slot exactly as the tiles do and one matrix write moves both.</para></summary>
    private Matrix4x4F ResolveBake(IGraphicsDevice device, IUIComponent component, Matrix4x4F world, out int slot)
    {
        var bake = ResolveBakeCore(device, component, world, out slot);
        return _cloneMatrix.HasValue ? bake * _cloneMatrix.Value : bake;
    }

    private Matrix4x4F ResolveBakeCore(IGraphicsDevice device, IUIComponent component, Matrix4x4F world, out int slot)
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

            // ...and every node ABOVE it carries this content too, through this one: an outer node's move takes this
            // node's slot with it. Without saying so, a view whose children are all nodes (a sliding view around a
            // scrolling list) has no unit of its own to vouch for it and refuses every move. TryAdd, never assignment:
            // a "no" recorded by MarkNodeNotAware - which now travels up the same chain - outranks it.
            for (var up = NodeOf(ApplySnap(node).RenderParent); up != null; up = NodeOf(ApplySnap(up).RenderParent))
                _nodeAllAware.TryAdd(up.RenderId, true);
        }
        _nodeAllAware.TryAdd(node.RenderId, true);
        return rel;
    }

    // Does writing this node's matrix move everything that rides it? A unit answers against its NEAREST node, and BOTH
    // answers travel UP the chain of nodes: a yes because an outer node moves this one's slot with it, a no because
    // whatever cannot follow cannot follow any of them. They must be symmetric - vouching upward while refusing only at
    // the nearest node is what put a shape's aura in the top-left corner: its own node said no, the sliding view above
    // it still said yes, and the frame patched with that one unit left behind.
    //
    // A node nobody answered for is a REFUSAL. Answering yes by default was tried and let a patch stand while content
    // had not reached the arena yet - the vector-icon page came up with one icon.
    private bool NodeCarriesItsContent(IUIComponent node) => _nodeAllAware.GetValueOrDefault(node.RenderId, false);

    // A unit under a motion node drew a path its slot matrix can't move (world-baked text, per-unit, gradient for now) ->
    // the node loses the slot-write fast path this frame (recorded per walk).
    private void MarkNodeNotAware(IUIComponent component)
    {
        var node = NodeOf(component);
        if (node == null) return;
        _nodeAllAware[node.RenderId] = false;
        NoteStraggler(node, component);

        // ...and every node ABOVE it: what this unit cannot follow, it cannot follow for any of them. Assignment, not
        // TryAdd - a "no" outranks the yes an inner node vouched with (see NodeCarriesItsContent).
        for (var up = NodeOf(ApplySnap(node).RenderParent); up != null; up = NodeOf(ApplySnap(up).RenderParent))
        {
            _nodeAllAware[up.RenderId] = false;
            NoteStraggler(up, component);
        }

        Core.Diagnostics.FrameTrace.NoteNotAware(node.GetType().Name + " <- " + component.GetType().Name);
    }

    private void NoteStraggler(IUIComponent node, IUIComponent component)
    {
        if (!_nodeStragglers.TryGetValue(node.RenderId, out var behind))
            _nodeStragglers[node.RenderId] = behind = new HashSet<IUIComponent>();
        behind.Add(component);
    }

    // Can this moved node be carried at all? Asked BEFORE anything is written, so a "no" costs the frame a walk and
    // never a half-updated table. A node that carries everything needs nothing; one that does not has to know its
    // stragglers by name (a node NOBODY answered for still refuses - see NodeCarriesItsContent) and every straggler's
    // batched units have to be re-bakeable in place.
    private bool CanCarryStragglers(IUIComponent node)
    {
        if (NodeCarriesItsContent(node)) return true;
        if (!_nodeStragglers.TryGetValue(node.RenderId, out var behind)) return false;

        foreach (var c in behind)
        {
            if (!_groupById.TryGetValue(c.RenderId, out var g)) continue;
            foreach (var u in g.Units)
                if (Drawing(u) && HoldsInstances(u) && !IsSlotPatchable(u)) return false;
        }
        return true;
    }

    // Carry them. A straggler holds its OWN slot with its FULL world (that is why it could not ride the node's), so
    // ResolveBake rewriting that slot is what moves it - and for a per-unit draw that is the whole job, since the
    // replay re-points those under a moved node (RepointIfItMoved). One that holds instances is re-baked too.
    private bool CarryStragglers(IGraphicsDevice device, IUIComponent node)
    {
        if (NodeCarriesItsContent(node) || !_nodeStragglers.TryGetValue(node.RenderId, out var behind)) return true;

        foreach (var c in behind)
        {
            if (!_groupById.TryGetValue(c.RenderId, out var g)) continue;
            foreach (var u in g.Units)
            {
                if (!Drawing(u)) continue;
                var bakeWorld = ResolveBake(device, u.Component, World(u.Component), out var slot);
                if (HoldsInstances(u) && !PatchSlot(device, u, bakeWorld, slot)) return false;
            }
        }
        return true;
    }

    // Apply the moved nodes' new matrices (64B each) before a replay-based draw; stale position memos drop and rebuild
    // lazily O(dirty). Returns false when ANY moved node has non-aware retained content - the caller full-walks.
    private bool RefreshMovedNodes(IGraphicsDevice device)
    {
        if (_movedNodesBuf.Count == 0)
        {
            _movedNodeOwners.Clear();   // nothing moved this frame - the replay re-points nothing
            return true;
        }
        // NESTED nodes: a node's slot holds its OWN world, so one that moved only because an ANCESTOR node did has to be
        // written too - nothing else writes it, and its whole subtree would stay behind (a list inside a view that
        // slides). Nodes are counted in ones per window, so this is a short list against an ancestor walk rather than a
        // scan of anything.
        _nestedMovedNodes.Clear();
        foreach (var known in _nodeRefreshed.Keys)
            if (!_movedNodesBuf.Contains(known) && IsUnder(known, _movedNodesBuf)) _nestedMovedNodes.Add(known);

        foreach (var node in _movedNodesBuf) if (!CanCarryStragglers(node)) return false;
        foreach (var node in _nestedMovedNodes) if (!CanCarryStragglers(node)) return false;
        // Positions moved -> drop the WORLD memos (rebuilt lazily from _snap). NOT _snap: the recorder already re-captured
        // the moved nodes' fresh LocalTransform this frame (CaptureSnapshot). NOT the clip memo either: a clip is the
        // ClipToBounds ancestors' viewport (a scroll viewport, a panel), which a node moving INSIDE it never changes, and
        // recomputing it from live ancestor Bounds mid-relayout produced a transiently-wrong viewport that CULLED on-screen
        // tiles for one frame (the hover "empty cell"). A move that DOES change a viewport (resize/maximize) is structural
        // -> a full walk, which clears every memo.
        _worldCache.Clear();
        _relWorldCache.Clear();
        _movedNodeOwners.Clear();
        foreach (var node in _movedNodesBuf)
        {
            if (_transformTable.TryGetSlot(node.RenderId, out var slot))
                _transformTable.SetMatrix(device, slot, World(node));
            _movedNodeOwners.Add(node);   // the replay re-points the per-unit draws under them
        }
        foreach (var node in _nestedMovedNodes)
        {
            if (_transformTable.TryGetSlot(node.RenderId, out var slot))
                _transformTable.SetMatrix(device, slot, World(node));
            _movedNodeOwners.Add(node);
        }

        // ...and last, whatever could not ride those slots - AFTER the memo flush above, so every straggler is re-baked
        // from the world the node has NOW and not the one it was memoized at.
        foreach (var node in _movedNodesBuf) if (!CarryStragglers(device, node)) return false;
        foreach (var node in _nestedMovedNodes) if (!CarryStragglers(device, node)) return false;

        _movedNodesBuf.Clear();
        return true;
    }

    // The same thing for an ORDINARY mover - an element that changed place without being a motion node (a slider thumb,
    // a drop gap opening, a re-arranged row). A node moves its whole subtree by rewriting ONE matrix; an ordinary mover
    // cannot, because nothing about its subtree is expressed relative to it. So its move is carried the way a dirty
    // element's change is carried: the subtree's drawn units are RE-BAKED, exactly as TryPartialReplay re-bakes the
    // dirty ones, through the same ResolveBake + PatchSlot pair.
    //
    // That is the whole point: O(moved subtree), where forbidding the patch is O(scene). Dragging one thumb over a 22k
    // node tab was a full 55 ms walk EVERY frame - 16 fps - because the transform-dirty flag is frame-wide and one
    // element moving spoke for the whole frame.
    //
    // Re-baking rather than writing slots is what makes it work under a MOTION NODE, and that distinction is not
    // academic: writing slots alone moved a drop gap's labels (per-unit draws, re-pointed at replay) while leaving its
    // TILES behind, because a tile inside a scrolling panel is batched NODE-RELATIVE - its place is in the instance,
    // not in a slot of its own. ResolveBake answers both cases; a slot write answers only one.
    //
    // Forgiveness is decided by the applier (nothing under the mover may clip: a recorded Scissor is a world-space rect
    // and nothing re-derives it). False = something in a moved subtree cannot be re-baked in place, and the caller
    // hands the frame to the walk.
    // The window scissor of the frame being drawn: the cull test in the collect needs it, and the collect sits a call
    // below the paths that hold it.
    private Adamantium.Vulkan.Core.Rect2D _cullScissor;

    // This frame's full scissor. The WALK is handed it as an argument, but a patch is not - and a patch re-bakes records
    // that carry a clip slot, so it has to be able to ask for one too.
    private Adamantium.Vulkan.Core.Rect2D _frameScissor;

    private bool RefreshMovedComponents(IGraphicsDevice device, Adamantium.Vulkan.Core.Rect2D fullScissor)
    {
        _cullScissor = fullScissor;

        if (_movedOwnersBuf.Count == 0)
        {
            _movedOwners.Clear();   // nothing moved this frame - the replay re-points nothing
            return true;
        }

        // Validate the WHOLE set before touching anything, as the dirty loop does: a refusal must cost the frame
        // nothing but the walk it was going to take anyway.
        _movedOwners.Clear();
        _movedSubtree.Clear();
        foreach (var mover in _movedOwnersBuf)
        {
            if (CollectMovedSubtree(mover)) continue;
            _movedOwners.Clear();
            _movedSubtree.Clear();
            return false;
        }

        // Positions moved -> the composed world memos are stale (same reasoning as RefreshMovedNodes; the clip memo is
        // deliberately kept - a mover that changes a viewport is structural).
        _worldCache.Clear();
        _relWorldCache.Clear();

        foreach (var c in _movedSubtree)
        {
            if (!_groupById.TryGetValue(c.RenderId, out var g)) continue;
            foreach (var u in g.Units)
            {
                if (!HoldsInstances(u)) continue;   // a per-unit draw - the replay re-points it (RepointIfItMoved)
                var bakeWorld = ResolveBake(device, u.Component, World(u.Component), out var slot);
                if (!PatchSlot(device, u, bakeWorld, slot)) return false;
            }
        }

        _movedOwnersBuf.Clear();
        return true;
    }

    // Everything in a moved subtree, collected once. The visited set doubles as the guard against re-walking: a
    // container and its children can BOTH be named movers (a panel re-arranging its rows), and without it overlapping
    // subtrees would cost the frame O(n^2).
    private bool CollectMovedSubtree(IUIComponent c)
    {
        if (c == null || !_movedOwners.Add(c)) return true;

        if (_groupById.TryGetValue(c.RenderId, out var g))
        {
            foreach (var u in g.Units)
            {
                if (!Drawing(u)) continue;

                // CULLED = no op in the stream at all: a unit entirely outside its clip is never recorded. Moving it can
                // bring it back INSIDE, and a patch cannot add a draw that was never written. This is what emptied the
                // vector-icon page - its ScrollViewer culls everything below the fold, and once a scroll patched instead
                // of walking there was nothing left to bring those icons in. Measured: clip 54,168,1172,498 against
                // icons at y=716..1164, and the page came right only when a 4K window made the viewport tall enough to
                // cull nothing. Asked of the CURRENT world, so it catches both directions.
                ResolveScissor(u.Component, World(u.Component), _cullScissor, out _, out var culled);
                if (culled) return false;

                if (HoldsInstances(u) && !IsSlotPatchable(u)) return false;
            }
        }

        _movedSubtree.Add(c);
        foreach (var child in c.VisualChildren)
            if (!CollectMovedSubtree(child)) return false;

        return true;
    }

    // The clip slot a unit under this component must read, or -1. Memoised per frame like CumulativeClip, and for the
    // same reason: every unit under one clip asks the same question.
    private readonly Dictionary<IUIComponent, int> _clipSlotCache = new();

    // The same clip as a SHAPE, for the draws that cannot read the table by slot - filled by the walk below, in the one
    // place that already computes it, so the two can never disagree. See RenderData.RoundedClipBox.
    private readonly Dictionary<IUIComponent, (Vector4F Box, Vector4F Radii)> _clipShapeCache = new();

    // The clip OWNERS whose slots exist - kept across frames, unlike the cache above. A clip that changes shape (a radius
    // animating, the container resizing) must reach the screen on a REPLAYED frame too, and a replay re-records nothing:
    // the slot is the only thing that can carry it, so its contents are refreshed per frame from here.
    private readonly Dictionary<IUIComponent, int> _clipOwners = new();

    /// <summary>Rewrite every live clip slot from its owner's current shape. One 32-byte write per clip, and it is what
    /// makes a rounded clip follow a resize or an animated radius without the frame being re-recorded.</summary>
    private void RefreshClipSlots(Vulkan.Core.Rect2D fullScissor)
    {
        if (_clipOwners.Count == 0 || _transformTable == null) return;

        foreach (var (owner, slot) in _clipOwners)
        {
            var s = ApplySnap(owner);
            if (!s.ClipToBounds || s.ClipRadii == Vector4F.Zero) continue;

            var box = ToFramebufferScissor(new Rect(0, 0, s.RenderSize.Width, s.RenderSize.Height)
                .TransformToAABB(World(owner)), fullScissor);
            _transformTable.SetClip(null, slot,
                new Vector4F(box.Offset.X, box.Offset.Y, box.Extent.Width, box.Extent.Height),
                s.ClipRadii * (float)_renderScale);
        }
    }

    /// <summary>
    /// The NEAREST rounded clip above this component, as a slot index the shaders can read.
    ///
    /// <para>One, not all of them. Intersecting two rounded clips is a max() of two distance fields - a second slot and a
    /// second fetch in every pixel shader - for a case that barely occurs: a rounded box inside another rounded box,
    /// both clipping, both cutting the same corner. The remaining ancestors keep clipping rectangularly through the
    /// scissor, which is where the bulk of the cut happens anyway.</para>
    /// </summary>
    private int RoundedClipSlot(IUIComponent c, Vulkan.Core.Rect2D fullScissor)
    {
        if (c == null || _transformTable == null) return -1;
        if (_clipSlotCache.TryGetValue(c, out var cached)) return cached;

        var slot = -1;
        for (var owner = c; owner != null; owner = ApplySnap(owner).RenderParent)
        {
            var s = ApplySnap(owner);
            if (!s.ClipToBounds || s.ClipRadii == Vector4F.Zero) continue;

            var box = ToFramebufferScissor(new Rect(0, 0, s.RenderSize.Width, s.RenderSize.Height)
                .TransformToAABB(World(owner)), fullScissor);

            var boxVec = new Vector4F(box.Offset.X, box.Offset.Y, box.Extent.Width, box.Extent.Height);
            var radiiVec = s.ClipRadii * (float)_renderScale;
            slot = _transformTable.AcquireClipSlot(owner.RenderId);
            _transformTable.SetClip(null, slot, boxVec, radiiVec);
            _clipShapeCache[c] = (boxVec, radiiVec);
            _clipOwners[owner] = slot;   // so a replayed frame can refresh it - see RefreshClipSlots
            break;
        }

        // A slot the shader cannot reach yet (the table grew past this frame's buffer) would be indexed out of the
        // allocation, and this device answers that with a lost device rather than a wrong pixel.
        if (slot >= 0 && !_transformTable.IsSlotLive(slot)) slot = -1;

        _clipSlotCache[c] = slot;
        return slot;
    }

    /// <summary>The rounded clip as a SHAPE (device px), for a per-unit draw that takes it as a uniform instead of
    /// reading the table by slot. Zero size = no clip. Asks the slot walk above so both answers come from one place.
    /// A slot the shader could not reach also answers "no clip" here, exactly as it does there.</summary>
    private (Vector4F Box, Vector4F Radii) RoundedClipShape(IUIComponent c, Vulkan.Core.Rect2D fullScissor)
    {
        var slot = RoundedClipSlot(c, fullScissor);
        return slot >= 0 && _clipShapeCache.TryGetValue(c, out var shape) ? shape : default;
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


