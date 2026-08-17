using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Rendering.Retained;
using Adamantium.UI.Rendering.RenderUnits;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

public partial class RenderCache
{
    // Item-background batch (solid rounded-rect fills -> one SDF-AA'd instanced draw). Rects are the LOWER layer
    // (FlushBatches draws rects THEN text). Both batches share one clip GROUP (_batchScissor): a scissor (or text-atlas)
    // change flushes both together, preserving order.
    private RectBatchCollector _rectBatch;
    private EllipseBatchCollector _ellipseBatch;   // SDF family, same fill layer as rects (below text)

    // GPU-resident transform table (one world matrix per MOTION NODE; slot 0 = identity for legacy world-space bakes). The
    // SDF vertex shaders fetch each instance's matrix by slot, so moving a node costs ONE matrix write instead of re-baking
    // its instances - and rotated/3D instances stay batched. Owned per cache; initialised in the Render device block.
    private TransformTable _transformTable;

    // Set only while a clone run is being drawn (§4o); null on every ordinary group, which is what keeps the hot loop
    // paying one null check instead of a matrix multiply.
    private Matrix4x4F? _cloneMatrix;

    /// <summary>Index just past the prototype's subtree in <see cref="_groups"/>. Paint rank is DFS order, so a subtree
    /// is a CONTIGUOUS run: scan forward while each group's control is a visual descendant of the prototype.</summary>
    private int CloneSubtreeEnd(int start)
    {
        var prototype = _groups[start].Component;
        var end = start + 1;
        while (end < _groups.Count && IsVisualDescendantOf(_groups[end].Component, prototype)) end++;
        return end;
    }

    private static bool IsVisualDescendantOf(IUIComponent node, IUIComponent ancestor)
    {
        for (var p = node?.VisualParent; p != null; p = p.VisualParent)
        {
            if (ReferenceEquals(p, ancestor)) return true;
        }

        return false;
    }
    private GradientRectCollector _gradientRectBatch;   // SDF family: rounded rects with a linear/radial GRADIENT fill
    private GradientEllipseCollector _gradientEllipseBatch;   // SDF family: ellipses with a linear/radial GRADIENT fill
    private PatternRectCollector _patternBatch;   // SDF family: rounded rects with a PROCEDURAL pattern fill (checker/stripes/dots/grid)
    private FractalRectCollector _fractalBatch;   // SDF family: rounded rects with an escape-time FRACTAL fill (Julia/Mandelbrot)
    private TexRectCollector _texRectBatch;   // SDF family: rounded rects whose fill is SAMPLED from a texture (ImageBrush / NineSliceBrush)
    // The soft band (aura / shadow) in its TWO paint positions. An OUTER band goes under every fill; an INNER one over
    // them - drawn under, it would simply be covered by the shape's own fill. Both lazy: most windows have neither.
    private HaloRectCollector _haloUnder;
    private HaloRectCollector _haloOver;
    // The LIVING aura rides its own pass, so it has its own pair - flushed right beside their still twins.
    private HaloLivingCollector _haloLivingUnder;
    private HaloLivingCollector _haloLivingOver;
    // Whose inner band is pending. Its OWN fill must not flush it - the fill is added right after the band and the two
    // belong together; anyone ELSE overlapping it still forces a flush, or a later sibling would be painted over.
    private IUIComponent _haloOverOwner;
    private Rect2D _batchScissor;
    private bool _batchOpen;

    // General instanced fills (arbitrary tessellated geometry sharing a mesh), flushed in PAINT ORDER via FlushBatches.
    // Own buffer manager, distinct from the per-unit geometry buffers.
    private GpuBufferManager _instanceBuffers;
    private InstancedFillCollector _instancedFill;

    // --- Retained draw (clean-frame op replay) ---
    // A Clean frame would re-bake byte-identical items and re-issue identical draws for thousands of units (~15 ms idle
    // floor on the 60k-tile view -> ~0.8 ms replayed). Instead every NON-clean frame RECORDS the ordered GPU op stream the
    // walk emits (scissor changes, per-unit draws, batch segments, instanced flushes), and the next Clean frame REPLAYS it:
    // the retained batch/instance buffers still hold last frame's bytes (BeginFrame skipped, uploads skipped by SceneClean).
    // _opsReplayable is the escape hatch for a draw type the flat stream can't reproduce (there is none today).
    private enum RenderOpKind : byte { Scissor, Unit, Segment, InstancedFlush }
    private struct RenderOp
    {
        public RenderOpKind Kind;
        public Rect2D Scissor;    // Scissor
        public IRenderUnit Unit;  // Unit
        public byte Batch;        // Segment: which collector (0 rect, 1 ellipse, 2 text, 3 gradient-rect, 4 gradient-ellipse, 5 pattern, 6 fractal, 7 textured)
        public int SegIndex;      // Segment: index into that collector's recorded segment list; InstancedFlush: flush index

        // Paint rank of the group being recorded when this op was emitted. The stream is written in rank order, so a
        // control that starts drawing later knows exactly where its op belongs - by its OWN rank, not by guessing from
        // whatever happens to sit next to it. Guessing is what made an unrelated diagnostics label in the corner decide
        // whether hovering a tile cost 0.9 ms or a full walk.
        public long Order;

        // A SEGMENT is not one rank - it glues the rects of EVERY control that fell between two flushes, so it covers the
        // SPAN [OrderFirst, Order]. Insertion needs both ends: a newcomer whose rank lands strictly inside the span cannot
        // be placed before or after that op at all (either way it jumps over somebody), and the frame has to say so
        // instead of drawing it in the wrong layer. Recorded as Order until proven otherwise, so a non-segment op's span
        // is just its own rank.
        public long OrderFirst;
    }
    private readonly List<RenderOp> _ops = new();
    private long _recordOrder;   // paint rank of the group currently being recorded - stamped onto every op it emits

    // Where the rect batch's PENDING segment's paint span begins: the rank of the first group that put something in it.
    // Noted while the segment is still empty, because afterwards there is nothing left to read it from - and a splice that
    // places a newcomer needs the span's START, not just where it ended (see PlaceNewSegment).
    private long _rectSegStart;

    // The transform-table version the op stream was recorded against, and whether it still holds. A recorded stream bakes
    // THREE things against the transforms of its own frame: each Scissor op (a world-space rect), each per-unit draw (its
    // full world, baked into RenderData - see ExecuteOps) and the batch segments (which follow their slot matrix LIVE).
    // Once a matrix moves, those three no longer agree: the batched fill follows, the per-unit outline and the clip do
    // not. Replaying then draws a frame that never existed - a clip one frame stale, a fill sliding out from under its
    // own outline - which is exactly the flicker, and why it only shows with a render thread: that is when frames are
    // replayed many times between records (measured: the flicker disappears the moment clean replay is disabled).
    private ulong _opsMatrixVersion;

    // Set by the applier when a packet folds a new layout snapshot in; cleared when a walk re-records the stream against
    // that layout. While it is set, the retained stream describes an older frame than the snapshot does.
    private bool _layoutChangedSinceRecord;

    private bool OpsMatchTransforms =>
        !_layoutChangedSinceRecord && (_transformTable == null || _transformTable.MatrixVersion == _opsMatrixVersion);
    private bool _recording;       // this frame runs the walk and is appending ops
    private bool _opsRecorded;     // _ops holds a complete frame from a prior walk
    private bool _opsReplayable;   // the recorded stream faithfully reproduces the frame (currently always true - see above)

    private readonly List<Compositor.Entry> _compositedBuf = new();   // this thread's view of the composited set (reused)
    private readonly Dictionary<IUIComponent, (LayoutSnapshot Snap, Matrix4x4F ParentWorld)> _compositedFallback = new();   // keep animating across a settling swap's snapshot re-capture
    private readonly HashSet<IUIComponent> _compositedOwners = new();   // motion nodes the compositor moved THIS present (ExecuteOps re-Updates their per-unit draws)

    private const int MaxRetainedOps = 256;   // op stream past this -> a splice yields to a full walk that recompacts
    private readonly List<GroupPatch> _patchBuf = new();   // TrySplicedPatch: staged per-group patches (validated before mutation)
    private readonly List<int> _patchLayers = new();     // the LAYERS those patches land in - each re-issued once, whole
    private readonly List<RectItem> _rebakeBuf = new();

    // Picks the transform-table copy this frame writes and draws from, and hands its address to every collector that
    // exists. Called at the very top of Render AND again once the walk has (re)created the collectors, because the
    // address moves with the copy: the shader reads the table through a constant pushed on every draw, so a replay -
    // which re-records its draws but never reaches the walk's setup - must be given this frame's address too, or it
    // would draw last frame's matrices while the moves were being written into the current copy.
    private void BeginTransformFrame(IGraphicsDevice device)
    {
        if (device == null) return;

        if (_transformTable == null)
        {
            _transformTable = new TransformTable();
            _transformTable.EnsureResources(device);
            _transformTable.SetMatrix(device, _transformTable.AcquireSlot(Guid.Empty), Matrix4x4F.Identity);
        }

        // Every clone takes a slot of its own, and a clone run asks for its whole set in ONE frame. The buffer is sized
        // here and nowhere else, so the count has to be known BEFORE it is made - discovering it while baking leaves the
        // overflow unuploaded and the shader reading past the buffer (tiles vanishing and jumping, differently each
        // frame). Cheap: this scans groups, and only a clone host contributes.
        var reserve = 0;
        foreach (var group in _groups)
        {
            if (group.Clones is { Count: > 0 } clones) reserve += clones.Count;
        }

        _transformTable.Reserve(reserve);
        _transformTable.EnsureResources(device);

        var address = _transformTable.DeviceAddress;
        if (_rectBatch != null) _rectBatch.TransformsAddress = address;
        if (_ellipseBatch != null) _ellipseBatch.TransformsAddress = address;
        if (_gradientRectBatch != null) _gradientRectBatch.TransformsAddress = address;
        if (_gradientEllipseBatch != null) _gradientEllipseBatch.TransformsAddress = address;
        if (_patternBatch != null) _patternBatch.TransformsAddress = address;
        if (_fractalBatch != null) _fractalBatch.TransformsAddress = address;
        if (_texRectBatch != null) _texRectBatch.TransformsAddress = address;
        if (_haloUnder != null) _haloUnder.TransformsAddress = address;
        if (_haloOver != null) _haloOver.TransformsAddress = address;
        if (_haloLivingUnder != null) _haloLivingUnder.TransformsAddress = address;
        if (_haloLivingOver != null) _haloLivingOver.TransformsAddress = address;
        if (_textBatch != null) _textBatch.TransformsAddress = address;   // glyph VS fetches the block's node matrix by slot
        if (_instancedFill != null) _instancedFill.TransformsAddress = address;
    }

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

    /// <summary>Renders every cached unit, narrowing the Vulkan scissor per unit to the intersection of its
    /// <see cref="IUIComponent.ClipToBounds"/> ancestors' bounds. <paramref name="fullScissor"/> is restored for unclipped
    /// units.</summary>
    public void Render(IGraphicsDevice device, Rect2D fullScissor)
    {
        // TEMP trace: one array write per frame, dumped from memory by the overlay.
        var traceStart = Core.Diagnostics.FrameTrace.Enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
        LastFrameReplayed = false;
        _traceWhy = 0;
        try
        {
            RenderCore(device, fullScissor);
        }
        finally
        {
            if (Core.Diagnostics.FrameTrace.Enabled)
            {
                var clones = 0;
                foreach (var g in _groups)
                {
                    if (g.Clones is { Count: > 0 } c) clones += c.Count;
                }

                var unitOps = 0;
                foreach (var op in _ops)
                {
                    if (op.Kind == RenderOpKind.Unit) unitOps++;
                }

                Core.Diagnostics.FrameTrace.Add(
                    System.Diagnostics.Stopwatch.GetElapsedTime(traceStart).TotalMilliseconds,
                    (byte)LastBuildKind, LastFrameReplayed, clones, _traceWhy, _traceCacheId, _traceComposited,
                    _ops.Count, unitOps);
            }
        }
    }

    /// <summary>Did the last frame REPLAY the recorded op stream (patching only what changed) instead of walking the tree?
    /// A walk is O(scene) and a replay is O(dirty). Tests assert it, so a regression to "one dirty element re-draws
    /// everything" is caught as a failure rather than as a slower frame nobody notices.</summary>
    public bool LastFrameReplayed { get; private set; }

    /// <summary>Why the last frame WALKED instead of patching: 0 none, 1 nothing recorded, 2 stream unusable, 3 transform
    /// dirty, 4 layout changed since the record, 5 the splice refused, 6 the slot patch refused. A walk is O(scene) where a
    /// patch is O(changed), so every non-zero answer here is a frame that cost the whole scene - and the reason has to be
    /// askable from a test, not only from a live trace.</summary>
    public byte LastWalkReason => _traceWhy;

    private byte _traceWhy;

    // TEMP trace: several caches (one per window, plus each window's adorner layer) write into one ring, so a frame has to
    // say whose it is - otherwise a quiet cache's cheap frames and a busy one's expensive frames read as one bimodal blur.
    private static int _traceNextCacheId;
    private readonly int _traceCacheId = System.Threading.Interlocked.Increment(ref _traceNextCacheId);
    private int _traceComposited;

    private void RenderCore(IGraphicsDevice device, Rect2D fullScissor)
    {
        // This frame's transform-table copy, picked BEFORE anything writes a matrix or draws - the composited animations
        // below write matrices, and the replay paths below draw without ever reaching the walk's setup block.
        BeginTransformFrame(device);

        // The animations this thread plays by itself. BEFORE the clean-frame early-out on purpose: a composited animation
        // changes what the retained op stream draws (a matrix, a re-baked colour slot), so an otherwise CLEAN frame is
        // exactly when it must still apply - the loop can be stalled in a theme cascade and the spinner keeps turning.
        ApplyCompositedAnimations(device);

        // Clean-frame replay: re-issue the last recorded walk's op stream and skip the per-unit loop (the retained buffers
        // still hold its bytes). Only a fully-Clean build qualifies; a Partial/Full re-walks and re-records.
        if (device != null && _opsRecorded && _opsReplayable && LastBuildKind == RenderBuildKind.Clean && OpsMatchTransforms)
        {
            LastFrameReplayed = true;
            ExecuteOps(device, fullScissor);
            return;
        }

        // Fast-path PARTIAL replay: a geometry-only partial that only recoloured/updated already-batched tiles in place (no
        // splice). Patch just those slots, then replay - O(dirty). ONLY when nothing MOVED (!LastBuildTransformDirty):
        // ExecuteOps redraws batch segments from last frame's baked positions, so a MOVE would leave batched fills stale
        // while per-unit draws follow the new transform (the "outline runs ahead of its fill" tear) -> fall through to the walk.
        if (device != null && _opsRecorded && _opsReplayable && LastBuildKind == RenderBuildKind.Partial
            && !LastBuildTransformDirty && OpsMatchTransforms && !_partialSpliced && _rectBatch != null && TryPartialReplay(device, fullScissor))
        { LastFrameReplayed = true; return; }

        // SPLICED partial patch: a dirty control's unit COUNT changed (hover background 0<->1, a live chart re-recording a
        // different number of segments). Its group re-rendered in place; here the retained BATCH is patched by segment
        // surgery (excise its old run, its re-baked items append as a new segment spliced into the op stream at the same
        // paint position) then replayed. O(dirty groups). Falls back to the full walk on anything not yet patchable.
        if (device != null && _opsRecorded && _opsReplayable && LastBuildKind == RenderBuildKind.Partial
            && !LastBuildTransformDirty && OpsMatchTransforms && _partialSpliced && _rectBatch != null && TrySplicedPatch(device, fullScissor))
        { LastFrameReplayed = true; return; }

        // TEMP trace: WHY this frame is walking instead of patching - the walk is O(scene) and the patch is O(dirty), so
        // every one of these is a 43 ms frame among 0.12 ms ones.
        if (Core.Diagnostics.FrameTrace.Enabled && LastBuildKind == RenderBuildKind.Partial)
        {
            _traceWhy = !_opsRecorded ? (byte)1
                : !_opsReplayable ? (byte)2
                : LastBuildTransformDirty ? (byte)3
                : !OpsMatchTransforms ? (byte)4
                : _partialSpliced ? (byte)5
                : (byte)6;   // the patch itself refused
        }

        var scissorNarrowed = false;   // whether the active scissor is currently narrower than fullScissor

        _recording = device != null;   // a device walk records its op stream for a later clean-frame replay
        if (_recording)
        {
            _ops.Clear(); 
            _opsReplayable = true; 
            _rectSlotByUnit.Clear();
            _sdfSlotByUnit.Clear();
            _textRunByUnit.Clear();
            _unitsByBrush.Clear(); 
            _walkGroup = null; 
            _walkVersion++;
            _nodeAllAware.Clear();
            _movedNodesBuf.Clear();   // a full walk re-bakes fresh node matrices - pending node moves are subsumed
            // ...but "subsumed" holds only if this walk composes CURRENT transforms. When the fast path BAILS on a moved
            // node (non-aware content - e.g. a tile that just face-swapped to an image) it bails BEFORE its own memo flush,
            // so this fall-through walk would re-bake the moving subtree at LAST frame's memoized position (a flip froze at
            // the 90-degree swap angle until a scroll flushed the memo). Clear the WORLD memos - NOT the clip memo:
            // recomputing it from live ancestor Bounds mid-relayout culled on-screen tiles for a frame (the hover "empty cell").
            _worldCache.Clear();
            _relWorldCache.Clear();
            _opacityChain.Clear();
        }

        // Text + item-background + instanced-fill batches: reset per frame. Device renders only - GPU-free tests skip batching.
        if (device != null)
        {
            _textBatch ??= new TextBatchCollector();
            _rectBatch ??= new RectBatchCollector();
            _ellipseBatch ??= new EllipseBatchCollector();
            _gradientRectBatch ??= new GradientRectCollector();
            _gradientEllipseBatch ??= new GradientEllipseCollector();
            _patternBatch ??= new PatternRectCollector();
            _fractalBatch ??= new FractalRectCollector();
            _textBatch.BeginFrame(device);
            _rectBatch.BeginFrame(device);
            _ellipseBatch.BeginFrame(device);
            _gradientRectBatch.BeginFrame(device);
            _gradientEllipseBatch.BeginFrame(device);
            _patternBatch.BeginFrame(device);
            _fractalBatch.BeginFrame(device);
            // Created lazily (below, on the first textured fill) - but once it exists it needs its frame reset like any
            // other collector. Leaving it out is what made a nine-slice draw for exactly ONE frame and then vanish.
            _texRectBatch?.BeginFrame(device);
            _haloUnder?.BeginFrame(device);
            _haloOver?.BeginFrame(device);
            _haloLivingUnder?.BeginFrame(device);
            _haloLivingOver?.BeginFrame(device);
            _haloOverOwner = null;
            // Transform table: this frame's copy, and its address on the collectors that were just (re)created above.
            BeginTransformFrame(device);
            var sceneClean = LastBuildKind == RenderBuildKind.Clean;
            // Incremental upload: a Clean frame re-bakes byte-identical items into slots the buffers already hold, so Flush
            // skips the redundant upload (zero bytes move on an idle frame).
            if (InstancedFillCollector.Enabled)
            {
                _instanceBuffers ??= new GpuBufferManager(device);
                _instancedFill ??= new InstancedFillCollector(device, _instanceBuffers);
                _instancedFill.TransformsAddress = _transformTable.DeviceAddress;   // instance VS fetches its slot matrix
                _instancedFill.BeginFrame();
                _instancedFill.SceneClean = sceneClean;
            }
            _batchOpen = false;
        }

        // CLONES (§4o): a prototype's subtree is drawn once per matrix instead of once at its own place. The subtree is a
        // CONTIGUOUS run of groups (paint rank is DFS order), so a clone run is "replay groups [start, end) under another
        // matrix" - the per-unit body below is untouched except for the one line that composes the clone into the world.
        IReadOnlyList<Matrix4x4F> cloneRun = null;
        var cloneStart = 0;
        var cloneEnd = 0;
        var cloneIndex = 0;
        var recordingBeforeClones = _recording;

        for (var groupIndex = 0; groupIndex < _groups.Count; groupIndex++)
        {
            var group = _groups[groupIndex];

            if (cloneRun == null && group.Clones is { Count: > 0 } clones)
            {
                cloneRun = clones;
                cloneStart = groupIndex;
                cloneEnd = CloneSubtreeEnd(groupIndex);
                cloneIndex = 0;
                _cloneMatrix = clones[0];
                // A clone run IS recorded - the stream has to describe the whole frame, clones included, or a replay
                // re-issues everything except them. What it must NOT do is offer this group to the per-unit patch paths:
                // they key a batch slot by UNIT, one to one, and a cloned unit owns N of them. Marking the group
                // unpatchable says exactly that, and costs nothing else.
                // (Refusing to replay instead was the first attempt, and it cost the whole window its fast path for as
                // long as any skeleton was on screen: 600 fps -> 180.)
                group.PatchableRectOnly = false;
            }

            foreach (var unit in group.Units)
            {
            // Group boundary (recording walks): reset this group's spliced-patch records once per group (re-derived by the
            // draw decisions below). Boundary detection instead of an outer block keeps the hot loop flat.
            if (_recording && !ReferenceEquals(group, _walkGroup))
            {
                _walkGroup = group;
                _recordOrder = group.Order;
                // An EMPTY rect segment will begin with whatever group first puts a rect in it, and that is this one or a
                // later one - so keep moving the mark until something lands.
                if (_rectBatch == null || !_rectBatch.HasPending) _rectSegStart = group.Order;
                group.RectRuns.Clear();
                group.PatchableRectOnly = true;
                group.WalkVersion = _walkVersion;
            }

            // World transform read ONCE (frame-memoized): the bounds-cull below and the GPU re-bake use the SAME value, so
            // the cull can't approve "inside" while the GPU draws the element elsewhere (the spill).
            // A clone COMPOSES onto it (never replaces it): the subtree keeps its own internal layout and the clone only
            // says where this copy goes. Substituting would collapse the whole subtree onto the clone's origin.
            var wt = World(unit.Component);
            if (_cloneMatrix.HasValue) wt = wt * _cloneMatrix.Value;

            var scissor = fullScissor;
            var clipped = false;
            if (device != null)
            {
                scissor = ResolveScissor(unit.Component, wt, fullScissor, out clipped, out var cull);
                // Owner entirely outside its clip (a virtualized item just off the viewport, content sliding out): draw
                // nothing, feed no batch. The per-surviving-unit scissor is set below.
                if (cull)
                {
                    // A culled unit draws nothing, so its motion node stays PATCHABLE (rewriting its matrix can't desync
                    // draws that don't exist). Without this, tilting off-viewport tiles (the tilt FIELD moves every tile,
                    // scrolled-out included) left their nodes un-aware -> every mouse frame bailed to a full walk.
                    if (_recording && NodeOf(unit.Component) is { } culledNode)
                        _nodeAllAware.TryAdd(culledNode.RenderId, true);
                    continue;
                }
            }

            // Bake AND draw with the transform the cull approved: refresh RenderData ONCE here (the batches read its opacity
            // while baking, the per-unit path reuses it). Culled units returned above. Compose the effective alpha from the
            // frozen snapshot first, so batches (rru.FillOpacity) and per-unit renderers bake with the current opacity.
            unit.SetEffectiveOpacity(EffectiveOpacity(unit.Component));
            unit.Update(wt, _projectionMatrix, _renderScale);

            // The unit's soft bands (aura / shadow), if it wears any. NOT an alternative to its fill - an addition, so it
            // is collected before the routing chain and drawn first at the flush, which is what puts it UNDER the fills.
            if (device != null && (HaloRectCollector.WantsBatch(unit.RenderData) || HaloLivingCollector.WantsBatch(unit.RenderData)))
            {
                // The band reaches PAST the element, so the overlap test uses the grown box: what it must not be drawn
                // under is whatever the band itself covers, not just what the element covers. A LIVING band wanders
                // further than its radius, so it answers for its own reach.
                var reach = System.Math.Max(HaloRectCollector.MaxReach(unit.RenderData.Halo),
                    HaloLivingCollector.MaxReach(unit.RenderData.LivingHalo));
                var bandBounds = LogicalBounds(unit.Component, wt).Inflate(reach);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(-1, bandBounds))
                {
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                }
                // Both sides are collected HERE, before the fill - only their FLUSH positions differ, and the flush is
                // what decides paint order. Collecting the inner one later would mean finding this unit again.
                var under = CollectHalo(device, unit, wt, scissor, inner: false);
                var over = CollectHalo(device, unit, wt, scissor, inner: true);
                under |= CollectLivingHalo(device, unit, wt, scissor, inner: false);
                over |= CollectLivingHalo(device, unit, wt, scissor, inner: true);
                if ((under || over) && _recording)
                {
                    group.PatchableRectOnly = false;   // a band is not a rect slot; the fast-path patch can't reproduce it
                }
            }

            // Batches: item-background rects (lower layer) + text (upper layer), each one instanced draw. A clip-group
            // change (scissor, or the text atlas) flushes BOTH together (rect-under-text order); a non-batchable unit that
            // overlaps either flushes both first so it paints on top.
            if (device != null && unit is RectangleRenderUnit rru && _rectBatch.CanBatch(rru.RectPayload))
            {
                var rectBounds = LogicalBounds(unit.Component, wt);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(0, rectBounds, unit.Component))   // 0 = rect layer
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var bakeWorld = ResolveBake(device, unit.Component, wt, out var slot4Rect);
                if (_rectBatch.TryAdd(rru.RectPayload, bakeWorld, rru.FillOpacity, scissor, rectBounds, slot4Rect))
                {
                    if (_recording)
                    {
                        var slot = _rectBatch.LastSlot;
                        _rectSlotByUnit[unit] = slot;   // for a later fast-path partial replay
                        IndexUnitBrush(unit.Component, unit, rru.RectPayload.LiveBrush);   // for a composited paint re-bake
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
                // A rounded rect with a LINEAR/RADIAL gradient fill: same SDF-batch family, different pass (the pixel shader
                // evaluates the gradient). Shares the clip group with the other batches.
                var gradRectBounds = LogicalBounds(unit.Component, wt);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(2, gradRectBounds, unit.Component))   // 2 = gradient-rect layer
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var gradBakeWorld = ResolveBake(device, unit.Component, wt, out var slot4Grad);
                if (_gradientRectBatch.TryAdd(grru.RectPayload, gradBakeWorld, grru.FillOpacity, scissor, gradRectBounds, slot4Grad))
                {
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;   // gradient: node-aware, but not rect-splice-patchable
                        _sdfSlotByUnit[unit] = (SdfSlotKind.GradientRect, _gradientRectBatch.LastSlot);   // ...but PAINT-patchable
                        IndexUnitBrush(unit.Component, unit, grru.RectPayload.LiveBrush);
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                grru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is EllipseRenderUnit eru && _ellipseBatch.CanBatch(eru.EllipsePayload))
            {
                var ellipseBounds = LogicalBounds(unit.Component, wt);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(1, ellipseBounds, unit.Component))   // 1 = ellipse layer
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var bakeWorld = ResolveBake(device, unit.Component, wt, out var slot4El);
                if (_ellipseBatch.TryAdd(eru.EllipsePayload, bakeWorld, eru.FillOpacity, scissor, ellipseBounds, slot4El))
                {
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;   // non-rect-batch draw -> not rect-splice-patchable
                        _sdfSlotByUnit[unit] = (SdfSlotKind.Ellipse, _ellipseBatch.LastSlot);   // ...but PAINT-patchable
                        IndexUnitBrush(unit.Component, unit, eru.EllipsePayload.LiveBrush);
                    }
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
                var gradElBounds = LogicalBounds(unit.Component, wt);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(3, gradElBounds, unit.Component))   // 3 = gradient-ellipse layer
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var gradElBakeWorld = ResolveBake(device, unit.Component, wt, out var slot4GradEl);
                if (_gradientEllipseBatch.TryAdd(geru.EllipsePayload, gradElBakeWorld, geru.FillOpacity, scissor, gradElBounds, slot4GradEl))
                {
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;   // gradient: node-aware, but not rect-splice-patchable
                        _sdfSlotByUnit[unit] = (SdfSlotKind.GradientEllipse, _gradientEllipseBatch.LastSlot);   // ...but PAINT-patchable
                        IndexUnitBrush(unit.Component, unit, geru.EllipsePayload.LiveBrush);
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                geru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is EllipseRenderUnit peru && _patternBatch.CanBatchEllipse(peru.EllipsePayload))
            {
                // A full ellipse with a PROCEDURAL PATTERN/NOISE fill: routes into the SAME pattern SDF batch (self-AA, no
                // jagged tessellated edges), the shader branching to the ellipse SDF on the negative baked corner radius.
                // Same clip group + layer 4 as the pattern rect.
                var patElBounds = LogicalBounds(unit.Component, wt);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(4, patElBounds, unit.Component))   // 4 = pattern layer
                {
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                }
                var patElBakeWorld = ResolveBake(device, unit.Component, wt, out var slot4PatEl);
                if (_patternBatch.TryAddEllipse(peru.EllipsePayload, patElBakeWorld, peru.FillOpacity, scissor, patElBounds, slot4PatEl))
                {
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;   // pattern: node-aware, not paint/splice-patchable in v1
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                peru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is RectangleRenderUnit pru && _patternBatch.CanBatch(pru.RectPayload))
            {
                // A rounded rect with a PROCEDURAL PATTERN fill (checkerboard/stripes/dots/grid): a new SDF-batch sibling,
                // its own pass evaluates the pattern per fragment. Shares the clip group with the other batches.
                var patternBounds = LogicalBounds(unit.Component, wt);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(4, patternBounds, unit.Component))   // 4 = pattern layer
                {
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                }
                var patBakeWorld = ResolveBake(device, unit.Component, wt, out var slot4Pat);
                if (_patternBatch.TryAdd(pru.RectPayload, patBakeWorld, pru.FillOpacity, scissor, patternBounds, slot4Pat))
                {
                    // Pattern is node-aware (rides the transform table), but NOT paint/splice-patchable in v1: no
                    // _sdfSlotByUnit entry, so a dirty pattern falls to a full walk (patterns are static backdrops).
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                pru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is RectangleRenderUnit fru && _fractalBatch.CanBatch(fru.RectPayload))
            {
                // A rounded rect with an escape-time FRACTAL fill (Julia/Mandelbrot): a new SDF-batch sibling, its own pass
                // iterates z=z²+c per fragment. Shares the clip group with the other batches; auto-morph is a shader-side
                // Time drift, so this batch is not paint/slot-patchable (no _sdfSlotByUnit entry) - a full walk re-records it.
                var fractalBounds = LogicalBounds(unit.Component, wt);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(5, fractalBounds, unit.Component))   // 5 = fractal layer
                {
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                }
                var fracBakeWorld = ResolveBake(device, unit.Component, wt, out var slot4Frac);
                if (_fractalBatch.TryAdd(fru.RectPayload, fracBakeWorld, fru.FillOpacity, scissor, fractalBounds, slot4Frac))
                {
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                fru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is RectangleRenderUnit xru && TexRectCollector.WantsBatch(xru.RectPayload))
            {
                // A rounded rect whose fill is SAMPLED from a texture (ImageBrush / NineSliceBrush). ONE texture per
                // segment, so a different source flushes the batch - the same rule the text batch follows for its atlas.
                // A source still decoding has no texture yet: draw nothing this frame and let the re-render pick it up.
                var texture = xru.BrushTexture();
                if (texture == null)
                {
                    continue;
                }
                // Created on FIRST use, not with the other collectors: its GPU ring is dead weight in the windows that
                // never draw a textured fill - which is most of them - and enough caches paying for it at once ran the
                // device out of memory.
                if (_texRectBatch == null)
                {
                    _texRectBatch = new TexRectCollector { TransformsAddress = _transformTable?.DeviceAddress ?? 0 };
                    _texRectBatch.BeginFrame(device);
                }
                var texBounds = LogicalBounds(unit.Component, wt);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || !_texRectBatch.SameTexture(texture)
                    || OverlapsHigherLayer(6, texBounds, unit.Component))   // 6 = textured layer
                {
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                }
                var texBakeWorld = ResolveBake(device, unit.Component, wt, out var slot4Tex);
                if (_texRectBatch.TryAdd(xru.RectPayload, texBakeWorld, xru.FillOpacity, scissor, texBounds, texture, slot4Tex))
                {
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                xru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is EllipseRenderUnit xeru && TexRectCollector.WantsBatchEllipse(xeru.EllipsePayload))
            {
                // A full ellipse whose fill is SAMPLED from a texture: the SAME textured batch and layer as the rect, the
                // shader branching to the ellipse SDF on the negative baked corner radius. Same texture-per-segment rule.
                var texEllTexture = xeru.BrushTexture();
                if (texEllTexture == null)
                {
                    continue;
                }
                if (_texRectBatch == null)
                {
                    _texRectBatch = new TexRectCollector { TransformsAddress = _transformTable?.DeviceAddress ?? 0 };
                    _texRectBatch.BeginFrame(device);
                }
                var texEllBounds = LogicalBounds(unit.Component, wt);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || !_texRectBatch.SameTexture(texEllTexture)
                    || OverlapsHigherLayer(6, texEllBounds, unit.Component))   // 6 = textured layer
                {
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                }
                var texEllBakeWorld = ResolveBake(device, unit.Component, wt, out var slot4TexEll);
                if (_texRectBatch.TryAddEllipse(xeru.EllipsePayload, texEllBakeWorld, xeru.FillOpacity, scissor, texEllBounds, texEllTexture, slot4TexEll))
                {
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                xeru.EnsureMachinery();
                unit.Update(wt, _projectionMatrix, _renderScale);
            }
            else if (device != null && unit is TextRenderUnit tru && tru.TextComponent is { } tc && _textBatch.CanBatch(tc, out var atlas))
            {
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || !_textBatch.SameAtlas(atlas))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                // Node-aware, same as the rect batch: glyphs pack NODE-LOCAL with the node's transform-table slot, so a block
                // under a motion node (a scroll list) rides the O(1) slot-write fast path. ResolveBake returns the
                // node-relative transform + slot (world + slot 0 off any node).
                // The unit's own placement on top of the bake - a Drawing's text run sits at its own spot inside the
                // element. Folded here because this batch takes the COMPONENT, which cannot reach the payload; the
                // per-unit path composes the same value through Update.
                var textBake = tru.Place(ResolveBake(device, unit.Component, wt, out var slot4Text));
                var textFirst = _textBatch.RetainedCount;
                if (_textBatch.TryAdd(tc, textBake, slot4Text, scissor, atlas, LogicalBounds(unit.Component, wt)))
                {
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;   // text is a separate collector, not rect-splice-patchable...
                        // ...but SLOT-patchable: remember the run so a later re-render of the same glyph count patches it.
                        _textRunByUnit[unit] = (textFirst, _textBatch.RetainedCount - textFirst, atlas);
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;   // baked into the batch - drawn at the next flush (node-aware: no MarkNodeNotAware)
                }
                // else: rotated/sheared RELATIVE transform or overflow -> fall through to the per-block direct draw below
                // (which re-marks the node not-aware, exactly like a rejected rect)
            }
            else if (device != null && InstancedFillCollector.Enabled && unit is GeometryRenderUnit gru && _instancedFill.CanBatch(gru))
            {
                // General instanced fill (arbitrary tessellated geometry sharing a mesh): collect the fill and DEFER this
                // unit's fringe/stroke to the flush (drawn over the fill). A clip change flushes; the fill lands in its
                // natural z-layer (paint order), not all-at-once.
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                // A group draws every fill and only then every fringe, so an already-pending FRINGED shape could band this
                // one. Masking the fringes to the group's own coverage settles it - except once a frame has spent all
                // 255 marks, where closing the group stands in.
                if (_instancedFill.CoverageMarksExhausted
                    && _instancedFill.OverlapsPendingFringe(gru.Payload.Geometry.Bounds.TransformToAABB(gru.Place(wt))))
                {
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                }
                var fillBake = ResolveBake(device, unit.Component, wt, out var slot4Fill);
                if (_instancedFill.TryAdd(gru, fillBake, scissor, LogicalBounds(unit.Component, wt), slot4Fill))
                {
                    gru.FillInstanced = true;
                    // The fill AND its analytic-AA fringe now both ride the slot (one shared ring per mesh, drawn from
                    // the same instance buffer), so such a unit keeps the node aware. A unit that still has a per-unit
                    // overlay - a stroke, or a fringe the instanced path doesn't cover - does NOT: that draw bakes its
                    // transform from RenderData at record time, so a slot write would move the fill and leave its own
                    // outline behind, the exact tear the transform table removed.
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;
                        if (gru.HasPerUnitOverlay) MarkNodeNotAware(unit.Component);
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;   // fill batched; fringe/stroke drawn at the flush, over the fill
                }
                // Rejected (no drawable mesh / instance buffer overflow): draw the whole unit per-unit (fill included).
                gru.FillInstanced = false;
            }
            else if (device != null && InstancedFillCollector.Enabled && unit is GeometryRenderUnit ggru && _instancedFill.CanBatchGradient(ggru))
            {
                // General instanced GRADIENT fill (arbitrary geometry, gradient pass): same path, the fill body is skipped
                // (FillInstanced) and its fringe/stroke draw at the flush.
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var gradBake = ResolveBake(device, unit.Component, wt, out var slot4GradFill);
                if (_instancedFill.TryAddGradient(ggru, gradBake, scissor, LogicalBounds(unit.Component, wt), slot4GradFill))
                {
                    ggru.FillInstanced = true;
                    // The fill rides the slot now; only a per-unit overlay (its fringe, still per-unit here, or a
                    // stroke) bakes its transform at record time and so costs the node its slot-write fast path.
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;
                        if (ggru.HasPerUnitOverlay) MarkNodeNotAware(unit.Component);
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                ggru.FillInstanced = false;
            }
            else if (device != null && InstancedFillCollector.Enabled && unit is GeometryRenderUnit pgru && _instancedFill.CanBatchPattern(pgru))
            {
                // General instanced PATTERN/NOISE fill (arbitrary geometry, pattern-fill pass): same path as the gradient
                // one - the fill body is skipped (FillInstanced) and the unit's fringe/stroke draw at the flush.
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var patBake = ResolveBake(device, unit.Component, wt, out var slot4PatFill);
                if (_instancedFill.TryAddPattern(pgru, patBake, scissor, LogicalBounds(unit.Component, wt), slot4PatFill))
                {
                    pgru.FillInstanced = true;
                    // As the gradient above: the fill rides the slot, so only a real per-unit overlay costs the node
                    // its slot-write fast path.
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;
                        if (pgru.HasPerUnitOverlay) MarkNodeNotAware(unit.Component);
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                pgru.FillInstanced = false;
            }
            else if (device != null && InstancedFillCollector.Enabled && unit is GeometryRenderUnit tgru && _instancedFill.CanBatchTextured(tgru))
            {
                // General instanced TEXTURED fill (arbitrary geometry, textured-fill pass): as the gradient and pattern
                // ones - the fill body is skipped (FillInstanced) and the unit's fringe/stroke draw at the flush.
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                var texBake = ResolveBake(device, unit.Component, wt, out var slot4TexFill);
                if (_instancedFill.TryAddTextured(tgru, texBake, scissor, LogicalBounds(unit.Component, wt), slot4TexFill))
                {
                    tgru.FillInstanced = true;
                    if (_recording)
                    {
                        group.PatchableRectOnly = false;
                        if (tgru.HasPerUnitOverlay) MarkNodeNotAware(unit.Component);
                    }
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                tgru.FillInstanced = false;
            }
            else if (device != null && (_rectBatch.Active || _ellipseBatch.Active || _gradientRectBatch.Active || _gradientEllipseBatch.Active || _patternBatch.Active || _fractalBatch.Active || _textBatch.Active || (_instancedFill?.Active ?? false)))
            {
                // A non-batchable unit that overlaps any pending batch: flush them first so this unit paints OVER them, as
                // its later source order requires. Spatially disjoint units (a list's items) don't flush.
                var lb = LogicalBounds(unit.Component, wt);
                if (_rectBatch.OverlapsPending(lb) || _ellipseBatch.OverlapsPending(lb) || _gradientRectBatch.OverlapsPending(lb) || _gradientEllipseBatch.OverlapsPending(lb) || _patternBatch.OverlapsPending(lb) || _fractalBatch.OverlapsPending(lb) || _textBatch.OverlapsPending(lb) || (_instancedFill?.OverlapsPending(lb) ?? false))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
            }
            else if (device == null && unit is RectangleRenderUnit rruNoDev)
            {
                // No device = the overlay (popup / tooltip) path: each unit draws individually via unit.Render() below,
                // with NO batching. A batchable fill builds no per-unit machinery (it expects to be batched), so without
                // building it now its Render() draws NOTHING (a tooltip badge's background vanished). Build the body +
                // re-bake this frame's transform, exactly as the batch-rejected path does above.
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
            if (_recording) _ops.Add(new RenderOp { Kind = RenderOpKind.Unit, Unit = unit, Order = _recordOrder });
            }

            // End of a clone run's subtree: rewind to its first group under the next matrix, or leave the run.
            if (cloneRun != null && groupIndex == cloneEnd - 1)
            {
                if (++cloneIndex < cloneRun.Count)
                {
                    _cloneMatrix = cloneRun[cloneIndex];
                    groupIndex = cloneStart - 1;
                }
                else
                {
                    cloneRun = null;
                    _cloneMatrix = null;
                    _recording = recordingBeforeClones;
                }
            }
        }

        _cloneMatrix = null;   // a run that ended on the last group leaves it set otherwise
        _recording = recordingBeforeClones;

        // Drain the tail batches (rects under fills under text), then leave the device on the full scissor for next pass.
        if (device != null) FlushBatches(device, fullScissor, ref scissorNarrowed);
        if (scissorNarrowed) { device.SetScissors(fullScissor); RecordScissor(fullScissor); }

        if (_recording)
        {
            _opsRecorded = true; _recording = false;
            // The transform + layout state this op stream was recorded AGAINST. A replay is faithful only while it holds.
            _opsMatrixVersion = _transformTable?.MatrixVersion ?? 0;
            _layoutChangedSinceRecord = false;
        }
    }

    // The batches flush bottom-up (rect < ellipse < gradient-rect < gradient-ellipse < pattern < fractal < textured < instanced < text), so a
    // HIGHER-layer batch draws ON TOP. A unit going into `layer` that OVERLAPS a pending higher-layer batch would be drawn
    // UNDER it - yet that batch holds units EARLIER in paint order, so this (later) unit belongs on top (a solid thumb
    // sitting on a gradient bar, a solid overlay over gradient content). Returning true here flushes the pending batches
    // first, dropping this unit into a fresh cycle that draws after them = correct paint order. Same-or-lower layers keep
    // their insertion order and are fine as-is; disjoint content never overlaps, so same-material tiles pay only O(1) checks.
    private bool OverlapsHigherLayer(int layer, Rect lb, IUIComponent owner = null)
    {
        // Layer -1 is the halo band: it sits under EVERY fill, so a pending rect that overlaps it was painted earlier and
        // has to be flushed first - otherwise a panel's own background, batched before its child was reached, covers the
        // child's glow completely.
        if (layer < 0 && _rectBatch.OverlapsPending(lb))
        {
            return true;
        }

        // The INNER band sits above every fill, so anything below text that overlaps a pending one has to flush first -
        // otherwise a later element's fill would be drawn over a glow that belongs to the element before it.
        if (layer < 8 && !ReferenceEquals(owner, _haloOverOwner)
            && ((_haloOver != null && _haloOver.OverlapsPending(lb))
                || (_haloLivingOver != null && _haloLivingOver.OverlapsPending(lb))))
        {
            return true;
        }

        if (layer < 1 && _ellipseBatch.OverlapsPending(lb))
        {
            return true;
        }

        if (layer < 2 && _gradientRectBatch.OverlapsPending(lb))
        {
            return true;
        }

        if (layer < 3 && _gradientEllipseBatch.OverlapsPending(lb))
        {
            return true;
        }

        if (layer < 4 && _patternBatch.OverlapsPending(lb))
        {
            return true;
        }

        if (layer < 5 && _fractalBatch.OverlapsPending(lb))
        {
            return true;
        }

        if (layer < 6 && (_texRectBatch?.OverlapsPending(lb) ?? false))
        {
            return true;
        }

        if (layer < 7 && (_instancedFill?.OverlapsPending(lb) ?? false))
        {
            return true;
        }

        if (layer < 8 && _textBatch.OverlapsPending(lb))
        {
            return true;
        }

        return false;
    }

    // Play this frame's composited animations for RIGHT NOW and push to GPU, without the loop thread or the property system
    // (see Compositor). Recompose ALL entries (Tick), then apply each by its channel.
    private void ApplyCompositedAnimations(IGraphicsDevice device)
    {
        _traceComposited = 0;
        if (device == null || _transformTable == null) return;
        if (!Compositor.Tick(_compositedBuf))   // recomposes matrices AND republishes paint snapshots
        {
            if (_compositedOwners.Count > 0) _compositedOwners.Clear();
            return;
        }

        _traceComposited = _compositedBuf.Count;

        _compositedOwners.Clear();
        foreach (var entry in _compositedBuf)
        {
            if (entry.Channel == CompositorChannel.Transform)
            {
                ApplyCompositedTransform(device, entry);
                if (entry.Owner != null) _compositedOwners.Add(entry.Owner);
            }
            else ApplyCompositedPaint(device, entry);
        }
    }

    // TRANSFORM: one 64-byte matrix write moves the whole node, and it lands in TWO places. The transform table is what the
    // GPU reads (the retained instances draw in the new place). The frozen SNAPSHOT is what every compose helper here reads
    // (World, NodeOf, ResolveScissor), so overwriting LocalTransform makes a walk that runs this frame agree with the
    // compositor instead of re-baking the element back where it was.
    private void ApplyCompositedTransform(IGraphicsDevice device, Compositor.Entry entry)
    {
        var owner = entry.Owner;

        LayoutSnapshot snap;
        Matrix4x4F parentWorld;
        if (_applySnap.TryGetValue(owner, out snap))
        {
            // Normal frame: the whole snapshot is present. Remember this owner's snapshot + parent world so the fallback
            // below can keep animating it while the snapshot is being re-captured.
            parentWorld = snap.RenderParent != null ? World(snap.RenderParent) : Matrix4x4F.Identity;
            _compositedFallback[owner] = (snap, parentWorld);
        }
        else if (_compositedFallback.TryGetValue(owner, out var fb))
        {
            // A SETTLING theme/DPI swap re-captures the ENTIRE snapshot every frame (SnapReset - the cascade writes outside
            // the mark system), so between re-captures the owner is briefly absent and the animation would freeze on screen
            // while the render thread still ticks it (the theme-swap spinner). Reuse the last parent world we saw for this
            // owner (a busy overlay spinner's ancestors don't move mid-swap) so it keeps turning until the snapshot settles.
            snap = fb.Snap;
            parentWorld = fb.ParentWorld;
        }
        else return;   // never applied yet (its motion-node promotion isn't recorded here); the loop mirror re-records.

        var world = snap.RenderParent != null ? entry.Local * parentWorld : entry.Local;
        _applySnap[owner] = new LayoutSnapshot(entry.Local, snap.RenderSize, snap.ClipToBounds, snap.IsMotionNode, snap.RenderParent, snap.Opacity, snap.SelfOpacity);
        _worldCache[owner] = world;   // set directly: the _applySnap chain may be mid-recapture, so don't recompose off it
        _transformTable.SetMatrix(device, _transformTable.AcquireSlot(owner.RenderId), world);
        entry.MarkApplied();   // tell the loop thread the render thread is drawing this - so its mirror stops re-baking it
    }

    // PAINT: Tick already republished the brush's snapshot; here every slot that paints with it is re-baked from that
    // snapshot. Nothing moved, so this is the SAME per-slot re-bake the loop-driven paint patch does, on the render thread.
    // One brush fans out to all its units (the skeleton pulse) - the reason paint needs the brush->units index.
    private void ApplyCompositedPaint(IGraphicsDevice device, Compositor.Entry entry)
    {
        if (!entry.PaintChanged) return;   // the baked bytes are identical to last present - nothing to do (see Entry)
        if (entry.Target is not Core.Media.Brush brush) return;
        if (!_unitsByBrush.TryGetValue(brush, out var units)) return;

        foreach (var u in units)
        {
            if (!IsSlotPatchable(u)) continue;   // a unit whose bytes moved off the slot map (rare) - the next walk fixes it
            var bakeWorld = ResolveBake(device, u.Component, World(u.Component), out var slot);
            PatchSlot(device, u, bakeWorld, slot);
        }
    }

    private void RecordScissor(Rect2D scissor)
    {
        if (_recording) _ops.Add(new RenderOp { Kind = RenderOpKind.Scissor, Scissor = scissor, Order = _recordOrder });
    }


    // Draw a fast-path partial by patching only the dirty tiles' batch slots, then replaying last frame's op stream. False
    // (-> full walk) if ANY dirty unit isn't a still-batchable rect we recorded a slot for (its bytes live elsewhere - a
    // per-unit / text / instanced unit, or a tile that just switched to a gradient). Validate fully BEFORE patching.
    private bool TryPartialReplay(IGraphicsDevice device, Rect2D fullScissor)
    {
        // Moved motion nodes first: rewrite their table matrices (64B each) so the replayed segments draw the scrolled
        // subtrees at their new position. A moved node with non-node-aware retained content bails to the full walk.
        if (!RefreshMovedNodes(device)) return false;

        _opacityChain.Clear();   // a paint-only opacity change may have re-frozen the dirty subtree's snapshot; recompose it

        foreach (var comp in _partialDirty)
        {
            // A dirty component with NO drawn units (detached/pooled/collapsed - e.g. a text block that re-marks geometry
            // every frame but isn't in the paint tree) contributes nothing: the op stream is unchanged, so skip it and let
            // the replay stand. The common hover case (nothing visible changed).
            if (!_groupById.TryGetValue(comp.RenderId, out var g)) continue;
            foreach (var u in g.Units)
                if (!IsSlotPatchable(u))
                {
                    // TEMP: name the type that costs the frame its patch.
                    if (Core.Diagnostics.FrameTrace.Enabled) Core.Diagnostics.FrameTrace.Refuser = u.GetType().Name;
                    return false;   // a per-unit / text / instanced / no-longer-batchable dirty unit -> full walk
                }
        }
        // Nothing moved on a geometry-only partial, so the cached world is still valid; re-bake each dirty tile from its
        // (just-updated) payload into its retained slot. (No-units components patched nothing above.)
        foreach (var comp in _partialDirty)
        {
            if (!_groupById.TryGetValue(comp.RenderId, out var g)) continue;
            foreach (var u in g.Units)
            {
                u.SetEffectiveOpacity(EffectiveOpacity(u.Component));   // a paint change may be an opacity change
                var bakeWorld = ResolveBake(device, u.Component, World(u.Component), out var slot);
                if (!PatchSlot(device, u, bakeWorld, slot))
                    return false;   // became non-bakeable (rotated); the full walk re-bakes everything anyway
            }
        }
        AcceptPatchedTransforms();
        ExecuteOps(device, fullScissor);
        return true;
    }

    // A patch WRITES node matrices - that is how a scrolled or re-baked element moves without re-recording. The op stream
    // is checked against the table version to catch transforms that changed UNDER it, but the patch just validated the ones
    // it wrote (RefreshMovedNodes proves the moved subtrees are node-aware), so those must not count as a mismatch. Left
    // counting, the first patch made every following frame walk - one hover cost every other frame the whole scene.
    private void AcceptPatchedTransforms() => _opsMatrixVersion = _transformTable?.MatrixVersion ?? 0;

    // Does this unit's GPU data live in ONE retained SDF-batch slot we can rewrite in place? The whole precondition for
    // repainting without re-walking. Anything else (text, per-unit geometry, an instanced fill) keeps its bytes elsewhere.
    private bool IsSlotPatchable(IRenderUnit u)
    {
        // A CLONED unit fills one slot PER CLONE, and the maps below hold ONE slot per unit - the last the walk wrote.
        // Patching through it repaints a single card, and once the clone set shrinks (a list finishing its fill) the
        // walk renumbers the arena behind that run, so the remembered slot belongs to whatever moved into its place:
        // the pulse was recolouring the first star in step with the last skeleton. A cloned unit is repainted by the
        // next walk, in full, rather than by one slot write that may not even be its own.
        if (u.Component?.RenderClones is { Count: > 0 }) return false;

        if (u is RectangleRenderUnit rru)
        {
            if (_rectSlotByUnit.ContainsKey(u)) return _rectBatch.CanBatch(rru.RectPayload);
            if (_sdfSlotByUnit.TryGetValue(u, out var gr) && gr.Kind == SdfSlotKind.GradientRect)
                return _gradientRectBatch.CanBatch(rru.RectPayload);
            return false;
        }

        // A text block holds a RUN of glyph slots. Patchable only while the run still DESCRIBES it: the same number of
        // glyphs (the run is a fixed span of the retained buffer) and the same atlas (the recorded segment binds one).
        // A counter ticking 600 -> 598 qualifies; text that grew or shrank does not, and takes the walk.
        if (u is TextRenderUnit tru && _textRunByUnit.TryGetValue(u, out var run))
            return tru.TextComponent is { } tc
                && _textBatch.CanBatch(tc, out var atlas)
                && atlas == run.Atlas
                && tc.GlyphRun.Count == run.Count;

        if (u is EllipseRenderUnit eru && _sdfSlotByUnit.TryGetValue(u, out var e))
            return e.Kind == SdfSlotKind.Ellipse
                ? _ellipseBatch.CanBatch(eru.EllipsePayload)
                : _gradientEllipseBatch.CanBatch(eru.EllipsePayload);

        return false;
    }

    // Re-bake one unit from its (live) payload straight into the slot it already occupies. Validated by IsSlotPatchable.
    private bool PatchSlot(IGraphicsDevice device, IRenderUnit u, Matrix4x4F bakeWorld, int transformSlot)
    {
        if (u is RectangleRenderUnit rru)
        {
            if (_rectSlotByUnit.TryGetValue(u, out var rectSlot))
            {
                if (!RectBatchCollector.BakeItem(rru.RectPayload, bakeWorld, rru.FillOpacity, transformSlot, out var item)) return false;
                _rectBatch.UpdateSlot(device, rectSlot, item);
                return true;
            }

            if (!GradientRectCollector.BakeItem(rru.RectPayload, bakeWorld, rru.FillOpacity, transformSlot, out var gradItem)) return false;
            _gradientRectBatch.UpdateSlot(device, _sdfSlotByUnit[u].Slot, gradItem);
            return true;
        }

        if (u is TextRenderUnit tru)
        {
            // The block's own placement rides on top of the bake, exactly as the recording walk composed it.
            return _textBatch.UpdateRun(device, _textRunByUnit[u].First, tru.TextComponent, tru.Place(bakeWorld), transformSlot);
        }

        var eru = (EllipseRenderUnit)u;
        var entry = _sdfSlotByUnit[u];
        if (entry.Kind == SdfSlotKind.Ellipse)
        {
            if (!EllipseBatchCollector.BakeItem(eru.EllipsePayload, bakeWorld, eru.FillOpacity, transformSlot, out var item)) return false;
            _ellipseBatch.UpdateSlot(device, entry.Slot, item);
            return true;
        }

        if (!GradientEllipseCollector.BakeItem(eru.EllipsePayload, bakeWorld, eru.FillOpacity, transformSlot, out var gradEllipse)) return false;
        _gradientEllipseBatch.UpdateSlot(device, entry.Slot, gradEllipse);
        return true;
    }

    // One dirty group's staged patch (validated + baked BEFORE any mutation, so a bail leaves the retained frame intact).
    private struct GroupPatch
    {
        public ControlGroup Group;
        public RectItem[] Items;      // re-baked instances of the group's (non-culled) units, in unit order
        public Rect2D Scissor;        // the group's clip (all units of one component share it)
        public bool InPlace;          // count-stable recolor -> per-slot UpdateSlot, no surgery
        public int Layer;             // the layer this group's items belong to, resolved ONCE before anything is mutated
    }

    // Draw a partial whose dirty controls changed their unit COUNT (a hover backdrop appearing, a live chart) by editing
    // the LAYER each belongs to, then replaying - O(dirty layer) instead of O(scene). A layer is one recorded batch run
    // drawn by one op; the edit happens inside it and the op is left where it stands, so paint order relative to text,
    // per-unit draws and instanced geometry holds by construction. A control that has no run of its own gets its own
    // layer, placed by its paint RANK - never by what happens to sit next to it (see PlaceNewSegment).
    // Requirements per dirty group (checked BEFORE anything is mutated -> full walk): every unit rect-batchable NOW; a
    // group described by the last walk must have been rect-only; its clip must be the layer's. Re-baked runs are appended
    // and not reclaimed until a full walk resets Count, so a sustained burst still yields to the walk on a full arena.
    private bool TrySplicedPatch(IGraphicsDevice device, Rect2D fullScissor)
    {
        // Moved motion nodes first (same as TryPartialReplay): rewrite their matrices, bail on non-aware content.
        if (!RefreshMovedNodes(device)) return SpliceRefused("movedNode");

        // Op stream grown too long from accumulated splices -> recompact with a full walk before it mis-replays.
        if (_ops.Count > MaxRetainedOps) return SpliceRefused("opsTooLong");

        _opacityChain.Clear();   // recompose from the (possibly re-frozen) snapshot, as in TryPartialReplay

        // ---- Phase 1: validate + bake (no mutation) ----
        _patchBuf.Clear();
        var appendTotal = 0;
        foreach (var comp in _partialDirty)
        {
            if (!_groupById.TryGetValue(comp.RenderId, out var group)) continue;   // no drawn units - contributes nothing

            // A group's RectRuns are valid only against the arena the LAST recording walk (or a splice under it) built. A
            // stale WalkVersion means that walk did NOT visit it (recycled / scrolled off / re-appeared since) and its slots
            // were REASSIGNED to whatever the walk recorded there, so its runs now point at OTHER groups' slots - excising
            // them would blank a live neighbour for a frame (the hover "blink"). A stale group has nothing of its own to
            // excise: drop its runs and re-append fresh. (A splice re-append below re-stamps WalkVersion.)
            var walked = group.WalkVersion == _walkVersion;
            if (!walked) group.RectRuns.Clear();
            var runTotal = 0;
            foreach (var r in group.RectRuns) runTotal += r.Count;
            // A group DESCRIBED by the last walk must have been rect-only - else it also drew per-unit/text/instanced content
            // whose recorded ops we can't excise (stale Unit ops would even replay disposed units).
            if (walked && !group.PatchableRectOnly) return SpliceRefused("notRectOnly");

            var items = new List<RectItem>(group.Units.Count);
            var scissor = fullScissor;
            var haveScissor = false;
            foreach (var u in group.Units)
            {
                if (u is not RectangleRenderUnit rru || !_rectBatch.CanBatch(rru.RectPayload)) return SpliceRefused("notARect");
                u.SetEffectiveOpacity(EffectiveOpacity(u.Component));   // a splice may ride an opacity cascade too
                var wt = World(u.Component);
                if (!haveScissor)
                {
                    scissor = ResolveScissor(u.Component, wt, fullScissor, out _, out var cull);
                    haveScissor = true;
                    if (cull) break;   // whole component off-clip: it contributes no items (units share the component)
                }
                var bakeWorld = ResolveBake(device, u.Component, wt, out var slot);
                if (!RectBatchCollector.BakeItem(rru.RectPayload, bakeWorld, rru.FillOpacity, slot, out var item)) return SpliceRefused("notBakeable");
                items.Add(item);
            }

            // Count-stable recolor (every unit already holds a retained slot): patch in place, no surgery/fragmentation.
            var inPlace = items.Count == group.Units.Count && items.Count == runTotal && AllUnitsHaveSlots(group);
            if (!inPlace) appendTotal += items.Count;
            _patchBuf.Add(new GroupPatch { Group = group, Items = items.ToArray(), Scissor = scissor, InPlace = inPlace });
        }

        // ---- Phase 1b: which LAYER does each surgery group belong to (no mutation) ----
        // The unit of repair is the SEGMENT, not the item. A group whose unit count changed is put right by re-baking the
        // whole segment it lives in, with its new items in their paint position inside it, and pointing the SAME recorded
        // op at the result. Nothing is excised, nothing is inserted into the op stream, so paint order relative to text,
        // per-unit draws and instanced flushes is unchanged BY CONSTRUCTION - which is why there is no instanced-flush
        // question here at all. Cost is O(items in that segment) instead of O(scene).
        _patchLayers.Clear();
        for (var n = 0; n < _patchBuf.Count; n++)
        {
            var p = _patchBuf[n];
            if (p.InPlace) continue;

            // Nothing recorded for this control yet: it gets its own segment, placed by its own paint rank.
            if (p.Group.RectRuns.Count == 0)
            {
                p.Layer = -1;
                _patchBuf[n] = p;
                continue;
            }

            var layer = TargetLayer(p.Group);
            if (layer < 0) return false;   // TargetLayer already named which of its three answers it gave
            if (FindSegmentOp(layer) < 0) return SpliceRefused("noLayerOp");
            // One segment draws under ONE clip; a group that now sits under a different one cannot join it.
            if (p.Items.Length > 0 && !ScissorEquals(_rectBatch.GetSegmentScissor(layer), p.Scissor)) return SpliceRefused("otherClip");
            p.Layer = layer;
            _patchBuf[n] = p;
            if (!_patchLayers.Contains(layer)) _patchLayers.Add(layer);
        }

        // ---- Phase 2: mutate (can no longer fail) ----
        foreach (var p in _patchBuf)
        {
            if (!p.InPlace) continue;   // count-stable recolour: the slots are already the right ones
            var i = 0;
            foreach (var u in p.Group.Units) _rectBatch.UpdateSlot(device, _rectSlotByUnit[u], p.Items[i++]);
        }

        foreach (var p in _patchBuf)
        {
            if (p.InPlace) continue;
            if (!ReissueLayer(device, p)) return SpliceRefused("arenaFull");
        }

        AcceptPatchedTransforms();
        ExecuteOps(device, fullScissor);
        return true;
    }

    // TEMP: name WHICH of the splice's preconditions sent the frame to the full walk - there are nine and they are fixed
    // by nine different means.
    private static bool SpliceRefused(string reason)
    {
        if (Core.Diagnostics.FrameTrace.Enabled) Core.Diagnostics.FrameTrace.Refuser = reason;
        return false;
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

    // A LAYER is one recorded batch run drawn by one op - the backdrops of a list.s rows, say. A group that already draws
    // in one is repaired inside it; a group that does not gets its own (see PlaceNewSegment), so this only ever answers
    // for the former. It used to hunt for a neighbour.s layer to join, which is how the placement came to depend on what
    // else the frame happened to contain.
    private int TargetLayer(ControlGroup group)
    {
        var own = _rectBatch.FindSegmentContaining(group.RectRuns[0].First);
        if (own < 0 && Core.Diagnostics.FrameTrace.Enabled) Core.Diagnostics.FrameTrace.Refuser = "runOutsideAnyLayer";
        return own;
    }

    // Re-issue ONE layer with one group's items replaced. The layer's content comes from the RETAINED RANGE, copied as
    // bytes, never rebuilt from the groups: group bookkeeping does not describe every slot in a layer (a culled unit, a
    // group the last walk did not visit), and whatever it fails to account for would be dropped. The range describes itself.
    private bool ReissueLayer(IGraphicsDevice device, GroupPatch patch)
    {
        if (patch.Layer < 0) return PlaceNewSegment(device, patch);

        var layer = patch.Layer;
        var (first, count) = _rectBatch.SegmentRange(layer);
        var scissor = _rectBatch.GetSegmentScissor(layer);
        var group = patch.Group;

        // WHERE inside the layer this group's items sit: its own run, which is the only reason it is in this layer at all.
        var at = group.RectRuns[0].First - first;
        var replaced = 0;
        foreach (var run in group.RectRuns) replaced += run.Count;

        if (at < 0 || at + replaced > count) return true;   // its run is not in this layer after all - leave the frame be

        // The cheap path: edit inside the room the layer already owns, moving only what follows the edit. Only when the
        // layer has outgrown its room does it relocate, and then it does have to be carried across whole.
        if (!_rectBatch.ReplaceInSegment(device, layer, at, replaced, patch.Items))
        {
            _rebakeBuf.Clear();
            _rectBatch.CopyRetained(first, at, _rebakeBuf);
            _rebakeBuf.AddRange(patch.Items);
            _rectBatch.CopyRetained(first + at + replaced, count - at - replaced, _rebakeBuf);
            if (!_rectBatch.RepointSegment(device, layer, CollectionsMarshal.AsSpan(_rebakeBuf), scissor)) return false;
        }

        // The layer may have moved, and everything after the edit shifted by the size difference. Re-index every group
        // that draws in it - runs and unit slots both, or a later patch would address freed space.
        var (newFirst, _) = _rectBatch.SegmentRange(layer);
        var delta = patch.Items.Length - replaced;
        var editEnd = first + at + replaced;
        foreach (var g in _groups)
        {
            if (g.WalkVersion != _walkVersion || ReferenceEquals(g, group)) continue;
            var touched = false;
            for (var r = 0; r < g.RectRuns.Count; r++)
            {
                var run = g.RectRuns[r];
                if (run.First < first || run.First >= first + count) continue;
                g.RectRuns[r] = (run.First - first + newFirst + (run.First >= editEnd ? delta : 0), run.Count);
                touched = true;
            }

            if (touched) ReslotUnits(g);
        }

        group.RectRuns.Clear();
        group.WalkVersion = _walkVersion;
        if (patch.Items.Length > 0)
        {
            group.RectRuns.Add((newFirst + at, patch.Items.Length));
            group.PatchableRectOnly = true;
        }

        ReslotUnits(group);
        return true;
    }

    // A control that drew nothing until now: give it its own segment and put its op where its RANK says, not where its
    // neighbours happen to be. Nothing already recorded moves, so this cannot disturb anyone's order - and it is provable
    // without looking at what else the frame contains.
    private bool PlaceNewSegment(IGraphicsDevice device, GroupPatch patch)
    {
        var group = patch.Group;
        if (patch.Items.Length == 0)
        {
            group.WalkVersion = _walkVersion;
            ReslotUnits(group);
            return true;   // drew nothing, still draws nothing
        }

        var seg = _rectBatch.AllocateSegment(device, patch.Items, patch.Scissor);
        if (seg < 0) return false;

        _ops.Insert(OpIndexForRank(group.Order), new RenderOp
        {
            Kind = RenderOpKind.Segment, Batch = 0, SegIndex = seg, Order = group.Order
        });

        var (first, _) = _rectBatch.SegmentRange(seg);
        group.RectRuns.Clear();
        group.RectRuns.Add((first, patch.Items.Length));
        group.PatchableRectOnly = true;
        group.WalkVersion = _walkVersion;
        ReslotUnits(group);
        return true;
    }

    // Where an op of this rank belongs in the stream: before the first op recorded for a LATER rank. Never immediately
    // after a Scissor op - that op sets a clip for the draw that follows it, and a segment slipped in between would
    // restore the full clip and leave that draw unclipped.
    //
    // KNOWN LIMIT, reproduced by BorderPatchRenderTests.ANeighbourAppearing_DoesNotCostABorderItsRing: a SEGMENT covers
    // the whole paint SPAN between two flushes (its OrderFirst..Order), and a newcomer whose rank lands inside that span
    // has no correct place in a flat stream - before the op it paints under controls it must cover, after it over controls
    // it must not. Comparing against the span's START instead only moves which half is wrong (it was tried: the backdrop
    // tests, which need the other half, fail immediately). The fix is to SPLIT the segment at the newcomer's rank, which
    // is why the span is recorded at all.
    private int OpIndexForRank(long order)
    {
        var at = _ops.Count;
        for (var i = 0; i < _ops.Count; i++)
        {
            if (_ops[i].Order <= order) continue;
            at = i;
            break;
        }

        while (at > 0 && _ops[at - 1].Kind == RenderOpKind.Scissor) at--;
        return at;
    }

    // A group's units map onto its run one-for-one only when the run accounts for all of them; a partly-culled group gives
    // its entries up rather than keep a guess, and the next walk restores them.
    private void ReslotUnits(ControlGroup group)
    {
        var total = 0;
        foreach (var run in group.RectRuns) total += run.Count;
        if (total != group.Units.Count || group.RectRuns.Count != 1)
        {
            foreach (var u in group.Units) _rectSlotByUnit.Remove(u);
            return;
        }

        var i = group.RectRuns[0].First;
        foreach (var u in group.Units) _rectSlotByUnit[u] = i++;
    }


}
