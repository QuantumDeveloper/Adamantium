using System;
using System.Collections.Generic;
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
    private GradientRectCollector _gradientRectBatch;   // SDF family: rounded rects with a linear/radial GRADIENT fill
    private GradientEllipseCollector _gradientEllipseBatch;   // SDF family: ellipses with a linear/radial GRADIENT fill
    private PatternRectCollector _patternBatch;   // SDF family: rounded rects with a PROCEDURAL pattern fill (checker/stripes/dots/grid)
    private FractalRectCollector _fractalBatch;   // SDF family: rounded rects with an escape-time FRACTAL fill (Julia/Mandelbrot)
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
        public byte Batch;        // Segment: which collector (0 rect, 1 ellipse, 2 text, 3 gradient-rect, 4 gradient-ellipse, 5 pattern, 6 fractal)
        public int SegIndex;      // Segment: index into that collector's recorded segment list; InstancedFlush: flush index
    }
    private readonly List<RenderOp> _ops = new();
    private bool _recording;       // this frame runs the walk and is appending ops
    private bool _opsRecorded;     // _ops holds a complete frame from a prior walk
    private bool _opsReplayable;   // the recorded stream faithfully reproduces the frame (currently always true - see above)
    private bool _opsHaveInstancedFlush;   // the recorded stream contains an instanced-fill flush op - a splice that INSERTS a
                                           // rect segment must yield to the full walk (it can't place the segment relative to
                                           // that flush, so an appended highlight could draw OVER instanced geometry)

    private readonly List<Compositor.Entry> _compositedBuf = new();   // this thread's view of the composited set (reused)
    private readonly Dictionary<IUIComponent, (LayoutSnapshot Snap, Matrix4x4F ParentWorld)> _compositedFallback = new();   // keep animating across a settling swap's snapshot re-capture
    private readonly HashSet<IUIComponent> _compositedOwners = new();   // motion nodes the compositor moved THIS present (ExecuteOps re-Updates their per-unit draws)

    private const int MaxRetainedOps = 256;   // op stream past this -> a splice yields to a full walk that recompacts
    private readonly List<GroupPatch> _patchBuf = new();   // TrySplicedPatch: staged per-group patches (validated before mutation)

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
        // The animations this thread plays by itself. BEFORE the clean-frame early-out on purpose: a composited animation
        // changes what the retained op stream draws (a matrix, a re-baked colour slot), so an otherwise CLEAN frame is
        // exactly when it must still apply - the loop can be stalled in a theme cascade and the spinner keeps turning.
        ApplyCompositedAnimations(device);

        // Clean-frame replay: re-issue the last recorded walk's op stream and skip the per-unit loop (the retained buffers
        // still hold its bytes). Only a fully-Clean build qualifies; a Partial/Full re-walks and re-records.
        if (device != null && _opsRecorded && _opsReplayable && LastBuildKind == RenderBuildKind.Clean)
        {
            ExecuteOps(device, fullScissor);
            return;
        }

        // Fast-path PARTIAL replay: a geometry-only partial that only recoloured/updated already-batched tiles in place (no
        // splice). Patch just those slots, then replay - O(dirty). ONLY when nothing MOVED (!LastBuildTransformDirty):
        // ExecuteOps redraws batch segments from last frame's baked positions, so a MOVE would leave batched fills stale
        // while per-unit draws follow the new transform (the "outline runs ahead of its fill" tear) -> fall through to the walk.
        if (device != null && _opsRecorded && _opsReplayable && LastBuildKind == RenderBuildKind.Partial
            && !LastBuildTransformDirty && !_partialSpliced && _rectBatch != null && TryPartialReplay(device, fullScissor))
            return;

        // SPLICED partial patch: a dirty control's unit COUNT changed (hover background 0<->1, a live chart re-recording a
        // different number of segments). Its group re-rendered in place; here the retained BATCH is patched by segment
        // surgery (excise its old run, its re-baked items append as a new segment spliced into the op stream at the same
        // paint position) then replayed. O(dirty groups). Falls back to the full walk on anything not yet patchable.
        if (device != null && _opsRecorded && _opsReplayable && LastBuildKind == RenderBuildKind.Partial
            && !LastBuildTransformDirty && _partialSpliced && _rectBatch != null && TrySplicedPatch(device, fullScissor))
            return;

        var scissorNarrowed = false;   // whether the active scissor is currently narrower than fullScissor

        _recording = device != null;   // a device walk records its op stream for a later clean-frame replay
        if (_recording)
        {
            _ops.Clear(); 
            _opsReplayable = true; 
            _opsHaveInstancedFlush = false; 
            _rectSlotByUnit.Clear(); 
            _sdfSlotByUnit.Clear(); 
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
            // Transform table: identity at slot 0, (re)sized at this fence-safe point; the SDF collectors read the address
            // per draw (BeginFrame may have reallocated the buffer).
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
            _transformTable.BeginFrameStats();
            // AGGREGATE, never assign: there is one table PER RENDER CACHE (per window/popup) and these counters are static,
            // so a plain assignment let the last writer - typically an empty overlay window holding a single slot - erase
            // the real window's numbers. Max for levels, sum for per-frame events; the reader zeroes them after logging.
            var stats = _transformTable;
            if (stats.SlotCount > Core.Diagnostics.RuntimeStats.TransformSlots)
                Core.Diagnostics.RuntimeStats.TransformSlots = stats.SlotCount;
            if (stats.Recreations > Core.Diagnostics.RuntimeStats.TransformRecreations)
                Core.Diagnostics.RuntimeStats.TransformRecreations = stats.Recreations;
            Core.Diagnostics.RuntimeStats.TransformWrites += stats.WritesLastFrame;
            Core.Diagnostics.RuntimeStats.TransformAcquires += stats.AcquiresLastFrame;
            Core.Diagnostics.RuntimeStats.TransformReleases += stats.ReleasesLastFrame;
            _rectBatch.TransformsAddress = _transformTable.DeviceAddress;
            _ellipseBatch.TransformsAddress = _transformTable.DeviceAddress;
            _gradientRectBatch.TransformsAddress = _transformTable.DeviceAddress;
            _gradientEllipseBatch.TransformsAddress = _transformTable.DeviceAddress;
            _patternBatch.TransformsAddress = _transformTable.DeviceAddress;
            _fractalBatch.TransformsAddress = _transformTable.DeviceAddress;
            _textBatch.TransformsAddress = _transformTable.DeviceAddress;   // glyph VS fetches the block's node matrix by slot
            var sceneClean = LastBuildKind == RenderBuildKind.Clean;
            _textBatch.SceneClean = sceneClean;
            _rectBatch.SceneClean = sceneClean;
            _ellipseBatch.SceneClean = sceneClean;
            _gradientRectBatch.SceneClean = sceneClean;
            _gradientEllipseBatch.SceneClean = sceneClean;
            _patternBatch.SceneClean = sceneClean;
            _fractalBatch.SceneClean = sceneClean;
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

        foreach (var group in _groups)
        foreach (var unit in group.Units)
        {
            // Group boundary (recording walks): reset this group's spliced-patch records once per group (re-derived by the
            // draw decisions below). Boundary detection instead of an outer block keeps the hot loop flat.
            if (_recording && !ReferenceEquals(group, _walkGroup))
            {
                _walkGroup = group;
                group.RectRuns.Clear();
                group.PatchableRectOnly = true;
                group.WalkVersion = _walkVersion;
            }

            // World transform read ONCE (frame-memoized): the bounds-cull below and the GPU re-bake use the SAME value, so
            // the cull can't approve "inside" while the GPU draws the element elsewhere (the spill).
            var wt = World(unit.Component);

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

            // Batches: item-background rects (lower layer) + text (upper layer), each one instanced draw. A clip-group
            // change (scissor, or the text atlas) flushes BOTH together (rect-under-text order); a non-batchable unit that
            // overlaps either flushes both first so it paints on top.
            if (device != null && unit is RectangleRenderUnit rru && _rectBatch.CanBatch(rru.RectPayload))
            {
                var rectBounds = LogicalBounds(unit.Component, wt);
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(0, rectBounds))   // 0 = rect layer
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
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(2, gradRectBounds))   // 2 = gradient-rect layer
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
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(1, ellipseBounds))   // 1 = ellipse layer
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
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(3, gradElBounds))   // 3 = gradient-ellipse layer
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
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(4, patElBounds))   // 4 = pattern layer
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
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(4, patternBounds))   // 4 = pattern layer
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
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || OverlapsHigherLayer(5, fractalBounds))   // 5 = fractal layer
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
            else if (device != null && unit is TextRenderUnit tru && tru.TextComponent is { } tc && _textBatch.CanBatch(tc, out var atlas))
            {
                if ((_batchOpen && !ScissorEquals(_batchScissor, scissor)) || !_textBatch.SameAtlas(atlas))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                // Node-aware, same as the rect batch: glyphs pack NODE-LOCAL with the node's transform-table slot, so a block
                // under a motion node (a scroll list) rides the O(1) slot-write fast path. ResolveBake returns the
                // node-relative transform + slot (world + slot 0 off any node).
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
                // General instanced fill (arbitrary tessellated geometry sharing a mesh): collect the fill and DEFER this
                // unit's fringe/stroke to the flush (drawn over the fill). A clip change flushes; the fill lands in its
                // natural z-layer (paint order), not all-at-once.
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
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
            else if (device != null && InstancedFillCollector.Enabled && unit is GeometryRenderUnit pgru && _instancedFill.CanBatchPattern(pgru))
            {
                // General instanced PATTERN/NOISE fill (arbitrary geometry, pattern-fill pass): same path as the gradient
                // one - the fill body is skipped (FillInstanced) and the unit's fringe/stroke draw at the flush.
                if (_batchOpen && !ScissorEquals(_batchScissor, scissor))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
                if (_instancedFill.TryAddPattern(pgru, wt, scissor, LogicalBounds(unit.Component, wt)))
                {
                    pgru.FillInstanced = true;
                    if (_recording) { group.PatchableRectOnly = false; MarkNodeNotAware(unit.Component); }   // instanced pattern: world-baked
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                pgru.FillInstanced = false;
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

    // The batches flush bottom-up (rect < ellipse < gradient-rect < gradient-ellipse < pattern < fractal < instanced < text), so a
    // HIGHER-layer batch draws ON TOP. A unit going into `layer` that OVERLAPS a pending higher-layer batch would be drawn
    // UNDER it - yet that batch holds units EARLIER in paint order, so this (later) unit belongs on top (a solid thumb
    // sitting on a gradient bar, a solid overlay over gradient content). Returning true here flushes the pending batches
    // first, dropping this unit into a fresh cycle that draws after them = correct paint order. Same-or-lower layers keep
    // their insertion order and are fine as-is; disjoint content never overlaps, so same-material tiles pay only O(1) checks.
    private bool OverlapsHigherLayer(int layer, Rect lb)
    {
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

        if (layer < 6 && (_instancedFill?.OverlapsPending(lb) ?? false))
        {
            return true;
        }

        if (layer < 7 && _textBatch.OverlapsPending(lb))
        {
            return true;
        }

        return false;
    }

    // Play this frame's composited animations for RIGHT NOW and push to GPU, without the loop thread or the property system
    // (see Compositor). Recompose ALL entries (Tick), then apply each by its channel.
    private void ApplyCompositedAnimations(IGraphicsDevice device)
    {
        if (device == null || _transformTable == null) return;
        if (!Compositor.Tick(_compositedBuf))   // recomposes matrices AND republishes paint snapshots
        {
            if (_compositedOwners.Count > 0) _compositedOwners.Clear();
            return;
        }

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
        if (_recording) _ops.Add(new RenderOp { Kind = RenderOpKind.Scissor, Scissor = scissor });
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
                    return false;   // a per-unit / text / instanced / no-longer-batchable dirty unit -> full walk
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
        ExecuteOps(device, fullScissor);
        return true;
    }

    // Does this unit's GPU data live in ONE retained SDF-batch slot we can rewrite in place? The whole precondition for
    // repainting without re-walking. Anything else (text, per-unit geometry, an instanced fill) keeps its bytes elsewhere.
    private bool IsSlotPatchable(IRenderUnit u)
    {
        if (u is RectangleRenderUnit rru)
        {
            if (_rectSlotByUnit.ContainsKey(u)) return _rectBatch.CanBatch(rru.RectPayload);
            if (_sdfSlotByUnit.TryGetValue(u, out var gr) && gr.Kind == SdfSlotKind.GradientRect)
                return _gradientRectBatch.CanBatch(rru.RectPayload);
            return false;
        }

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
    }

    // Draw a SPLICED partial by per-group batch-segment surgery + op-stream splice, then replay - the O(dirty-control) path
    // for unit-count changes (hover 0<->1 backgrounds, a live chart). Requirements per dirty group (else return false
    // BEFORE mutating anything -> full walk): every unit rect-batchable NOW; a group with retained runs must have been
    // rect-only on the last recording walk (its old draws are then fully described by RectRuns). A splice APPENDS the
    // re-baked group at the arena's end and INSERTS segment ops, neither reclaimed until a full walk resets Count/_ops -
    // so a sustained burst grows the arena + op stream unbounded (measured ops 30 -> 1300+), slow and increasingly fragile.
    // Cap the op stream: past MaxRetainedOps the splice yields to a full walk (its fallback), which recompacts.
    private bool TrySplicedPatch(IGraphicsDevice device, Rect2D fullScissor)
    {
        // Moved motion nodes first (same as TryPartialReplay): rewrite their matrices, bail on non-aware content.
        if (!RefreshMovedNodes(device)) return false;

        // Op stream grown too long from accumulated splices -> recompact with a full walk before it mis-replays.
        if (_ops.Count > MaxRetainedOps) return false;

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
            if (walked && !group.PatchableRectOnly) return false;

            var items = new List<RectItem>(group.Units.Count);
            var scissor = fullScissor;
            var haveScissor = false;
            foreach (var u in group.Units)
            {
                if (u is not RectangleRenderUnit rru || !_rectBatch.CanBatch(rru.RectPayload)) return false;
                u.SetEffectiveOpacity(EffectiveOpacity(u.Component));   // a splice may ride an opacity cascade too
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

        if (appendTotal > _rectBatch.PatchCapacityLeft) 
            return false;   // arena full - full walk compacts it

        // A splice that INSERTS rect segments (a 0->N appear: a selection/hover highlight materializing) anchors them only by
        // neighboring groups' rect runs - it has no handle on where the instanced-fill flush sits in the op stream, so the
        // appended segment can land AFTER it and the highlight would draw OVER the instanced geometry (a selection accent on
        // top of an instanced star instead of behind it), flipping z as splices and full walks alternate. When the frame has
        // an instanced flush, yield any INSERTING splice to the full walk (it emits rects before the instanced flush = correct
        // z). Excise-only (a highlight vanishing) and in-place recolors don't insert, so they stay on the fast path.
        if (_opsHaveInstancedFlush && appendTotal > 0) 
            return false;

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
            // segment whose op is inserted right after the original - [before][after] keeps every other item's order. The
            // FIRST run's position is the insertion anchor so the group's new items draw at the same paint position.
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
                // group with a retained rect run (split its run's segment at the run start so our op sits exactly at our
                // paint rank), else fall back to the nearest PRECEDING group's run. A mid-phase-2 bail here is SAFE: every
                // already-patched group is self-consistent, and the caller's full walk re-records the whole frame anyway.
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

        // Following group with a run: split its run's segment AT the run start; our op goes after the 'before' piece (the
        // successor's items keep drawing after us via the split-off remainder).
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

}
