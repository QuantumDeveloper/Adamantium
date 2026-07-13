using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.UI.Rendering.Retained;
using Adamantium.UI.Rendering.RenderUnits;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

public class RenderCache
{
    private readonly List<DrawCommand> _commands = new();
    private IDrawingContext _drawingContext;
    private IDrawingContextInternal _drawingContextInternal;

    // ONE logical control's contiguous run in the paint order. The retained scene is a list of GROUPS (paint order),
    // not a flat unit list: a control whose recorded output changes - even its unit COUNT (a hover background appearing
    // 0->1, a per-frame chart re-recording a different number of segments) - mutates only ITS group's Units list; no
    // other group's units move. That makes a dirty control's update O(that control), never O(scene), and it is the
    // substrate for per-group batch-slot ranges and per-group op-stream patching (the incremental-draw follow-ups).
    private sealed class ControlGroup
    {
        public Guid ControlId;
        public readonly List<IRenderUnit> Units = new();

        // Recorded by the last RECORDING walk, for the spliced-patch draw path: the group's contiguous retained
        // rect-batch slot runs, and whether EVERY drawn unit of the group landed in the rect batch (only such groups can
        // be patched by segment surgery - anything else falls back to the full walk). Rect-only first: the huge-grid /
        // live-chart cases are rect-batched; the other SDF collectors follow the same pattern later.
        public readonly List<(int First, int Count)> RectRuns = new();
        public bool PatchableRectOnly;
        public int WalkVersion = -1;   // which recording walk last described this group (distinguishes NEW groups)
    }

    private int _walkVersion;   // bumped per recording walk

    private ControlGroup _walkGroup;   // recording-walk group-boundary detector (resets each group's run records once)

    private readonly List<ControlGroup> _groups = new();              // the paint order (groups in DFS paint rank)
    private readonly Dictionary<Guid, ControlGroup> _groupById = new();   // control -> its group (the retained unit cache)

    // Paint-order (DFS visit) index of each component, assigned during the full walk - kept even for a component that
    // rendered 0 units. Lets a PARTIAL re-render whose draw-command COUNT changed (a hover background appearing 0->1, or
    // vanishing) splice that ONE component's units into the retained paint-order list at the right spot, instead of
    // falling back to a full tree walk that re-renders every element (the hover / mouse-move FPS cliff on a big list).
    // RECORDER-owned (derived from walking the LIVE tree). The applier needs the ranks too - to splice a new group at its
    // paint position - but must never read this one: in the decoupled path the recorder rebuilds it (a full walk) while the
    // applier is still drawing the previous frame. So each rebuild PUBLISHES a fresh immutable dictionary on the packet and
    // the applier keeps its own reference (_applyOrder). Rare (full walk / a component appearing mid-partial), so the copy
    // costs nothing per frame.
    private Dictionary<Guid, int> _orderByControl = new();
    private IReadOnlyDictionary<Guid, int> _applyOrder = new Dictionary<Guid, int>();   // APPLIER-owned view of the above
    private IRootVisualComponent _lastVisualRoot;             // for an on-demand order re-walk (a component appearing mid-partial)
    private readonly Stack<IUIComponent> _orderStack = new(); // reused by ReassignOrders (no per-call alloc)
    private readonly HashSet<Guid> _orderVisited = new();
    private readonly Stack<IUIComponent> _walkStack = new();      // reused by RecordFullWalk (it ran on every structural frame)
    private readonly HashSet<IUIComponent> _walkVisited = new();
    private bool _forceFullNextFrame;                         // the applier hit a state only a full RECORD can fix (see ApplyPacket)

    // RECORDER-side mirror of "which components hold retained units" - the applier's _groupById, but derived purely from the
    // recorder's OWN decisions: a component's recorded command COUNT is exactly the unit count the applier will hold for it,
    // and a dirty component that records nothing has its units dropped (ProcessRenderCommands). The recorder needs this to
    // decide Skip vs Fallback for a component that is no longer drawn - and it must NOT read _groupById for it: that
    // dictionary is applier-owned and mutated while the recorder runs (the render thread realizes frame N-1 while the loop
    // records frame N), so a plain Dictionary read there is a data race, not just a stale value.
    private readonly Dictionary<Guid, (IUIComponent Component, int Units)> _recordedUnits = new();
    private readonly List<Guid> _staleUnitIds = new();   // scratch: detached controls to drop from the mirror

    // Mirror one recorded draw. Empty commands from a CLEAN control mean "reuse the cached units" (mirror unchanged); from a
    // DIRTY one they mean "it now draws nothing" and the applier frees them - exactly the wasGeometryValid split the applier
    // makes in ProcessRenderCommands.
    private void MirrorUnits(IUIComponent component, int commandCount, bool wasGeometryValid)
    {
        if (commandCount > 0) _recordedUnits[component.RenderId] = (component, commandCount);
        else if (!wasGeometryValid) _recordedUnits.Remove(component.RenderId);
    }

    private bool HoldsUnits(IUIComponent component) =>
        _recordedUnits.TryGetValue(component.RenderId, out var entry) && entry.Units > 0;

    private readonly IRenderUnitFactory _renderUnitFactory;

    // Reusable snapshot of the geometry-dirty set for the partial pass: ReRenderInPlace re-renders components, and a
    // component's Render can mark MORE geometry dirty mid-pass - enumerating the live set then throws. Reused each build
    // (no per-frame allocation), same pattern as LayoutManager's promote buffer.
    private readonly List<IUIComponent> _geometryDirtyBuffer = new();

    // Draw-phase partial replay: a geometry-only partial re-renders the dirty components IN PLACE (build side), but the
    // DRAW used to re-walk EVERY unit to re-bake the batches (the O(N) 14ms at 4K - the hover FPS drop). Instead, a batched
    // rect records its slot during the full walk (_rectSlotByUnit); a fast-path partial then patches only the dirty tiles'
    // slots (UpdateSlot) and REPLAYS the op stream - O(dirty). Any doubt (spliced list, non-rect/unbatched dirty unit,
    // a tile that stopped being batchable) falls back to the full walk, so replay is a pure speedup, never a correctness risk.
    private readonly List<IUIComponent> _partialDirty = new();               // dirty components of the last fast-path partial
    private readonly Dictionary<IRenderUnit, int> _rectSlotByUnit = new();   // batched rect unit -> its slot in _rectBatch
    private bool _partialSpliced;                                            // last partial mutated the paint-order list -> ops/slots stale

    // Last render scale seen in ProcessCommands; maps a unit's window-logical clip rect to framebuffer-pixel scissor.
    private double _renderScale = 1.0;

    public RenderCache(IDrawingContext context, IRenderUnitFactory renderUnitFactory)
    {
        _drawingContext = context;
        _drawingContextInternal = (IDrawingContextInternal)context;
        _renderUnitFactory = renderUnitFactory;
    }
    
    /// <summary>How much the most recent <see cref="BuildFromVisualTree"/> did - diagnostics + the "skip proc" decision
    /// (only a <see cref="RenderBuildKind.Clean"/> frame skips the transform re-bake).</summary>
    public RenderBuildKind LastBuildKind { get; private set; }

    /// <summary>Did the last build actually MOVE anything (transform-dirty / a full re-layout)? A geometry-only partial
    /// re-records draw contents but nothing moved, so the per-unit transform re-bake (proc) is redundant - the draw pass
    /// re-bakes each drawn unit anyway - and can be skipped, which is the difference between a cheap and an O(N) frame
    /// while hovering a big list.</summary>
    public bool LastBuildTransformDirty { get; private set; }

    private bool _built;

    /// <summary>
    /// Brings the retained render scene up to date for this frame, doing only as much work as changed
    /// (docs/RENDER_CACHE_REDESIGN.md §4a/§4i):
    /// <list type="bullet">
    /// <item>fully clean -> re-draw last frame's units (~0 CPU);</item>
    /// <item>only moves/geometry changed (non-structural) -> re-render just the dirty components IN PLACE, each within
    /// its own retained <see cref="ControlGroup"/> (the paint order of groups is untouched);</item>
    /// <item>structural change (or first build, or a partial that turned out structural) -> a full tree walk.</item>
    /// </list>
    /// </summary>
    public void BuildFromVisualTree(IRootVisualComponent visualRoot)
    {
        RecordFrame(visualRoot);
        ApplyFrame();
    }

    /// <summary>
    /// RECORD half of the frame build (DEVICE-FREE - runs on the update thread). Decides the build kind (Clean / Partial /
    /// Full), runs component.Render, and fills <see cref="_packet"/> with the draws the applier realizes. Also captures the
    /// RenderDirty-derived draw-phase state (moved nodes, transform-dirty flag) WHILE RenderDirty is still populated -
    /// the applier and the loop-level RenderDirty.Clear (Phase 3.2) run later. No GPU here.
    /// <list type="bullet">
    /// <item>fully clean -> record nothing; the applier re-draws last frame's units;</item>
    /// <item>non-structural -> record only the geometry-dirty components in place (packet.Kind = Partial);</item>
    /// <item>structural / first build / a partial that turned out structural -> a full tree walk (packet.Kind = Full).</item>
    /// </list>
    /// </summary>
    public void RecordFrame(IRootVisualComponent visualRoot)
    {
        _packet = RentPacket();
        RecordFrameCore(visualRoot);
        // Freeze the layout snapshot HERE, at the END of the DEVICE-FREE record, on the update thread - so the applier (the
        // render thread) reads ONLY the frozen packet: its draws, its layout delta. Never a live component.
        CaptureSnapshot();
        _published.Enqueue(_packet);   // hand it over; the applier drains the queue (see ApplyFrame)
        _packet = null;
    }

    private void RecordFrameCore(IRootVisualComponent visualRoot)
    {
        _movedBuf.Clear();   // filled only by a PARTIAL (a Full re-freezes everything from its packet anyway)
        _packet.Reset(RenderBuildKind.Clean);
        // The projection is a LIVE read of the root visual, so it is taken HERE (recorder thread) and travels ON the packet -
        // the applier must not reach back into the window for it.
        _packet.ProjectionMatrix = visualRoot.GetProjectionMatrix();

        // Fully clean: nothing changed since last build -> the applier re-draws the retained units as-is. Keep the
        // transform memo: nothing moved, so last frame's world transforms are still correct.
        if (_built && !RenderDirty.HasWork) return;

        // Non-structural change on an existing scene -> PARTIAL update: re-render only the geometry-dirty components in
        // place. Either way the retained groups (paint order + unit cache) stay. Drop the frame-scoped world/clip memos ONLY on
        // a MOVE (transform-dirty) - then last frame's baked transforms are stale and must be recomputed. A GEOMETRY-only
        // partial (a hover recolouring a tile) moved nothing, so the memos are still valid: keeping them lets the render
        // pass reuse cached world transforms + clips instead of recomputing them for every one of thousands of units
        // (the O(N) that made a hover cost ~2x a clean frame on a big list).
        // An UNNAMEABLE move (an unowned Transform ticking - RenderDirty couldn't record which component it moves) leaves the
        // recorder unable to tell which snapshot entries went stale, so it escalates to a FULL record: that re-records every
        // component and so re-freezes the whole snapshot from its own packet. (Re-deriving the snapshot from the retained
        // GROUPS instead would be a read of applier-owned state from the recorder's thread - the very thing the split exists
        // to remove.) Rare by construction, and strictly the pre-incremental behaviour.
        if (_built && !RenderDirty.IsStructural && !RenderDirty.IsTransformUnknown && !_forceFullNextFrame)
        {
            _packet.Kind = RenderBuildKind.Partial;
            if (RenderDirty.IsTransform)
            {
                // The frozen snapshot is refreshed INCREMENTALLY: RenderDirty.MarkTransform records WHICH components moved,
                // and CaptureSnapshot re-freezes exactly those entries (an ancestor's move leaves every descendant's LOCAL
                // transform untouched - only the composed WORLD transform changes, and that is a memo, dropped by the
                // applier). Dropping the whole snapshot here instead - as this did - forced a re-capture of EVERY retained
                // unit on every scroll frame: O(scene) per frame, AND a read of the render-owned group list from the update
                // thread, which is exactly what blocked the render-thread handoff.
                _packet.ClearMemos = true;   // APPLIER-owned - it drops them itself (see RenderPacket.ClearMemos)
            }

            // Snapshot the dirty set: RecordReRender re-renders each component, and a component's Render can mark MORE
            // geometry dirty (e.g. an image finishing decode), ADDING to the live RenderDirty.Geometry set mid-loop and
            // throwing "collection was modified". Copy into a reusable buffer and iterate that. The snapshot is taken under
            // RenderDirty's write lock - marks also arrive from OTHER threads (parallel arrange, a Dispatcher-thread Image
            // invalidation), so a lock-free copy raced them and corrupted the set.
            RenderDirty.SnapshotGeometryInto(_geometryDirtyBuffer);

            // RECORD pass (DEVICE-FREE): skip/record each dirty component. component.Render can mark MORE geometry dirty,
            // which the count check below detects; a component that needs a full walk (Fallback) stops the pass.
            var fellBack = false;
            foreach (var component in _geometryDirtyBuffer)
            {
                if (RecordReRender(component, _packet) == PartialRecord.Fallback) { fellBack = true; break; }
            }

            // Partial stands ONLY if nothing structural surfaced and NO new geometry was marked during the RECORD pass
            // (the set didn't grow). If a render re-marked geometry, escalate to a full walk so that change isn't dropped.
            if (!fellBack && !RenderDirty.IsStructural && RenderDirty.GeometryCount == _geometryDirtyBuffer.Count)
            {
                // Geometry-only partial -> nothing moved -> the applier can skip the per-unit transform re-bake (proc).
                _packet.IsTransformDirty = RenderDirty.IsTransform;
                // Which components changed (so the draw phase patches just their slots + replays) and which MOTION NODES
                // moved (so it rewrites their table matrices - the O(1)-scroll path). Read HERE, while RenderDirty is still
                // populated: it is cleared after the whole record phase, and the applier - which may be the render thread -
                // must never touch it. Both travel ON the packet.
                _packet.PartialDirty.AddRange(_geometryDirtyBuffer);
                RenderDirty.SnapshotNodesInto(_packet.MovedNodes);   // under the write lock (MarkNodeTransform runs on other threads too)
                RenderDirty.SnapshotMovedInto(_movedBuf);            // the MOVED components - CaptureSnapshot re-freezes just these
                return;   // packet.Kind == Partial -> ApplyFrame runs the partial apply
            }
            // a structural change or a new invalidation surfaced during the partial pass -> escalate to a full walk
        }

        RecordFullFrame(visualRoot);
    }

    // RECORD a full walk (DEVICE-FREE): first build, a structural change, or a partial that surfaced one. Fills the packet.
    private void RecordFullFrame(IRootVisualComponent visualRoot)
    {
        _commands.Clear();
        _forceFullNextFrame = false;      // this IS that full walk

        var projection = _packet.ProjectionMatrix;
        _packet.Reset(RenderBuildKind.Full);   // a full walk SUPERSEDES anything a partial had already recorded into it
        _packet.ProjectionMatrix = projection;
        _packet.IsTransformDirty = true;       // the paint order is rebuilt; every position must be re-baked
        _packet.ClearMemos = true;             // applier-owned - it drops them itself (see RenderPacket.ClearMemos)

        // A full walk rebuilds the PAINT ORDER. The frozen layout snapshot has nothing to do with paint order - it is a
        // per-component freeze of transform/size/clip - so dropping it here re-froze all ~9000 components on every structural
        // frame: a matrix compose plus two dictionary writes plus a delta entry, each, for components that had not changed at
        // all. It was the single biggest part of the walk.
        //
        // Keep it, and re-freeze only what actually changed. That is knowable: every mutation that can invalidate an entry
        // NAMES its component - geometry (RenderSize/clip), a move (LocalTransform), a motion node, and a structural change
        // (VisualParent). Anything the marks CANNOT name - an unattributed move, or a settling theme/DPI swap, whose cascade
        // deliberately writes outside the mark system - still drops the whole thing, exactly as before.
        var reFreezeEverything = RenderDirty.IsStructuralUnknown || RenderDirty.IsTransformUnknown;
        if (reFreezeEverything)
        {
            _snap.Clear();
            _packet.SnapReset = true;   // ...and so must the applier's replica
        }
        _snapFullCapture = false;

        // What changed, for CaptureSnapshot to re-freeze. Read HERE, while RenderDirty is still populated (it is cleared after
        // the whole record phase, and the applier must never touch it).
        RenderDirty.SnapshotGeometryInto(_geometryDirtyBuffer);
        RenderDirty.SnapshotStructuralInto(_structuralBuf);
        RenderDirty.SnapshotMovedInto(_movedBuf);
        RenderDirty.SnapshotNodesInto(_movedNodesCapture);

        RecordFullWalk(visualRoot, _packet);   // device-free: walk + component.Render + copy commands into the packet
    }

    private readonly List<IUIComponent> _structuralBuf = new();      // components that entered/left the drawn set this frame
    private readonly List<IUIComponent> _movedNodesCapture = new();  // moved motion nodes, for the snapshot re-freeze on a FULL frame

    /// <summary>
    /// APPLY half of the frame build (GPU - the render-thread side in Phase 3.3). Consumes the packet the recorder filled:
    /// Clean re-draws the retained units; Partial updates the dirty groups in place (splicing count changes), and if a
    /// newly-appearing component has no paint rank it re-records + applies a full walk (inline-safe: the tree is quiescent
    /// between RECORD and APPLY); Full rebuilds the paint-order groups. Freezes the layout snapshot the draw pass replays,
    /// then clears RenderDirty (Phase 3.2 hoists this clear to the loop level so it runs once after all windows record).
    /// </summary>
    public void ApplyFrame()
    {
        BeginApplyFrame();
        var drew = false;
        while (_published.TryDequeue(out var packet))
        {
            ApplyPacket(packet);
            packet.Reset(RenderBuildKind.Clean);
            _spare.Add(packet);   // back to the pool for the recorder
            drew = true;
        }

        // Phase 3.2 Step 2b: only the DEFAULT single-threaded path (record+apply inline in BeginDraw) clears here. In the
        // decoupled path the record runs at loop level (UIApplication.RecordRenderFrame) and the clear is hoisted there,
        // once after ALL windows have recorded - so a second window still sees the full dirty set this frame.
        if (drew && RenderThreadOptions.SingleThreaded) RenderDirty.Clear();
    }

    /// <summary>Starts a fresh DRAW frame's merged apply state. The per-frame results (build kind, dirty set, moved nodes)
    /// accumulate across every packet applied for THIS draw, and the draw consumes them - so they must be empty when it
    /// begins, not left over from the previous one.</summary>
    private void BeginApplyFrame()
    {
        LastBuildKind = RenderBuildKind.Clean;
        LastBuildTransformDirty = false;
        _partialDirty.Clear();
        _movedNodesBuf.Clear();
        _partialSpliced = false;
    }

    private readonly System.Text.StringBuilder _glyphWarmBuf = new();
    private readonly HashSet<Adamantium.Graphics.Fonts.FontAtlas> _warmAtlases = new();

    // Warm the font atlases for every text block this packet is about to realize - all of them, in ONE batch per atlas.
    //
    // Rasterizing a glyph is MSDF work (~23 ms in Debug), and the generator parallelises across glyphs - but a text unit built
    // one at a time can only ever hand it the characters ITS block introduced. A cold fill realizes ~50 new blocks, each
    // bringing about one new character, so those glyphs were rasterized serially, on one core: 1.1 s of a 1.9 s 4K fill, its
    // single biggest cost. Here the whole packet's characters are pooled first, so the generator gets them together and
    // spreads them across the cores it already knows how to use. Laziness is untouched - only glyphs the UI actually shows
    // are ever rasterized (prewarming a fixed charset instead just moves the same cost into startup and pays for glyphs
    // nobody asked for).
    private void WarmTextAtlases(RenderPacket packet)
    {
        var device = _renderUnitFactory.GraphicsDevice;
        if (device == null || packet.Draws.Count == 0) return;

        _warmAtlases.Clear();
        _glyphWarmBuf.Clear();
        foreach (var draw in packet.Draws)
        foreach (var command in draw.Commands)
        {
            if (command.Payload is not TextPayload { TextLayout: { } layout } || string.IsNullOrEmpty(layout.Text)) continue;
            _warmAtlases.Add(layout.EnsureAtlas(device));
            _glyphWarmBuf.Append(layout.Text);
        }
        if (_warmAtlases.Count == 0) return;

        var text = _glyphWarmBuf.ToString();
        foreach (var atlas in _warmAtlases) atlas.Warm(text);
    }

    // Realize ONE packet. The per-frame results the DRAW pass reads (LastBuildKind, LastBuildTransformDirty, _partialDirty,
    // _movedNodesBuf) are MERGED across the packets drained this frame: a Full supersedes everything recorded before it, and
    // two Partials union their dirty sets - exactly what one record of the combined interval would have produced.
    private void ApplyPacket(RenderPacket packet)
    {
        WarmTextAtlases(packet);   // one batched glyph rasterization for the whole packet, before any unit is built

        // The paint-order ranks, if the recorder re-derived them (a fresh, immutable map - see RenderPacket.Orders).
        if (packet.Orders != null) _applyOrder = packet.Orders;
        _projectionMatrix = packet.ProjectionMatrix;

        // The applier's OWN derived memos (composed world/clip/node transforms), dropped here rather than by the recorder:
        // they are applier-resident state, and the recorder runs on the loop thread while the applier reads them. The
        // recorder only says WHEN they went stale.
        if (packet.ClearMemos)
        {
            _worldCache.Clear();
            _clipCache.Clear();
            _relWorldCache.Clear();
            _nodeCache.Clear();
        }

        // Fold this packet's layout delta into the applier's private snapshot replica - the only thing the draw pass reads
        // for a component's transform/size/clip. A full walk resets it and then carries the whole scene.
        if (packet.SnapReset) _applySnap.Clear();
        foreach (var entry in packet.SnapDelta) _applySnap[entry.Key] = entry.Value;

        switch (packet.Kind)
        {
            case RenderBuildKind.Clean:
                break;   // re-draw the retained units as-is (nothing to realize)

            case RenderBuildKind.Partial:
            {
                // APPLY pass (GPU): realize the recorded draws - update the units in place / splice a count change.
                foreach (var draw in packet.Draws)
                {
                    if (ApplyReRender(draw.Component, draw.Commands)) continue;

                    // A recorded partial draw with no paint rank. Unreachable by construction: RecordReRender re-derives
                    // the ranks (a device-free order-only walk) and escalates to a FULL record when a component still has
                    // none, so every draw that reaches here has a place. Kept as a safety net that reads NOTHING live -
                    // the applier may be the render thread, so re-recording the tree from HERE (what this used to do)
                    // would race the loop's mutations. Instead ask the RECORDER for a full walk next frame.
                    _forceFullNextFrame = true;
                    break;
                }

                if (LastBuildKind != RenderBuildKind.Full) LastBuildKind = RenderBuildKind.Partial;
                LastBuildTransformDirty |= packet.IsTransformDirty;
                _partialDirty.AddRange(packet.PartialDirty);
                _movedNodesBuf.AddRange(packet.MovedNodes);
                break;
            }

            case RenderBuildKind.Full:
                ApplyFullWalk(packet);   // GPU: rebuild the paint-order groups from the packet
                _built = true;
                // A full walk re-records the WHOLE scene, so anything an earlier packet of this same drain marked dirty is
                // already covered by it - and its unit set is gone (the groups were rebuilt), which would make those stale
                // entries mis-patch the batch. Drop them.
                LastBuildKind = RenderBuildKind.Full;
                LastBuildTransformDirty = true;
                _partialDirty.Clear();
                _movedNodesBuf.Clear();
                _partialSpliced = false;
                break;
        }
    }

    // Re-render ONE already-cached component IN PLACE (its geometry went dirty). Returns false - "needs a full walk" -
    // only when this component has no recorded paint position yet (never in a full build). On a same-shape update the
    // unit objects are reused via UpdateWithDrawCommand (its group already references them - no change). On a COUNT
    // change - a hover background appearing (0->1 commands) or vanishing, a per-frame chart re-recording a different
    // number of segments - only THIS component's group mutates; every other group keeps its units untouched.
    // The record and apply decision for one dirty component: Skip = reuse its cached units as-is (nothing recorded);
    // Fallback = the caller must do a full walk; Recorded = its commands were captured into the packet for the applier.
    private enum PartialRecord { Skip, Fallback, Recorded }

    // RECORD half of a partial re-render for ONE geometry-dirty component (DEVICE-FREE): the skip/fallback decisions +
    // component.Render, copying the recorded commands into the packet. No GPU - the applier (ApplyReRender) realizes them.
    private PartialRecord RecordReRender(IUIComponent component, RenderPacket packet)
    {
        // Invisible (Collapsed/Hidden - e.g. an auto-hide ScrollBar that re-marks geometry dirty on every mouse-move):
        // it draws nothing. If it holds no units (the norm - going invisible was STRUCTURAL and already removed them),
        // SKIP it like a detached/collapsed one below, instead of forcing a full tree walk every dirty frame (the hover
        // FPS hitch). Only if it somehow still holds units fall back to a full walk to reconcile them.
        if (component.Visibility != Visibility.Visible)
            return HoldsUnits(component) ? PartialRecord.Fallback : PartialRecord.Skip;

        // Not in the live paint tree: DETACHED (no visual parent) or effectively hidden by a COLLAPSED ancestor. The full
        // walk never reaches such a component, so it has no paint rank and re-rendering it draws nothing - yet it used to
        // force a FULL tree rebuild EVERY frame it was geometry-dirty (a detached/pooled text block, a text block inside a
        // collapsed panel, an auto-hide scrollbar's parts). Skip it: it holds no units (a real detach/collapse is
        // STRUCTURAL and already removed them via a full walk), so there is nothing to draw or reclaim here.
        if (!component.IsAttachedToVisualTree) return PartialRecord.Skip;
        for (var a = component.VisualParent; a != null; a = a.VisualParent)
            if (a.Visibility != Visibility.Visible) return PartialRecord.Skip;

        // A geometry-dirty component from a FOREIGN visual tree - a popup/menu/tooltip subtree (a PopupRoot), drawn by
        // the popup stage's OWN cache, never by this one. Skip it WITHOUT rendering: Render() below would consume its
        // IsGeometryValid=false, and that flag is precisely the signal the popup stage's rebuild gate
        // (PopupRenderProcessor.OverlayChanged) polls to notice the change - the MAIN cache building first and eating it
        // starved that gate, so a menu item's hover recolour never redrew. (Tree-top walk: cheap - it only runs for the
        // handful of dirty components of a partial frame.)
        if (_lastVisualRoot != null)
        {
            var top = component;
            while (top.VisualParent != null) top = top.VisualParent;
            if (!ReferenceEquals(top, _lastVisualRoot)) return PartialRecord.Skip;
        }

        // Marked dirty EXTERNALLY (an animation heartbeat / duplicate mark) while its own geometry is still VALID:
        // Render() below no-ops on the flag and records ZERO commands, which the count-change path would read as
        // "now draws nothing" and DELETE the retained units (the mass tile vanish on ease-back completion). Nothing
        // about its recorded geometry changed - keep the retained units as-is.
        if (component.IsGeometryValid) return PartialRecord.Skip;

        // Holds no units yet AND has no paint rank: it was invisible/absent during the last full walk and has just appeared
        // (an auto-hide ScrollBar fading in), so the applier would have nowhere to splice its units. Re-derive the ranks with
        // a cheap ORDER-ONLY walk (no Render, no unit work) - and do it HERE, on the record side: the walk reads the LIVE
        // tree, which the applier (the render thread) must never touch. Genuinely not in the tree -> a full record.
        if (!HoldsUnits(component) && !_orderByControl.ContainsKey(component.RenderId))
        {
            ReassignOrders();
            if (!_orderByControl.ContainsKey(component.RenderId)) return PartialRecord.Fallback;
        }

        _drawingContextInternal.Clear();
        component.Render(_drawingContext);   // NB: consumes the dirty flag (Render sets IsGeometryValid back to true)
        var commands = CopyCommands(_drawingContextInternal.GetDrawCommands());
        packet.Draws.Add(new ComponentDraw(component, commands, false));
        MirrorUnits(component, commands.Count, false);   // it WAS dirty: no commands now means "draws nothing" -> units freed
        return PartialRecord.Recorded;
    }

    // APPLY half (GPU / render-thread side): realize ONE recorded partial draw - update the group's units in place (same
    // count + type) or splice in the count/type change. Returns false only when a newly-appearing component has no paint
    // rank (-> the caller does a full walk). wasGeometryValid is not used here (RecordReRender already skipped the clean).
    private bool ApplyReRender(IUIComponent component, IReadOnlyList<IDrawCommand> drawCommands)
    {
        _groupById.TryGetValue(component.RenderId, out var group);
        var oldCount = group?.Units.Count ?? 0;

        // Fast path: same command count and every unit still matches -> update in place; nothing structural changed.
        if (group != null && drawCommands.Count == oldCount && oldCount > 0)
        {
            var units = group.Units;
            var allMatch = true;
            for (var i = 0; i < drawCommands.Count; i++)
            {
                drawCommands[i].RenderData.ProjectionMatrix = _projectionMatrix;
                if (!units[i].Match(drawCommands[i])) { allMatch = false; break; }   // payload type changed
            }
            if (allMatch)
            {
                for (var i = 0; i < drawCommands.Count; i++) units[i].UpdateWithDrawCommand(drawCommands[i]);
                return true;
            }
        }

        // Count (or a unit type) changed. The change stays LOCAL to this control's group - BuildUnitsFor refreshes the
        // group's own Units list in place and no other group moves (the flat-list splice this replaced shifted every
        // later unit). The recorded op stream + rect-slot map still reference the old unit set though, so the draw
        // phase must re-walk this frame (per-group op patching is the planned follow-up that lifts this too).
        _partialSpliced = true;
        var isNewGroup = group == null;
        // A brand-new group with no paint rank can't be placed. The RECORDER already re-derives the ranks (an order-only walk
        // in RecordReRender) and escalates to a full record when a component still has none, so this is unreachable - and
        // re-walking the LIVE tree from here, as it used to, is precisely what the applier (the render thread) must not do.
        // Report it instead; ApplyPacket asks the recorder for a full walk next frame.
        if (isNewGroup && !_applyOrder.ContainsKey(component.RenderId)) return false;

        group = BuildUnitsFor(component, drawCommands, _projectionMatrix);

        if (isNewGroup)
        {
            // First units this control ever drew (a background appearing 0->1): place its group by DFS paint rank -
            // before the first group that ranks after it. Existing groups never move.
            var order = _applyOrder[component.RenderId];
            var pos = _groups.Count;
            for (var i = 0; i < _groups.Count; i++)
            {
                if (_applyOrder.GetValueOrDefault(_groups[i].ControlId, int.MaxValue) > order) { pos = i; break; }
            }
            _groups.Insert(pos, group);
        }
        return true;
    }

    /// <summary>
    /// Builds units from a FLAT list of components (the adorner stage), instead of walking a visual tree. Each
    /// component renders and is cached by RenderId exactly as in the tree build; units of components no longer in the
    /// list (e.g. a deselected adorner) are disposed. Used for overlays that aren't part of the content tree.
    /// </summary>
    public void BuildFromComponents(IReadOnlyList<IUIComponent> components, Matrix4x4F projectionMatrix)
    {
        // A FULL rebuild every call (clears + re-renders every component). Must record that: the batches' Clean-frame
        // upload-skip reads LastBuildKind, and without this it stays at its default (Clean) so the overlay batch would
        // skip EVERY GPU upload - its SSBO never fills and the whole overlay (menus, tooltips, SlidePanel) renders nothing.
        LastBuildKind = RenderBuildKind.Full;
        _commands.Clear();
        _groups.Clear();
        _snap.Clear();         // full rebuild -> drop last frame's frozen layout snapshot (else stale overlay positions + unbounded _snap growth)
        _worldCache.Clear();   // new frame: drop last frame's transform + clip memos
        _clipCache.Clear();
        _relWorldCache.Clear();
        _nodeCache.Clear();

        // This build FUSES record and apply (an overlay cache renders a flat list straight into its groups - it never crosses
        // the render-thread seam), but the snapshot still flows through a packet, because that is the only thing that fills the
        // applier's replica. Rent one, capture into it, fold it in, hand it back.
        _packet = RentPacket();
        _packet.Reset(RenderBuildKind.Full);
        _packet.SnapReset = true;

        var present = new HashSet<Guid>();
        if (components != null)
        {
            foreach (var component in components)
            {
                if (component.Visibility != Visibility.Visible) continue;
                present.Add(component.RenderId);

                var wasGeometryValid = component.IsGeometryValid;
                _drawingContextInternal.Clear();
                component.Render(_drawingContext);
                ProcessRenderCommands(component, _drawingContextInternal.GetDrawCommands(), projectionMatrix, wasGeometryValid);
            }
        }

        // Free the units of any component dropped from the list since the last build.
        List<Guid> stale = null;
        foreach (var id in _groupById.Keys)
            if (!present.Contains(id)) (stale ??= new List<Guid>()).Add(id);
        if (stale != null)
            foreach (var id in stale) RemoveAndDeferDispose(id);

        // Freeze the overlay's layout snapshot. Its draws never went through a packet, so the capture reads the just-built
        // GROUPS (safe here, and only here: this cache's recorder and applier are the same thread).
        _snapFullCapture = true;
        CaptureSnapshot();

        _applySnap.Clear();
        foreach (var entry in _packet.SnapDelta) _applySnap[entry.Key] = entry.Value;
        _packet.Reset(RenderBuildKind.Clean);
        _spare.Add(_packet);
        _packet = null;
    }

    /// <summary>
    /// Immediately disposes every cached render unit and empties the cache. The caller must ensure the GPU is
    /// idle first (e.g. after a DeviceWaitIdle). Used by the off-screen designer, which builds a brand-new tree
    /// each render: those controls never detach (each owns its own root window), so the attachment-based
    /// reconciliation can't reclaim them - the designer resets the cache between renders instead.
    /// </summary>
    public void DisposeUnits()
    {
        foreach (var group in _groupById.Values)
        {
            foreach (var unit in group.Units)
                unit?.Dispose();
        }

        _groupById.Clear();
        _groups.Clear();
        _recordedUnits.Clear();   // the recorder's mirror of the above
        // The designer builds a brand-new tree per render (fresh RenderIds), so the frozen layout of the old one - both the
        // recorder's map and the applier's replica - is dead weight that would otherwise grow unboundedly.
        _snap.Clear();
        _applySnap.Clear();
    }

    /// <summary>The projection of the last packet the applier realized - captured by the RECORDER from the root visual. The
    /// applier must use this rather than re-reading the live window, which it may not touch (it can be the render thread).</summary>
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

    private Matrix4x4F _projectionMatrix;

    // Frame-scoped world-transform memo. UIComponent.WorldTransform is computed LIVE O(depth) on every access, and the
    // render pass reads it many times per unit - ResolveScissor walks every ClipToBounds ancestor and reads each one's
    // WorldTransform - so the naive path is O(depth^2) per unit x N units (measured: 13 ms of a 19 ms render for 205
    // units). Within ONE frame the transforms are already stable (layout + animation ran before render), so compose each
    // component's world transform ONCE here: World(c) = c.LocalTransform * World(parent), memoized => O(nodes)/frame.
    // Cleared at each frame's build. Only the render path uses this; WorldTransform stays live for hit-test / layout.
    private readonly Dictionary<IUIComponent, Matrix4x4F> _worldCache = new();

    // RECORDER-owned frozen layout state (see LayoutSnapshot): the authority, mutated on the update thread. The APPLIER never
    // reads it - it folds each packet's SnapDelta into its own replica (_applySnap) instead, so the two threads share no
    // mutable map. Persistent across frames and refreshed incrementally (CaptureSnapshot).
    private readonly Dictionary<IUIComponent, LayoutSnapshot> _snap = new();

    // APPLIER-owned replica, built ONLY from the deltas of packets it has consumed - what the draw pass actually reads.
    private readonly Dictionary<IUIComponent, LayoutSnapshot> _applySnap = new();

    // --- The recorder -> applier seam (Phase 3.3, docs/RENDER_THREAD_PLAN.md) --------------------------------------------
    // DOUBLE-BUFFERED: the recorder fills one packet while the applier consumes another, so the loop no longer waits for the
    // GPU frame (the 3.3 scaffold had ONE packet + a free token, which lock-stepped the loop to the render's pace - no
    // anti-freeze at all). Packets are DELTAS, so a queued one may never be DROPPED (a Partial carries only that frame's
    // dirty components - skipping it loses them for good). The applier therefore drains EVERY queued packet in order and
    // draws ONCE afterwards: stale frames are collapsed at the DRAW, never at the delta.
    private readonly ConcurrentQueue<RenderPacket> _published = new();   // recorded, awaiting the applier
    private readonly ConcurrentBag<RenderPacket> _spare = new();         // consumed, back for reuse
    private RenderPacket _packet;                                        // the one being recorded right now

    private RenderPacket RentPacket() => _spare.TryTake(out var packet) ? packet : new RenderPacket();

    // Record one component's frozen layout into the recorder's map AND into this frame's delta (the applier's replica is
    // built from nothing else). Memoised: an unchanged component is captured once and never re-sent.
    private LayoutSnapshot Snap(IUIComponent c)
    {
        if (_snap.TryGetValue(c, out var s)) return s;
        s = new LayoutSnapshot(c.LocalTransform, c.RenderSize, c.ClipToBounds, c.IsRenderMotionNode, c.VisualParent);
        _snap[c] = s;
        _packet.SnapDelta.Add(new KeyValuePair<IUIComponent, LayoutSnapshot>(c, s));
        return s;
    }

    // APPLIER's view of a component's frozen layout: its private replica, folded from the packets' deltas. A miss means the
    // recorder never froze that component - which cannot happen for anything the applier draws - so it falls back to the live
    // component, exactly as the pre-split code did. That fallback is the ONE live read left on this side; it is unreachable in
    // the recorded paths.
    private LayoutSnapshot ApplySnap(IUIComponent c)
    {
        if (_applySnap.TryGetValue(c, out var s)) return s;
        s = new LayoutSnapshot(c.LocalTransform, c.RenderSize, c.ClipToBounds, c.IsRenderMotionNode, c.VisualParent);
        _applySnap[c] = s;
        return s;
    }

    // Eagerly freeze the layout snapshot of EVERY component the draw/compose pass will read - each retained unit's
    // component and its ancestor chain (World/CumulativeClip/NodeOf recurse to the root) plus the moved motion nodes -
    // at the END of the recorder (the build). After this the applier's Snap() lookups are all HITS, so the draw pass
    // never dereferences a live IUIComponent: it is the recorder->applier handoff of the frozen layout state
    // (docs/RENDER_THREAD_PLAN.md), the prerequisite for running the applier on a separate render thread while layout
    // mutates the tree. Memoised + the per-component ancestor early-out (ContainsKey) keep it O(distinct components).
    // INCREMENTAL (O(changed), not O(scene)): the snapshot PERSISTS across frames, and only what actually changed this frame is
    // re-frozen - the components the packet re-recorded (their size/clip may differ), the motion nodes that moved, and the
    // components that moved (RenderDirty names them). Nothing else can be stale: an ancestor moving does not change a
    // descendant's LOCAL transform, only the composed WORLD one - and that is a derived memo, dropped separately. A Full walk
    // drops the snapshot and its packet carries every component, so it re-freezes everything by construction.
    //
    // This is what lets the record run off the render thread: the previous version re-derived the snapshot by walking `_groups`
    // - the RENDER-side retained unit list - every frame, which is both O(scene) and a cross-thread read of render-owned state.
    // The group walk survives ONLY for the flat adorner build (BuildFromComponents), which has no packet and whose recorder and
    // applier always run together inline on the loop thread.
    private void CaptureSnapshot()
    {
        if (_snapFullCapture)
        {
            foreach (var group in _groups)
            foreach (var unit in group.Units)
                for (var c = unit.Component; c != null && !_snap.ContainsKey(c); c = c.VisualParent)
                    Snap(c);
            _snapFullCapture = false;
        }

        if (_packet.Kind == RenderBuildKind.Full)
        {
            // A FULL walk re-records the whole scene, but that does NOT mean the whole scene's LAYOUT changed - the walk exists
            // to rebuild the paint ORDER. So freeze only what a mark says actually changed, plus components seen for the first
            // time (Snap fills those lazily and publishes them). Re-freezing all ~9000 every structural frame - a matrix compose
            // and two dictionary writes each, for components that had not moved or resized - was the biggest single cost of the
            // walk. Anything the marks cannot name (an unattributed move, a settling theme/DPI swap) cleared the snapshot back in
            // RecordFullFrame, so every entry below is genuinely new and this loop re-freezes the scene exactly as it used to.
            foreach (var draw in _packet.Draws)
                for (var c = draw.Component; c != null && !_snap.ContainsKey(c); c = c.VisualParent)
                    Snap(c);

            foreach (var component in _geometryDirtyBuffer) RefreshSnapshot(component);   // size / clip may have changed
            foreach (var component in _structuralBuf) RefreshSnapshot(component);         // VisualParent may have changed
            foreach (var node in _movedNodesCapture) RefreshSnapshot(node);
            foreach (var moved in _movedBuf) RefreshSnapshot(moved);
            return;
        }

        // Re-recorded this frame (the dirty/newly-spliced components of a Partial).
        foreach (var draw in _packet.Draws) RefreshSnapshot(draw.Component);
        // Moved this frame. A moved MOTION NODE whose entry is kept stale is the classic O(1)-path regression: World() then
        // composes the node from LAST frame's LocalTransform, so a tilting tile never moves and a flipping one sticks at the
        // angle it had when its 90-degree face-swap re-recorded it (the swap is geometry - it rides the packet's draws - but
        // the ANGLE lives only here). Read the nodes off THIS frame's packet: _movedNodesBuf is the APPLIER's copy of them.
        foreach (var node in _packet.MovedNodes) RefreshSnapshot(node);
        foreach (var moved in _movedBuf) RefreshSnapshot(moved);
    }

    // Re-freeze ONE component's entry (it changed this frame, so the memoised ContainsKey early-out must NOT keep the old one),
    // then walk its ancestor chain lazily - those didn't change unless they are themselves in one of the changed sets, where
    // their own RefreshSnapshot handles them.
    private void RefreshSnapshot(IUIComponent component)
    {
        if (component == null) return;
        var snapshot = new LayoutSnapshot(component.LocalTransform, component.RenderSize, component.ClipToBounds,
            component.IsRenderMotionNode, component.VisualParent);
        _snap[component] = snapshot;
        // ...AND publish it. The delta is the ONLY thing the applier's replica is built from, so a re-freeze that updates the
        // recorder's map alone leaves the applier composing this component from its PREVIOUS transform: a tilting tile never
        // moves and a flipping one draws its new face at the old angle. (Snap() below publishes on its own - it only writes
        // when the entry is new - but this force-refresh overwrites an existing one, so it must publish explicitly.)
        _packet.SnapDelta.Add(new KeyValuePair<IUIComponent, LayoutSnapshot>(component, snapshot));
        for (var c = component.VisualParent; c != null && !_snap.ContainsKey(c); c = c.VisualParent)
            Snap(c);
    }

    private Matrix4x4F World(IUIComponent c)
    {
        if (_worldCache.TryGetValue(c, out var m)) return m;
        var s = ApplySnap(c);
        m = s.VisualParent != null ? s.LocalTransform * World(s.VisualParent) : s.LocalTransform;
        _worldCache[c] = m;
        return m;
    }

    // --- Motion-node memos (the O(1)-scroll path) --------------------------------------------------------------------
    // NodeOf: the nearest IsRenderMotionNode ancestor (or null). RelWorld: the component's transform RELATIVE to that
    // node (identity AT the node) - what a node-local bake uses; the shader applies the node's table matrix on top.
    // Cleared together with _worldCache (same lifetime: structure/transform changes invalidate both).
    private readonly Dictionary<IUIComponent, IUIComponent> _nodeCache = new();
    private readonly Dictionary<IUIComponent, Matrix4x4F> _relWorldCache = new();
    private readonly Dictionary<IUIComponent, int> _nodeRefreshed = new();   // node -> walk version its slot was refreshed
    // Per RECORDING walk: node -> "every drawn unit under it is in a node-aware batch (rect/ellipse with its slot)".
    // A moved node with ANY non-aware content (world-baked text, per-unit draws) can't take the slot-write fast path -
    // those retained draws would stay at the old position - so the frame falls back to the full walk.
    private readonly Dictionary<Guid, bool> _nodeAllAware = new();
    // APPLIER-owned: the moved nodes of the packets drained for THIS draw (the draw rewrites their table matrices, then clears
    // it). The RECORDER must not read it - it takes the frame's moved nodes off RenderDirty into packet.MovedNodes.
    private readonly List<IUIComponent> _movedNodesBuf = new();
    // RECORDER-owned: the components that MOVED this frame - CaptureSnapshot re-freezes exactly their snapshot entries.
    private readonly List<IUIComponent> _movedBuf = new();
    private bool _snapFullCapture;   // adorner build only: re-capture the snapshot from the retained units

    private IUIComponent NodeOf(IUIComponent c)
    {
        if (c == null) return null;
        if (_nodeCache.TryGetValue(c, out var n)) return n;
        var s = ApplySnap(c);
        n = s.IsMotionNode ? c : NodeOf(s.VisualParent);
        _nodeCache[c] = n;
        return n;
    }

    private Matrix4x4F RelWorld(IUIComponent c)
    {
        if (_relWorldCache.TryGetValue(c, out var m)) return m;
        var s = ApplySnap(c);
        m = s.IsMotionNode
            ? Matrix4x4F.Identity
            : (s.VisualParent is { } p ? s.LocalTransform * RelWorld(p) : s.LocalTransform);
        _relWorldCache[c] = m;
        return m;
    }

    // Resolve a unit's bake transform + transform-table slot: node-local + the node's slot when under a motion node
    // (refreshing the node's matrix once per walk), else world + slot 0 (identity).
    private Matrix4x4F ResolveBake(IGraphicsDevice device, IUIComponent component, Matrix4x4F world, out int slot)
    {
        var node = NodeOf(component);
        if (node == null) { slot = 0; return world; }
        slot = _transformTable.AcquireSlot(node.RenderId);
        if (!_nodeRefreshed.TryGetValue(node, out var v) || v != _walkVersion)
        {
            _nodeRefreshed[node] = _walkVersion;
            _transformTable.SetMatrix(device, slot, World(node));
        }
        _nodeAllAware.TryAdd(node.RenderId, true);
        return RelWorld(component);
    }

    // A unit under a motion node drew through a path its slot matrix can't move (world-baked text, per-unit, gradient
    // for now) -> the node loses the slot-write fast path this frame set (recorded per walk).
    private void MarkNodeNotAware(IUIComponent component)
    {
        var node = NodeOf(component);
        if (node != null) _nodeAllAware[node.RenderId] = false;
    }

    // Apply the moved nodes' new matrices (64 bytes each) before a replay-based draw; stale position memos drop and are
    // rebuilt lazily O(dirty). Returns false when ANY moved node has non-aware retained content - the caller full-walks.
    private bool RefreshMovedNodes(IGraphicsDevice device)
    {
        if (_movedNodesBuf.Count == 0) return true;
        foreach (var node in _movedNodesBuf)
            if (!_nodeAllAware.GetValueOrDefault(node.RenderId, false))
                return false;
        // Positions moved - drop the WORLD memos (rebuilt lazily from _snap). NOT _snap: the recorder already re-captured
        // the moved nodes' fresh LocalTransform this frame (CaptureSnapshot at the end of the build), so World recomposes
        // the new position straight from the frozen snapshot - the applier never re-reads the live node here (the whole
        // point of the snapshot). NOT the clip memo either: a clip is the ClipToBounds ancestors' viewport (a scroll
        // viewport, a panel), which a node moving INSIDE it never changes; the spliced-patch bake that follows reads
        // CumulativeClip, and recomputing it from live ancestor Bounds mid-relayout produced a transiently-wrong viewport
        // that CULLED on-screen tiles for one frame (the hover "empty cell"). A move that DOES change a viewport
        // (resize/maximize) is structural -> a full walk, which clears every memo in BuildFromVisualTree.
        _worldCache.Clear();
        _relWorldCache.Clear();
        foreach (var node in _movedNodesBuf)
            if (_transformTable.TryGetSlot(node.RenderId, out var slot))
                _transformTable.SetMatrix(device, slot, World(node));
        _movedNodesBuf.Clear();
        return true;
    }

    // Frame-scoped clip memo. A unit's scissor is the intersection of every ClipToBounds ancestor's world-space viewport -
    // a value that depends ONLY on the ancestor chain, not the unit, so all units under the same clipping ancestor (e.g.
    // every item in one ScrollViewer) share the SAME clip. The old code recomputed that whole walk per unit (180 tiles ->
    // 180 identical walks). Memoize it per component instead: CumulativeClip(c) = (c clips ? c.worldRect : none) ∩
    // CumulativeClip(parent). Cleared each frame with the world memo.
    private readonly Dictionary<IUIComponent, Rect?> _clipCache = new();

    private Rect? CumulativeClip(IUIComponent c)
    {
        if (c == null) return null;
        if (_clipCache.TryGetValue(c, out var cached)) return cached;
        var s = ApplySnap(c);
        var parentClip = CumulativeClip(s.VisualParent);
        var result = parentClip;
        if (s.ClipToBounds)
        {
            var rect = new Rect(0, 0, s.RenderSize.Width, s.RenderSize.Height).TransformToAABB(World(c));
            result = parentClip is { } p ? p.Intersect(rect) : rect;
        }
        _clipCache[c] = result;
        return result;
    }

    // CPU pre-transform text batch aggregator (docs/TEXT_GLYPH_BATCH_PLAN.md §9 Stage 2). Created lazily on the first
    // Render that has a device (GPU-free test renders never batch). Frame-scoped state lives inside it.
    private TextBatchCollector _textBatch;

    // Item-background batch (the "подложки" instancing). Sibling of the text batch: solid rounded-rect fills batched
    // into one SDF-AA'd instanced draw. Rects are the LOWER layer - FlushBatches draws rects THEN text. Both batches
    // share one clip GROUP (_batchScissor): a scissor (or text-atlas) change flushes both together, preserving order.
    private RectBatchCollector _rectBatch;
    private EllipseBatchCollector _ellipseBatch;   // SDF family, same fill layer as rects (below text)

    // GPU-resident transform table (one world matrix per MOTION NODE; slot 0 = identity for legacy world-space bakes).
    // The SDF vertex shaders fetch each instance's matrix by its slot index, so moving a node costs ONE matrix write
    // instead of re-baking its instances - and rotated/3D instances stay batched. Owned per cache (the popup overlay
    // cache gets its own), initialised in the Render device block alongside the collectors.
    private TransformTable _transformTable;
    private GradientRectCollector _gradientRectBatch;   // SDF family: rounded rects with a linear/radial GRADIENT fill
    private GradientEllipseCollector _gradientEllipseBatch;   // SDF family: ellipses with a linear/radial GRADIENT fill
    private Rect2D _batchScissor;
    private bool _batchOpen;

    // General instanced fills (arbitrary tessellated geometry sharing a mesh), collected in the render walk and flushed in
    // PAINT ORDER (their natural z-layer) via FlushBatches. Own buffer manager: the instance SSBOs + shared meshes are
    // distinct from the per-unit geometry buffers.
    private GpuBufferManager _instanceBuffers;
    private InstancedFillCollector _instancedFill;

    // --- Retained draw (clean-frame op replay) ---
    // On a fully-Clean frame the walk would re-bake byte-identical batch items and re-issue identical draws for every
    // one of thousands of units - the idle draw floor (measured ~15 ms for the 60k-tile stress view -> ~0.8 ms replayed).
    // Instead, every NON-clean frame RECORDS the ordered GPU op stream the walk emits (scissor changes, per-unit direct
    // draws, SDF/text batch segments, instanced-fill flushes), and the next Clean frame REPLAYS it directly: the retained
    // batch/instance buffers still hold last frame's bytes (BeginFrame is skipped, uploads were already skipped by
    // SceneClean), so replay reproduces the exact frame with ~0 per-unit CPU. The immediate-draw path is UNCHANGED -
    // recording only appends alongside it. _opsReplayable is the escape hatch for a draw type the flat op stream can't
    // faithfully reproduce (there is none today - the SDF/text batches AND the general instanced fills all replay via
    // their recorded segments/flushes); a future such type clears it and that Clean frame safely re-walks instead.
    private enum RenderOpKind : byte { Scissor, Unit, Segment, InstancedFlush }
    private struct RenderOp
    {
        public RenderOpKind Kind;
        public Rect2D Scissor;    // Scissor
        public IRenderUnit Unit;  // Unit
        public byte Batch;        // Segment: which collector (0 rect, 1 ellipse, 2 text)
        public int SegIndex;      // Segment: index into that collector's recorded segment list; InstancedFlush: flush index
    }
    private readonly List<RenderOp> _ops = new();
    private bool _recording;       // this frame runs the walk and is appending ops
    private bool _opsRecorded;     // _ops holds a complete frame from a prior walk
    private bool _opsReplayable;   // the recorded stream faithfully reproduces the frame (currently always true - see above)

    /// <summary>Out-of-render-pass pass: recorded before BeginRendering (shared-surface latch copies).</summary>
    public void PreRender()
    {
        foreach (var group in _groups)
        foreach (var unit in group.Units)
        {
            unit.PreRender();
        }
    }

    /// <summary>Renders every cached unit with no scissor management. Used by GPU-free tests (no device).</summary>
    public void Render() => Render(null, default);

    /// <summary>
    /// Renders every cached unit, narrowing the Vulkan scissor per unit so a unit whose owner sits inside one or more
    /// <see cref="IUIComponent.ClipToBounds"/> ancestors (a scroll viewport, a clipped panel, a content transition) is
    /// clipped to the intersection of those ancestors' bounds. <paramref name="fullScissor"/> is the window-wide
    /// scissor restored for unclipped units.
    /// </summary>
    public void Render(IGraphicsDevice device, Rect2D fullScissor)
    {
        // Clean-frame replay: nothing changed since the last recorded walk, so re-issue that walk's op stream directly
        // and skip the whole per-unit loop (the retained batch buffers still hold its bytes). Only a fully-Clean build
        // qualifies; a Partial/Full re-runs the walk below and re-records.
        if (device != null && _opsRecorded && _opsReplayable && LastBuildKind == RenderBuildKind.Clean)
        {
            ExecuteOps(device, fullScissor);
            return;
        }

        // Fast-path PARTIAL replay: a geometry-only partial that only recoloured/updated already-batched tiles in place
        // (no splice). Patch just those tiles' slots in the retained batch buffer, then replay the recorded op stream -
        // O(dirty) instead of re-walking every unit (the hover FPS drop). Bails to the full walk below on any doubt.
        // ONLY when nothing MOVED (!LastBuildTransformDirty): ExecuteOps re-draws each batch SEGMENT from its retained GPU
        // bytes (last frame's baked positions) and re-issues per-unit draws from their baked transforms - valid only if the
        // transforms are unchanged. On a TRANSFORM partial (a content slide, a RenderTransform animation) the world moved,
        // so the per-unit draws follow the new transform while the batched fills/text stay at their stale baked positions -
        // the "outline runs ahead of its fill" tear. A move must re-bake everything, so fall through to the full walk.
        if (device != null && _opsRecorded && _opsReplayable && LastBuildKind == RenderBuildKind.Partial
            && !LastBuildTransformDirty && !_partialSpliced && _rectBatch != null && TryPartialReplay(device, fullScissor))
            return;

        // SPLICED partial patch: a dirty control's unit COUNT changed (hover background 0<->1, a live chart re-recording
        // a different number of segments). Its group already re-rendered in place (build side); here the retained BATCH
        // is patched by segment surgery - the group's old slot run is excised from its segment and its re-baked items
        // append as a new segment spliced into the op stream at the same paint position - then the stream replays.
        // O(dirty groups), no tree walk, no other group touched. Falls back to the full walk on anything not yet
        // patchable this way (non-rect-batch groups, capacity, moved transforms).
        if (device != null && _opsRecorded && _opsReplayable && LastBuildKind == RenderBuildKind.Partial
            && !LastBuildTransformDirty && _partialSpliced && _rectBatch != null && TrySplicedPatch(device, fullScissor))
            return;

        var scissorNarrowed = false;   // whether the active scissor is currently narrower than fullScissor

        _recording = device != null;   // a device walk records its op stream for a later clean-frame replay
        if (_recording)
        {
            _ops.Clear(); _opsReplayable = true; _rectSlotByUnit.Clear(); _walkGroup = null; _walkVersion++;
            _nodeAllAware.Clear();
            _movedNodesBuf.Clear();   // a full walk re-bakes fresh node matrices - pending node moves are subsumed
            // ...but "subsumed" is only true if this walk composes CURRENT transforms. A partial build drops the
            // world memos only on a global-transform frame; when the fast path BAILS on a moved node (non-aware content -
            // e.g. a tile that just face-swapped to an image), it bails BEFORE its own memo flush, and this fall-through
            // walk then re-baked the moving subtree at LAST frame's memoized position - a flipping tile froze at the 90°
            // swap angle until any global transform change (a scroll) happened to flush the memo. Clear the WORLD memos
            // (positions) - but NOT the clip memo: a clip is the ClipToBounds ancestors' VIEWPORT rect (a scroll viewport,
            // a panel), which a node's own move never changes, and recomputing it from live ancestor Bounds mid-relayout
            // yielded a transiently-wrong viewport that CULLED on-screen tiles for a frame (the hover "empty cell").
            _worldCache.Clear();
            _relWorldCache.Clear();
        }

        // Text + item-background + instanced-fill batches: reset per frame. Device renders only - GPU-free tests skip batching.
        if (device != null)
        {
            _textBatch ??= new TextBatchCollector();
            _rectBatch ??= new RectBatchCollector();
            _ellipseBatch ??= new EllipseBatchCollector();
            _gradientRectBatch ??= new GradientRectCollector();
            _gradientEllipseBatch ??= new GradientEllipseCollector();
            _textBatch.BeginFrame(device);
            _rectBatch.BeginFrame(device);
            _ellipseBatch.BeginFrame(device);
            _gradientRectBatch.BeginFrame(device);
            _gradientEllipseBatch.BeginFrame(device);
            // Transform table: identity at slot 0 (legacy world-baked instances), (re)sized at this fence-safe point;
            // the SDF collectors read the address per draw (BeginFrame may have reallocated the buffer).
            if (_transformTable == null)
            {
                _transformTable = new TransformTable();
                _transformTable.EnsureResources(device);
                _transformTable.SetMatrix(device, _transformTable.AcquireSlot(Guid.Empty), Matrix4x4F.Identity);
            }
            else
            {
                _transformTable.EnsureResources(device);
            }
            _rectBatch.TransformsAddress = _transformTable.DeviceAddress;
            _ellipseBatch.TransformsAddress = _transformTable.DeviceAddress;
            _gradientRectBatch.TransformsAddress = _transformTable.DeviceAddress;
            _gradientEllipseBatch.TransformsAddress = _transformTable.DeviceAddress;
            _textBatch.TransformsAddress = _transformTable.DeviceAddress;   // glyph VS fetches the block's node matrix by slot
            var sceneClean = LastBuildKind == RenderBuildKind.Clean;
            _textBatch.SceneClean = sceneClean;
            _rectBatch.SceneClean = sceneClean;
            _ellipseBatch.SceneClean = sceneClean;
            _gradientRectBatch.SceneClean = sceneClean;
            _gradientEllipseBatch.SceneClean = sceneClean;
            // Incremental upload: a Clean frame changed nothing, so the batches re-bake byte-identical items into slots the
            // retained GPU buffers already hold - Flush then skips the redundant upload (zero bytes move on an idle frame).
            if (InstancedFillCollector.Enabled)
            {
                _instanceBuffers ??= new GpuBufferManager(device);
                _instancedFill ??= new InstancedFillCollector(device, _instanceBuffers);
                _instancedFill.BeginFrame();
                _instancedFill.SceneClean = sceneClean;
            }
            _batchOpen = false;
        }

        foreach (var group in _groups)
        foreach (var unit in group.Units)
        {
            // Group boundary (recording walks): reset this group's spliced-patch records once per group - they are
            // re-derived by the draw decisions below. Boundary detection instead of an outer block keeps the hot loop flat.
            if (_recording && !ReferenceEquals(group, _walkGroup))
            {
                _walkGroup = group;
                group.RectRuns.Clear();
                group.PatchableRectOnly = true;
                group.WalkVersion = _walkVersion;
            }

            // Read the world transform ONCE (frame-memoized): the bounds-cull below evaluates it and the GPU is re-baked
            // with the very same value before drawing. Using one read for both the cull decision and the re-bake keeps
            // them from diverging (the cull approving "inside" while the GPU draws the element elsewhere = the spill).
            var wt = World(unit.Component);

            var scissor = fullScissor;
            var clipped = false;
            if (device != null)
            {
                scissor = ResolveScissor(unit.Component, wt, fullScissor, out clipped, out var cull);
                // The unit's owner is entirely outside its clip (a virtualized item just off the viewport, content
                // sliding out, etc.): nothing of it is visible, so don't draw it and don't feed it to a batch. The
                // per-surviving-unit scissor is set below.
                if (cull)
                {
                    // A culled unit draws nothing, so its motion node stays PATCHABLE: rewriting the node's matrix
                    // can't desync retained draws that don't exist. Without this, tilting off-viewport tiles (the tilt
                    // FIELD moves every tile, including scrolled-out ones) left their nodes un-aware -> every mouse
                    // frame bailed to a full walk instead of the slot-write fast path.
                    if (_recording && NodeOf(unit.Component) is { } culledNode)
                        _nodeAllAware.TryAdd(culledNode.RenderId, true);
                    continue;
                }
            }

            // Bake AND draw with the same transform the cull just approved: refresh RenderData ONCE here (the batches
            // read its opacity while baking; the per-unit path reuses it). Culled units returned above, so this runs
            // only for units that will actually draw.
            unit.Update(wt, _projectionMatrix, _renderScale);

            // Batches: item-background rects (lower layer) + text (upper layer), each collapsed to one instanced draw.
            // A clip-group change (scissor, or the text atlas) flushes BOTH together, keeping the rect-under-text order;
            // a non-batchable unit that overlaps either batch flushes both first so it paints on top. (A batchable rect
            // drawn explicitly OVER batched text in the SAME clip would layer under it - rare; lists put bg under text.)
            if (device != null && unit is RectangleRenderUnit rru && _rectBatch.CanBatch(rru.RectPayload))
            {
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var bakeWorld = ResolveBake(device, unit.Component, wt, out var slot4Rect);
                if (_rectBatch.TryAdd(rru.RectPayload, bakeWorld, rru.FillOpacity, scissor, LogicalBounds(unit.Component, wt), slot4Rect))
                {
                    if (_recording)
                    {
                        var slot = _rectBatch.LastSlot;
                        _rectSlotByUnit[unit] = slot;   // for a later fast-path partial replay
                        // Extend/open this group's contiguous slot run (for the spliced-patch segment surgery).
                        var runs = group.RectRuns;
                        if (runs.Count > 0 && runs[^1].First + runs[^1].Count == slot)
                            runs[^1] = (runs[^1].First, runs[^1].Count + 1);
                        else
                            runs.Add((slot, 1));
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                // Rejected (rotated/sheared world, or the instance buffer overflowed): a batchable rect built no per-unit
                // machinery, so build its body now and re-bake this frame's transform into it, then it draws below.
                rru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is RectangleRenderUnit grru && _gradientRectBatch.CanBatch(grru.RectPayload))
            {
                // A rounded rect with a LINEAR/RADIAL gradient fill: same SDF-batch family as the solid rect, different
                // pass (the pixel shader evaluates the gradient). Shares the clip group with the other batches.
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var gradBakeWorld = ResolveBake(device, unit.Component, wt, out var slot4Grad);
                if (_gradientRectBatch.TryAdd(grru.RectPayload, gradBakeWorld, grru.FillOpacity, scissor, LogicalBounds(unit.Component, wt), slot4Grad))
                {
                    if (_recording) group.PatchableRectOnly = false;   // gradient: node-aware, but not rect-splice-patchable
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                grru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is EllipseRenderUnit eru && _ellipseBatch.CanBatch(eru.EllipsePayload))
            {
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var bakeWorld = ResolveBake(device, unit.Component, wt, out var slot4El);
                if (_ellipseBatch.TryAdd(eru.EllipsePayload, bakeWorld, eru.FillOpacity, scissor, LogicalBounds(unit.Component, wt), slot4El))
                {
                    if (_recording) group.PatchableRectOnly = false;   // non-rect-batch draw -> not rect-patchable
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                // Rejected (rotated/sheared world, or the instance buffer overflowed): build the body now + re-bake.
                eru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is EllipseRenderUnit geru && _gradientEllipseBatch.CanBatch(geru.EllipsePayload))
            {
                // A full ellipse with a LINEAR/RADIAL gradient fill: gradient sibling of the solid ellipse SDF batch.
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var gradElBakeWorld = ResolveBake(device, unit.Component, wt, out var slot4GradEl);
                if (_gradientEllipseBatch.TryAdd(geru.EllipsePayload, gradElBakeWorld, geru.FillOpacity, scissor, LogicalBounds(unit.Component, wt), slot4GradEl))
                {
                    if (_recording) group.PatchableRectOnly = false;   // gradient: node-aware, but not rect-splice-patchable
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                geru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is TextRenderUnit tru && tru.TextComponent is { } tc && _textBatch.CanBatch(tc, out var atlas))
            {
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || !_textBatch.SameAtlas(atlas))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                // Node-aware, same as the rect batch: glyphs are packed NODE-LOCAL with the node's transform-table slot, so
                // a block under a motion node (a scroll list) rides the O(1) slot-write fast path instead of forcing a
                // full re-bake. ResolveBake returns the node-relative transform + slot (world + slot 0 off any node).
                var textBake = ResolveBake(device, unit.Component, wt, out var slot4Text);
                if (_textBatch.TryAdd(tc, textBake, slot4Text, scissor, atlas, LogicalBounds(unit.Component, wt)))
                {
                    if (_recording) group.PatchableRectOnly = false;   // text is a separate collector, not rect-splice-patchable
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;   // baked into the batch - drawn at the next flush (node-aware: no MarkNodeNotAware)
                }
                // else: rotated/sheared RELATIVE transform or overflow -> fall through to the per-block direct draw below
                // (which re-marks the node not-aware, exactly like a rejected rect)
            }
            else if (device != null && InstancedFillCollector.Enabled && unit is GeometryRenderUnit gru && _instancedFill.CanBatch(gru))
            {
                // General instanced fill (arbitrary tessellated geometry sharing a mesh): collect the fill into the
                // instanced batch and DEFER this unit's fringe/stroke to the flush (drawn over the fill). A clip change
                // flushes the group; the fill lands in its natural z-layer (paint order), not all-at-once.
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                if (_instancedFill.TryAdd(gru, wt, scissor, LogicalBounds(unit.Component, wt)))
                {
                    gru.FillInstanced = true;
                    if (_recording) { group.PatchableRectOnly = false; MarkNodeNotAware(unit.Component); }   // instanced fill: world-baked
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;   // fill batched; fringe/stroke drawn at the flush, over the fill
                }
                // Rejected (no drawable mesh / instance buffer overflow): draw the whole unit per-unit (fill included).
                gru.FillInstanced = false;
            }
            else if (device != null && InstancedFillCollector.Enabled && unit is GeometryRenderUnit ggru && _instancedFill.CanBatchGradient(ggru))
            {
                // General instanced GRADIENT fill (arbitrary geometry with a linear/radial gradient): same instanced path,
                // gradient pass. The unit's fill body is skipped (FillInstanced) and its fringe/stroke draw at the flush.
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                if (_instancedFill.TryAddGradient(ggru, wt, scissor, LogicalBounds(unit.Component, wt)))
                {
                    ggru.FillInstanced = true;
                    if (_recording) { group.PatchableRectOnly = false; MarkNodeNotAware(unit.Component); }   // instanced gradient: world-baked
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                ggru.FillInstanced = false;
            }
            else if (device != null && (_rectBatch.Active || _ellipseBatch.Active || _gradientRectBatch.Active || _gradientEllipseBatch.Active || _textBatch.Active || (_instancedFill?.Active ?? false)))
            {
                // A non-batchable unit that overlaps any pending batch: flush them first so this unit paints OVER them,
                // as its later source order requires. Spatially disjoint units (a list's items) don't flush.
                var lb = LogicalBounds(unit.Component, wt);
                if (_rectBatch.OverlapsPending(lb) || _ellipseBatch.OverlapsPending(lb) || _gradientRectBatch.OverlapsPending(lb) || _gradientEllipseBatch.OverlapsPending(lb) || _textBatch.OverlapsPending(lb) || (_instancedFill?.OverlapsPending(lb) ?? false))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
            }
            else if (device == null && unit is RectangleRenderUnit rruNoDev)
            {
                // No device = the overlay (popup / tooltip) path: it draws each unit individually via unit.Render() below,
                // with NO batching (the batch collectors are never begun here). A batchable fill builds no per-unit
                // machinery (it expects to be batched), so without building it now its unit.Render() draws NOTHING - which
                // is why a tooltip badge's background vanished while its text (a non-batched unit) still showed. Build the
                // body + re-bake this frame's transform, exactly as the batch-rejected path does above.
                rruNoDev.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device == null && unit is EllipseRenderUnit eruNoDev)
            {
                eruNoDev.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }

            if (device != null)
            {
                if (clipped)
                {
                    device.SetScissors(scissor);
                    scissorNarrowed = true;
                    RecordScissor(scissor);
                }
                else if (scissorNarrowed)
                {
                    // First unclipped unit after a clipped one (or after a flush): restore the full window scissor.
                    device.SetScissors(fullScissor);
                    scissorNarrowed = false;
                    RecordScissor(fullScissor);
                }
            }

            if (_recording) { group.PatchableRectOnly = false; MarkNodeNotAware(unit.Component); }   // per-unit draw: world-baked RenderData
            unit.Render();
            if (_recording) _ops.Add(new RenderOp { Kind = RenderOpKind.Unit, Unit = unit });
        }

        // Drain the tail batches (rects under fills under text), then leave the device on the full scissor for next pass.
        if (device != null) FlushBatches(device, fullScissor, ref scissorNarrowed);
        if (scissorNarrowed) { device.SetScissors(fullScissor); RecordScissor(fullScissor); }

        if (_recording)
        {
            _opsRecorded = true; _recording = false;
        }
    }

    private void RecordScissor(Rect2D scissor)
    {
        if (_recording) _ops.Add(new RenderOp { Kind = RenderOpKind.Scissor, Scissor = scissor });
    }

    // Draw a fast-path partial by patching only the dirty tiles' batch slots, then replaying last frame's op stream.
    // Returns false (caller falls back to the full walk) if ANY dirty unit isn't a still-batchable rect we recorded a slot
    // for - its bytes live elsewhere (a per-unit / text / instanced unit, or a tile that just switched to a gradient).
    // Validate fully BEFORE patching so a rejected frame leaves no half-applied slots the fallback wouldn't overwrite.
    private bool TryPartialReplay(IGraphicsDevice device, Rect2D fullScissor)
    {
        // Moved motion nodes first: rewrite their table matrices (64B each) so the replayed segments draw the scrolled
        // subtrees at their new position. A moved node with non-node-aware retained content bails to the full walk.
        if (!RefreshMovedNodes(device)) return false;

        foreach (var comp in _partialDirty)
        {
            // A dirty component with NO drawn units (a detached/pooled/collapsed element - e.g. a text block that
            // re-marks geometry every frame but isn't in the paint tree) contributes nothing to the frame: the op stream
            // is unchanged, so skip it and let the replay stand. This is the common hover case (nothing visible changed).
            if (!_groupById.TryGetValue(comp.RenderId, out var g)) continue;
            foreach (var u in g.Units)
                if (u is not RectangleRenderUnit rru || !_rectSlotByUnit.ContainsKey(u) || !_rectBatch.CanBatch(rru.RectPayload))
                    return false;   // a per-unit / text / instanced / no-longer-batchable dirty unit -> full walk
        }
        // Nothing moved on a geometry-only partial, so the cached world transform is still valid; re-bake each dirty tile
        // from its (just-updated) payload into its retained slot. (No-units components patched nothing above.)
        foreach (var comp in _partialDirty)
        {
            if (!_groupById.TryGetValue(comp.RenderId, out var g)) continue;
            foreach (var u in g.Units)
            {
                var rru = (RectangleRenderUnit)u;
                var bakeWorld = ResolveBake(device, u.Component, World(u.Component), out var slot);
                if (!RectBatchCollector.BakeItem(rru.RectPayload, bakeWorld, rru.FillOpacity, slot, out var item))
                    return false;   // became non-bakeable (rotated); the full walk re-bakes everything anyway
                _rectBatch.UpdateSlot(device, _rectSlotByUnit[u], item);
            }
        }
        ExecuteOps(device, fullScissor);
        return true;
    }

    // One dirty group's staged patch (validated + baked BEFORE any mutation, so a bail leaves the retained frame intact).
    private struct GroupPatch
    {
        public ControlGroup Group;
        public RectItem[] Items;      // re-baked instances of the group's (non-culled) units, in unit order
        public Rect2D Scissor;        // the group's clip (all units of one component share it)
        public bool InPlace;          // count-stable recolor -> per-slot UpdateSlot, no surgery
    }

    // Draw a SPLICED partial by per-group batch-segment surgery + op-stream splice, then replay - the O(dirty-control)
    // path for unit-count changes (hover 0<->1 backgrounds, a live chart). Requirements per dirty group (else return
    // false BEFORE mutating anything -> the caller full-walks): every unit rect-batchable NOW; a group with retained
    // runs must have been rect-only on the last recording walk (its old draws are then fully described by RectRuns).
    // A spliced patch APPENDS the re-baked group at the arena's end (abandoning its old slots) and INSERTS segment ops
    // into the retained stream - neither is reclaimed until a full walk resets Count/_ops at BeginFrame. A sustained
    // burst (hovering across a list, every item's hover-background toggling = a count-change splice) therefore grows the
    // arena and the op stream without bound (measured: ops 30 -> 1300+), and replaying a 1300-op stream every frame is
    // both slow and increasingly fragile (a stale/duplicated segment op mis-draws a cell for a frame). Cap the op stream:
    // once it grows past this, the splice yields to a full walk - its designed fallback - which recompacts the arena and
    // re-records a clean, minimal stream. The fast path still serves normal short bursts (a few hovers).
    private const int MaxRetainedOps = 256;

    private readonly List<GroupPatch> _patchBuf = new();
    private bool TrySplicedPatch(IGraphicsDevice device, Rect2D fullScissor)
    {
        // Moved motion nodes first (same as TryPartialReplay): rewrite their matrices, bail on non-aware content.
        if (!RefreshMovedNodes(device)) return false;

        // Op stream grown too long from accumulated splices -> recompact with a full walk before it mis-replays.
        if (_ops.Count > MaxRetainedOps) return false;

        // ---- Phase 1: validate + bake (no mutation) ----
        _patchBuf.Clear();
        var appendTotal = 0;
        foreach (var comp in _partialDirty)
        {
            if (!_groupById.TryGetValue(comp.RenderId, out var group)) continue;   // no drawn units - contributes nothing

            // A group's RectRuns are valid ONLY relative to the arena the LAST recording walk (or a splice that ran under
            // it) built. When a group's WalkVersion is stale, the last full walk did NOT visit it (it was recycled /
            // scrolled off / re-appeared since) and its slots have been REASSIGNED to whatever the walk recorded in their
            // place. Its RectRuns now point at OTHER groups' slots, so excising them (phase 2) would remove a live
            // neighbour's slot from its segment - it draws blank for a frame until a full walk recompacts (the hover
            // "blink": a stale run at [102+1] excising group 464954's slot 102). A stale group has nothing of its own to
            // excise: drop its runs and let it re-append fresh. (A splice re-append below re-stamps WalkVersion, so a
            // group that re-appended this cycle is NOT treated as stale on the next splice.)
            var walked = group.WalkVersion == _walkVersion;
            if (!walked) group.RectRuns.Clear();
            var runTotal = 0;
            foreach (var r in group.RectRuns) runTotal += r.Count;
            // A group DESCRIBED by the last recording walk must have been rect-only - otherwise it also drew per-unit /
            // text / instanced content whose recorded ops we can't excise (stale Unit ops would even replay disposed units).
            if (walked && !group.PatchableRectOnly) return false;

            var items = new List<RectItem>(group.Units.Count);
            var scissor = fullScissor;
            var haveScissor = false;
            foreach (var u in group.Units)
            {
                if (u is not RectangleRenderUnit rru || !_rectBatch.CanBatch(rru.RectPayload)) return false;
                var wt = World(u.Component);
                if (!haveScissor)
                {
                    scissor = ResolveScissor(u.Component, wt, fullScissor, out _, out var cull);
                    haveScissor = true;
                    if (cull) break;   // whole component off-clip: it contributes no items (units share the component)
                }
                var bakeWorld = ResolveBake(device, u.Component, wt, out var slot);
                if (!RectBatchCollector.BakeItem(rru.RectPayload, bakeWorld, rru.FillOpacity, slot, out var item)) return false;
                items.Add(item);
            }

            // Count-stable recolor (every unit already holds a retained slot): patch in place, no surgery/fragmentation.
            var inPlace = items.Count == group.Units.Count && items.Count == runTotal && AllUnitsHaveSlots(group);
            if (!inPlace) appendTotal += items.Count;
            _patchBuf.Add(new GroupPatch { Group = group, Items = items.ToArray(), Scissor = scissor, InPlace = inPlace });
        }

        if (appendTotal > _rectBatch.PatchCapacityLeft) return false;   // arena full - full walk compacts it

        // Every surgery group must have a findable segment for each retained run, and an op referencing it.
        foreach (var p in _patchBuf)
        {
            if (p.InPlace) continue;
            foreach (var run in p.Group.RectRuns)
            {
                var seg = _rectBatch.FindSegmentContaining(run.First);
                if (seg < 0 || FindSegmentOp(seg) < 0) return false;
            }
        }

        // ---- Phase 2: mutate (can no longer fail) ----
        foreach (var p in _patchBuf)
        {
            var group = p.Group;
            if (p.InPlace)
            {
                var i = 0;
                foreach (var u in group.Units)
                    _rectBatch.UpdateSlot(device, _rectSlotByUnit[u], p.Items[i++]);
                continue;
            }

            // Excise the old runs: each segment shrinks to its 'before' part and the 'after' remainder becomes a new
            // segment whose op is inserted right after the original - [before][after] keeps every other item's order.
            // The FIRST run's position is remembered as the insertion anchor so the group's new items draw at the same
            // paint position they had.
            var anchorOp = -1;
            foreach (var run in group.RectRuns)
            {
                var seg = _rectBatch.FindSegmentContaining(run.First);
                var opIdx = FindSegmentOp(seg);
                var after = _rectBatch.ExcludeRun(seg, run.First, run.Count);
                if (after >= 0)
                    _ops.Insert(opIdx + 1, new RenderOp { Kind = RenderOpKind.Segment, Batch = 0, SegIndex = after });
                if (anchorOp < 0) anchorOp = opIdx;
            }

            if (anchorOp < 0 && p.Items.Length > 0)
            {
                // 0 -> N (first units this control ever batched): anchor at the paint position of the nearest FOLLOWING
                // group with a retained rect run, splitting that run's segment at the run start so our new segment's op
                // sits between "everything painted before that group" and that group - i.e. exactly at our paint rank.
                // No such successor -> fall back to the nearest PRECEDING group's run (insert after its segment op).
                // A mid-phase-2 bail here is SAFE: every already-patched group's surgery is self-consistent (excised
                // runs + appended segment + spliced ops), and the caller's full walk re-records the whole frame anyway.
                if (!TryAnchorByNeighbour(group, out anchorOp)) return false;
            }

            if (p.Items.Length > 0)
            {
                var newSeg = _rectBatch.AppendPatchSegment(device, p.Items, p.Scissor);
                var newFirst = _rectBatch.RetainedCount - p.Items.Length;
                _ops.Insert(anchorOp + 1, new RenderOp { Kind = RenderOpKind.Segment, Batch = 0, SegIndex = newSeg });

                group.RectRuns.Clear();
                group.RectRuns.Add((newFirst, p.Items.Length));
                group.PatchableRectOnly = true;
                group.WalkVersion = _walkVersion;   // its runs now describe THIS arena - not stale on the next splice
                var i = 0;
                foreach (var u in group.Units) _rectSlotByUnit[u] = newFirst + i++;
            }
            else
            {
                group.RectRuns.Clear();   // re-rendered to nothing (a hover background vanishing) - exclusion was enough
                foreach (var u in group.Units) _rectSlotByUnit.Remove(u);
            }
        }

        ExecuteOps(device, fullScissor);
        return true;
    }

    private bool AllUnitsHaveSlots(ControlGroup group)
    {
        foreach (var u in group.Units)
            if (!_rectSlotByUnit.ContainsKey(u)) return false;
        return true;
    }

    // The op index that draws rect-batch segment <paramref name="segIndex"/>.
    private int FindSegmentOp(int segIndex)
    {
        for (var i = 0; i < _ops.Count; i++)
            if (_ops[i].Kind == RenderOpKind.Segment && _ops[i].Batch == 0 && _ops[i].SegIndex == segIndex) return i;
        return -1;
    }

    // Anchor a 0->N group's new segment by its paint rank relative to neighbouring groups' retained rect runs.
    private bool TryAnchorByNeighbour(ControlGroup group, out int anchorOp)
    {
        anchorOp = -1;
        var idx = _groups.IndexOf(group);
        if (idx < 0) return false;

        // Following group with a run: split its run's segment AT the run start; our op goes after the 'before' piece
        // (and the successor's items keep drawing after us via the split-off remainder).
        for (var g = idx + 1; g < _groups.Count; g++)
        {
            var runs = _groups[g].RectRuns;
            if (runs.Count == 0) continue;
            var seg = _rectBatch.FindSegmentContaining(runs[0].First);
            if (seg < 0) return false;
            var opIdx = FindSegmentOp(seg);
            if (opIdx < 0) return false;
            var after = _rectBatch.ExcludeRun(seg, runs[0].First, 0);   // pure split - nothing excluded
            if (after >= 0)
                _ops.Insert(opIdx + 1, new RenderOp { Kind = RenderOpKind.Segment, Batch = 0, SegIndex = after });
            anchorOp = opIdx;
            return true;
        }

        // No successor: insert after the nearest preceding group's run segment op.
        for (var g = idx - 1; g >= 0; g--)
        {
            var runs = _groups[g].RectRuns;
            if (runs.Count == 0) continue;
            var lastRun = runs[^1];
            var seg = _rectBatch.FindSegmentContaining(lastRun.First);
            if (seg < 0) return false;
            var opIdx = FindSegmentOp(seg);
            if (opIdx < 0) return false;
            anchorOp = opIdx;
            return true;
        }
        return false;
    }

    // Replay a recorded frame's op stream (a Clean frame): re-issue its scissor changes, per-unit direct draws and batch
    // segment draws in order. No walk, no bake, no upload - the batch GPU buffers still hold last frame's bytes, and each
    // unit's RenderData still holds last frame's baked transform (nothing moved on a Clean frame).
    private void ExecuteOps(IGraphicsDevice device, Rect2D fullScissor)
    {
        foreach (var op in _ops)
        {
            switch (op.Kind)
            {
                case RenderOpKind.Scissor:
                    device.SetScissors(op.Scissor);
                    break;
                case RenderOpKind.Unit:
                    op.Unit.Render();
                    break;
                case RenderOpKind.Segment:
                    switch (op.Batch)
                    {
                        case 0: _rectBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                        case 1: _ellipseBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                        case 3: _gradientRectBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                        case 4: _gradientEllipseBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                        default: _textBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                    }
                    break;
                case RenderOpKind.InstancedFlush:
                    _instancedFill.ReplayFlush(op.SegIndex, fullScissor, _projectionMatrix);
                    break;
            }
        }
    }

    // A unit's own viewport (local 0,0..RenderSize) mapped into window-logical space - the same box ResolveScissor
    // clips against, reused here for the batches' paint-order overlap test.
    private Rect LogicalBounds(IUIComponent component, Matrix4x4F worldTransform)
    {
        var size = ApplySnap(component).RenderSize;
        return new Rect(0, 0, size.Width, size.Height).TransformToAABB(worldTransform);
    }

    // Flush all batches in LAYER order - item-background rects, then instanced geometry fills (+ their deferred
    // fringe/stroke), then text on top - and mark the group closed. Each Flush leaves the device on fullScissor, so the
    // per-unit scissor state resets to "not narrowed".
    private void FlushBatches(IGraphicsDevice device, Rect2D fullScissor, ref bool scissorNarrowed)
    {
        RecordSegment(0, _rectBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(1, _ellipseBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(3, _gradientRectBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(4, _gradientEllipseBatch.Flush(device, fullScissor, _projectionMatrix));
        // The general instanced-fill flush (each key's instances + the collected units' deferred fringe/stroke) is
        // retained too: Flush records the group and returns its index, which the op stream replays via ReplayFlush - so
        // a vector icon no longer disables replay for the whole window.
        if (_instancedFill != null)
        {
            var fi = _instancedFill.Flush(fullScissor, _projectionMatrix);
            if (_recording && fi >= 0) _ops.Add(new RenderOp { Kind = RenderOpKind.InstancedFlush, SegIndex = fi });
        }
        RecordSegment(2, _textBatch.Flush(device, fullScissor, _projectionMatrix));
        scissorNarrowed = false;
        _batchOpen = false;
    }

    // Record a batch segment op (the immediate draw already happened inside Flush; this only appends it to the op stream
    // for a later clean-frame replay). A Flush that drew nothing returns -1 and records nothing.
    private void RecordSegment(byte batch, int segIndex)
    {
        if (_recording && segIndex >= 0)
            _ops.Add(new RenderOp { Kind = RenderOpKind.Segment, Batch = batch, SegIndex = segIndex });
    }

    private static bool ScissorEquals(Rect2D a, Rect2D b)
        => a.Offset.X == b.Offset.X && a.Offset.Y == b.Offset.Y
           && a.Extent.Width == b.Extent.Width && a.Extent.Height == b.Extent.Height;

    // The scissor for a unit: the intersection of every ancestor viewport that ClipToBounds (in framebuffer pixels),
    // or fullScissor if none clip. `clipped` is false in the latter case so the caller keeps the window scissor.
    // `cull` is true when the unit's own bounds fall ENTIRELY outside that clip (so the caller skips drawing it).
    private Rect2D ResolveScissor(IUIComponent component, Matrix4x4F worldTransform, Rect2D fullScissor, out bool clipped, out bool cull)
    {
        // Intersection of every ClipToBounds ancestor's viewport - memoized per component (all units under one clipping
        // ancestor share it), so a 180-item list resolves the shared clip ONCE, not 180 times.
        var clip = CumulativeClip(component);

        cull = false;
        if (clip is not { } logical)
        {
            clipped = false;
            return fullScissor;
        }

        // A PERSPECTIVE world (a 3D-rotated tile: M34/M14/M24 carry the w term) cannot be bounds-tested by the affine
        // AABB below - TransformToAABB does no w-divide, so the box comes out garbage and the tilted tile was culled
        // (it VANISHED mid-flip). Perspective content is rare and pixel-clipped by the scissor anyway: skip the cull.
        if (worldTransform.M34 != 0 || worldTransform.M14 != 0 || worldTransform.M24 != 0)
        {
            clipped = true;
            return ToFramebufferScissor(logical, fullScissor);
        }

        // A unit under a render MOTION NODE (a scrolled panel's item) is drawn through the node's transform-table matrix,
        // which the O(1)-scroll replay REWRITES every frame WITHOUT re-recording the op stream. So its record-time world
        // is NOT where later frames draw it: as the node scrolls, an off-viewport BUFFER row (realized ahead so it can
        // slide in seamlessly) translates INTO view under the very same recorded op. Culling it here (its current world
        // is still below the fold) drops it from the recorded stream, so the replay leaves it blank until a full walk
        // re-records - the row "materialising" a frame late as it scrolls in. Don't cull motion-node units: the scissor
        // still clips them to the viewport, and the realized window is bounded (viewport + a couple of buffer rows), so
        // recording the few off-screen ones is cheap and the replay can slide them in already-drawn.
        if (NodeOf(component) != null)
        {
            clipped = true;
            return ToFramebufferScissor(logical, fullScissor);
        }

        // Is the unit's own owner fully outside the clip on any axis? Then none of it shows -> let the caller cull it.
        // Use the SAME world transform the caller will bake into the GPU draw, not a fresh component.WorldTransform read:
        // layout runs on another thread, so a re-read here could differ from what is actually drawn (cull says "inside"
        // while the GPU paints it outside -> the off-viewport spill).
        var scissorSize = ApplySnap(component).RenderSize;
        var bounds = new Rect(0, 0, scissorSize.Width, scissorSize.Height).TransformToAABB(worldTransform);
        cull = bounds.Right <= logical.X || bounds.X >= logical.Right || bounds.Bottom <= logical.Y || bounds.Y >= logical.Bottom;

        clipped = true;
        return ToFramebufferScissor(logical, fullScissor);
    }

    // Window-logical rect -> Vulkan scissor in framebuffer pixels (logical x RenderScale), clamped to the window
    // scissor so it never exceeds the attachment and collapses to empty (nothing drawn) when fully scrolled out.
    private Rect2D ToFramebufferScissor(Rect logical, Rect2D fullScissor)
    {
        var fbLeft = fullScissor.Offset.X;
        var fbTop = fullScissor.Offset.Y;
        var fbRight = fbLeft + (int)fullScissor.Extent.Width;
        var fbBottom = fbTop + (int)fullScissor.Extent.Height;

        var left = Math.Clamp((int)Math.Floor(logical.X * _renderScale), fbLeft, fbRight);
        var top = Math.Clamp((int)Math.Floor(logical.Y * _renderScale), fbTop, fbBottom);
        var right = Math.Clamp((int)Math.Ceiling(logical.Right * _renderScale), fbLeft, fbRight);
        var bottom = Math.Clamp((int)Math.Ceiling(logical.Bottom * _renderScale), fbTop, fbBottom);

        return new Rect2D
        {
            Offset = new Offset2D { X = left, Y = top },
            Extent = new Extent2D { Width = (uint)Math.Max(0, right - left), Height = (uint)Math.Max(0, bottom - top) }
        };
    }
    
    // Re-derive every component's paint-order rank WITHOUT rendering (no Render, no unit build) - the same DFS + paint
    // order as the full walk, just assigning _orderByControl. Used when a component appears mid-partial (needs a rank to
    // be spliced into the retained paint-order list) so we don't have to re-render the whole tree to place one element.
    private void ReassignOrders()
    {
        if (_lastVisualRoot == null) return;
        // Fresh map + publish, for the same reason as RecordFullWalk: the applier holds the previous one by reference.
        _orderByControl = new Dictionary<Guid, int>(_orderByControl.Count);
        _packet.Orders = _orderByControl;
        _orderStack.Clear();
        _orderVisited.Clear();
        var order = 0;
        _orderStack.Push(_lastVisualRoot);
        while (_orderStack.Count > 0)
        {
            var component = _orderStack.Pop();
            if (component.Visibility != Visibility.Visible) continue;
            if (!_orderVisited.Add(component.RenderId)) continue;
            _orderByControl[component.RenderId] = order++;
            PushChildrenInPaintOrder(_orderStack, component.VisualChildren);
        }
    }

    // RECORD half of the full walk (DEVICE-FREE): DFS the visual tree, run component.Render to produce this frame's draw
    // commands, and COPY each component's commands into the packet in paint order (the shared drawing context is reused for
    // the next component, so the commands must be snapshotted now). No GPU here - no unit build, no buffer alloc; the
    // applier realizes them (ApplyFullWalk). This is what lets the recorder run on the update thread (docs/RENDER_THREAD_PLAN.md).
    private void RecordFullWalk(IRootVisualComponent visualRoot, RenderPacket packet)
    {
        // A FRESH map, never a Clear() of the published one: the applier still holds the previous map by reference (it may be
        // drawing the previous frame on the render thread), so mutating it in place would pull the ranks out from under it.
        _orderByControl = new Dictionary<Guid, int>(_orderByControl.Count);
        packet.Orders = _orderByControl;   // publish this walk's ranks to the applier
        _lastVisualRoot = visualRoot;
        var order = 0;
        packet.ProjectionMatrix = visualRoot.GetProjectionMatrix();
        // Reused, not re-allocated per frame: a full walk runs on EVERY structural frame (a scrolling grid realizes containers
        // constantly), and these grew to one entry per component in the scene - a fresh HashSet rehashing its way up to ~9000
        // entries, every frame. Keyed by the COMPONENT, not its RenderId: hashing a Guid is several times the cost of a
        // reference hash, and the identity is the same either way.
        var stack = _walkStack;
        var visited = _walkVisited;
        stack.Clear();
        visited.Clear();
        stack.Push(visualRoot);
        while (stack.Count > 0)
        {
            var component = stack.Pop();

            if (component.Visibility != Visibility.Visible) continue;

            // A component must render exactly once per frame. If the visual tree somehow makes one reachable twice in
            // this walk (e.g. a templated content host whose child is referenced from two places), processing it again
            // would add its group to the paint order a second time -> every such element is drawn TWICE (overdraw at the
            // same spot). Guard against that here so each component (and its subtree) is built once.
            if (!visited.Add(component)) continue;

            _orderByControl[component.RenderId] = order++;   // paint-order rank (for the incremental partial-patch)

            // Capture dirtiness BEFORE Render: a clean control's Render() is a no-op (records nothing),
            // so an empty command list means "reuse the cached units". A dirty control re-records, so an
            // empty list then means "this control now draws nothing" and its stale units must be cleared.
            var wasGeometryValid = component.IsGeometryValid;

            _drawingContextInternal.Clear();
            component.Render(_drawingContext);
            var commands = CopyCommands(_drawingContextInternal.GetDrawCommands());
            packet.Draws.Add(new ComponentDraw(component, commands, wasGeometryValid));
            MirrorUnits(component, commands.Count, wasGeometryValid);

            PushChildrenInPaintOrder(stack, component.VisualChildren);
        }

        // Mirror the applier's ReconcileDetachedControls: it frees the units of every control no longer in the tree, so the
        // recorder's own view of "who holds units" must drop them too (this walk never visits them - they aren't in the tree).
        _staleUnitIds.Clear();
        foreach (var (id, entry) in _recordedUnits)
            if (!entry.Component.IsAttachedToVisualTree) _staleUnitIds.Add(id);
        foreach (var id in _staleUnitIds) _recordedUnits.Remove(id);
    }

    // APPLY half of the full walk (GPU / render-thread side): rebuild the paint-order groups from the recorded draws -
    // create/update/free the retained units per component (BuildUnitsFor via ProcessRenderCommands), then reclaim any
    // control that dropped off the tree.
    private void ApplyFullWalk(RenderPacket packet)
    {
        _groups.Clear();
        foreach (var draw in packet.Draws)
            ProcessRenderCommands(draw.Component, draw.Commands, packet.ProjectionMatrix, draw.WasGeometryValid);
        ReconcileDetachedControls();
    }

    // Snapshot a component's just-recorded draw commands (the shared drawing context is reused for the next component).
    // Empty -> a shared empty array (a clean control that recorded nothing, or one that now draws nothing). Allocates per
    // NON-empty component per FULL walk (rare - structural changes only); poolable later.
    private static IReadOnlyList<IDrawCommand> CopyCommands(IReadOnlyList<IDrawCommand> commands)
    {
        if (commands.Count == 0) return Array.Empty<IDrawCommand>();
        var copy = new IDrawCommand[commands.Count];
        for (var i = 0; i < commands.Count; i++) copy[i] = commands[i];
        return copy;
    }

    // Push a component's children so the stack pops them in PAINT order (drawn first = underneath). Fast path (the norm):
    // no explicit ZIndex -> natural document order (push reversed so child 0 pops first). Otherwise composite by ZIndex
    // then document order - the same precedence the hit-test's ZSort uses - so a raised child (e.g. a tab mid-drag) draws
    // over its siblings.
    private static void PushChildrenInPaintOrder(Stack<IUIComponent> stack, IReadOnlyCollection<IUIComponent> children)
    {
        var anyZ = false;
        foreach (var child in children)
            if (child.ZIndex != 0) { anyZ = true; break; }

        if (!anyZ)
        {
            // Reverse WITHOUT allocating (children.Reverse() buffers them all): the concrete VisualChildren is a
            // ReadOnlyCollection (an IReadOnlyList), so walk it back-to-front by index. This runs per component on every
            // full walk, so the per-call array Enumerable.Reverse allocated was per-component-per-walk garbage.
            if (children is IReadOnlyList<IUIComponent> list)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                    stack.Push(list[i]);
            }
            else
            {
                foreach (var child in children.Reverse())
                    stack.Push(child);
            }
            return;
        }

        foreach (var child in children
                     .Select((child, index) => (child, index))
                     .OrderByDescending(x => x.child.ZIndex)
                     .ThenByDescending(x => x.index)
                     .Select(x => x.child))
        {
            stack.Push(child);
        }
    }

    // Refresh a component's cached units from its freshly recorded draw commands: reuse a still-matching unit in place,
    // replace a type-changed one, create the extra, dispose the surplus. Mutates the component's GROUP in place but does
    // NOT touch the paint order (_groups) - the caller places a NEW group (full build appends; a partial patch inserts
    // by DFS rank); an existing group already sits at its spot and its Units list is the very list refreshed here.
    private ControlGroup BuildUnitsFor(IUIComponent component, IReadOnlyList<IDrawCommand> drawCommands, Matrix4x4F projectionMatrix)
    {
        if (!_groupById.TryGetValue(component.RenderId, out var group))
        {
            group = new ControlGroup { ControlId = component.RenderId };
            _groupById[component.RenderId] = group;
        }

        var units = group.Units;
        for (int i = 0; i < drawCommands.Count; i++)
        {
            var command = drawCommands[i];
            command.RenderData.ProjectionMatrix = projectionMatrix;
            if (i >= units.Count)
            {
                units.Add(_renderUnitFactory.CreateRenderUnitFromCommand(command));
            }
            else
            {
                var unit = units[i];
                if (unit.Match(command))
                {
                    unit.UpdateWithDrawCommand(command);
                }
                else
                {
                    unit.DeferDispose();
                    units[i] = _renderUnitFactory.CreateRenderUnitFromCommand(command);
                }
            }
        }

        if (units.Count > drawCommands.Count)
        {
            for (int i = drawCommands.Count; i < units.Count; i++)
                units[i].DeferDispose();
            units.RemoveRange(drawCommands.Count, units.Count - drawCommands.Count);
        }

        return group;
    }

    private void ProcessRenderCommands(IUIComponent component, IReadOnlyList<IDrawCommand> drawCommands, Matrix4x4F projectionMatrix, bool wasGeometryValid)
    {
        if (drawCommands.Count > 0)
        {
            _groups.Add(BuildUnitsFor(component, drawCommands, projectionMatrix));
        }
        else
        {
            // No commands this frame. Distinguish the two cases (see wasGeometryValid in BuildRenderCommands):
            //  - was clean: Render() didn't re-record -> reuse the cached units.
            //  - was dirty: the control re-rendered to nothing -> clear its stale units so they stop drawing.
            if (_groupById.TryGetValue(component.RenderId, out var group))
            {
                if (wasGeometryValid)
                {
                    _groups.Add(group);
                }
                else
                {
                    RemoveAndDeferDispose(component.RenderId);
                }
            }
        }
    }

    /// <summary>
    /// Frees the cached units of any control no longer attached to the visual tree. Must run during the build
    /// (EndDraw): disposal is deferred on the current frame slot and drained M frames later, so calling it earlier
    /// (e.g. from the detach event during Update) would dispose a unit still in flight. The root visual is kept
    /// even though it is always rendered - it reports attached via its own <see cref="IUIComponent.RootVisual"/>.
    /// Attachment, not visibility, is the keep signal, so hidden-but-attached controls retain their resources.
    /// </summary>
    private void ReconcileDetachedControls()
    {
        List<Guid> detached = null;
        foreach (var pair in _groupById)
        {
            var units = pair.Value.Units;
            if (units.Count == 0
                || units[0].Component.IsAttachedToVisualTree) continue;
            (detached ??= new List<Guid>()).Add(pair.Key);
        }

        if (detached == null) return;
        foreach (var id in detached)
            RemoveAndDeferDispose(id);
    }

    /// <summary>
    /// Drops the cache entry and defer-disposes its units (deferred until the frame fence signals, as the GPU may
    /// still be using them). Build-phase only (EndDraw). Idempotent. The group is also dropped from the paint order -
    /// on a full walk that just rebuilt _groups this is a no-op miss; on any other path it keeps order and cache in sync.
    /// </summary>
    private void RemoveAndDeferDispose(Guid renderId)
    {
        if (!_groupById.Remove(renderId, out var group)) return;

        foreach (var unit in group.Units)
            unit?.DeferDispose();
        _groups.Remove(group);
    }
}