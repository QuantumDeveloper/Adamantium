using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering.Retained;
using Adamantium.UI.Rendering.RenderUnits;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

public class RenderCache
{
    private readonly List<DrawCommand> _commands = new();
    private readonly List<IRenderUnit> _renderUnits = new();
    private IDrawingContext _drawingContext;
    private IDrawingContextInternal _drawingContextInternal;
    private readonly Dictionary<Guid, List<IRenderUnit>> _unitsByControl = new Dictionary<Guid, List<IRenderUnit>>();

    // Paint-order (DFS visit) index of each component, assigned during the full walk - kept even for a component that
    // rendered 0 units. Lets a PARTIAL re-render whose draw-command COUNT changed (a hover background appearing 0->1, or
    // vanishing) splice that ONE component's units into the retained paint-order list at the right spot, instead of
    // falling back to a full tree walk that re-renders every element (the hover / mouse-move FPS cliff on a big list).
    private readonly Dictionary<Guid, int> _orderByControl = new();
    private IRootVisualComponent _lastVisualRoot;             // for an on-demand order re-walk (a component appearing mid-partial)
    private readonly Stack<IUIComponent> _orderStack = new(); // reused by ReassignOrders (no per-call alloc)
    private readonly HashSet<Guid> _orderVisited = new();

    private readonly IRenderUnitFactory _renderUnitFactory;

    // Reusable snapshot of the geometry-dirty set for the partial pass: ReRenderInPlace re-renders components, and a
    // component's Render can mark MORE geometry dirty mid-pass - enumerating the live set then throws. Reused each build
    // (no per-frame allocation), same pattern as LayoutManager's promote buffer.
    private readonly List<IUIComponent> _geometryDirtyBuffer = new();

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
    /// <item>only moves/geometry changed (non-structural) -> re-render just the dirty components IN PLACE, keeping the
    /// retained <c>_renderUnits</c> paint-order list;</item>
    /// <item>structural change (or first build, or a partial that turned out structural) -> a full tree walk.</item>
    /// </list>
    /// </summary>
    public void BuildFromVisualTree(IRootVisualComponent visualRoot)
    {
        // Fully clean: nothing changed since last build -> re-draw the retained units as-is. Keep the transform memo:
        // nothing moved, so last frame's world transforms are still correct.
        if (_built && !RenderDirty.HasWork)
        {
            LastBuildKind = RenderBuildKind.Clean;
            return;
        }

        // Non-structural change on an existing scene -> PARTIAL update: re-render only the geometry-dirty components in
        // place. Either way _renderUnits + _unitsByControl stay retained. Drop the frame-scoped world/clip memos ONLY on
        // a MOVE (transform-dirty) - then last frame's baked transforms are stale and must be recomputed. A GEOMETRY-only
        // partial (a hover recolouring a tile) moved nothing, so the memos are still valid: keeping them lets the render
        // pass reuse cached world transforms + clips instead of recomputing them for every one of thousands of units
        // (the O(N) that made a hover cost ~2x a clean frame on a big list).
        if (_built && !RenderDirty.IsStructural)
        {
            if (RenderDirty.IsTransform)
            {
                _worldCache.Clear();
                _clipCache.Clear();
            }

            // Snapshot the dirty set: ReRenderInPlace re-renders each component, and a component's Render can mark MORE
            // geometry dirty (e.g. an image finishing decode), ADDING to the live RenderDirty.Geometry set mid-loop and
            // throwing "collection was modified". Copy into a reusable buffer and iterate that.
            _geometryDirtyBuffer.Clear();
            _geometryDirtyBuffer.AddRange(RenderDirty.Geometry);

            var fellBack = false;
            foreach (var component in _geometryDirtyBuffer)
            {
                if (!ReRenderInPlace(component)) { fellBack = true; break; }
            }

            // Partial completes ONLY if nothing structural surfaced and NO new geometry was marked during the pass (the
            // set didn't grow). If a render re-marked geometry, fall through to a full walk so that change isn't dropped.
            if (!fellBack && !RenderDirty.IsStructural && RenderDirty.Geometry.Count == _geometryDirtyBuffer.Count)
            {
                LastBuildKind = RenderBuildKind.Partial;   // no full walk (only the dirty components' unit contents)
                LastBuildTransformDirty = RenderDirty.IsTransform;   // geometry-only partial -> nothing moved -> proc can be skipped
                RenderDirty.Clear();
                return;
            }
            // a structural change or a new invalidation surfaced during the partial pass -> fall through to a full walk
        }

        // Full walk: first build, a structural change, or a partial that surfaced one.
        LastBuildKind = RenderBuildKind.Full;
        LastBuildTransformDirty = true;   // a full walk rebuilds the paint-order list; positions must be re-baked
        _commands.Clear();
        _worldCache.Clear();
        _clipCache.Clear();
        BuildRenderCommands(visualRoot);
        _built = true;
        RenderDirty.Clear();
    }

    // Re-render ONE already-cached component IN PLACE (its geometry went dirty). Returns false - "needs a full walk" -
    // only when this component has no recorded paint position yet (never in a full build). On a same-shape update the
    // unit objects are reused via UpdateWithDrawCommand (the retained _renderUnits already references them - no change).
    // On a COUNT change - a hover background appearing (0->1 commands) or vanishing (1->0), the mouse-move hover cliff -
    // it rebuilds just this component's units and SPLICES them into the paint-order list at the component's recorded
    // DFS rank, instead of forcing a full tree walk that re-renders every element.
    private bool ReRenderInPlace(IUIComponent component)
    {
        if (component.Visibility != Visibility.Visible) return false;   // (no Render() run yet - nothing to undo)

        // Not in the live paint tree: DETACHED (no visual parent) or effectively hidden by a COLLAPSED ancestor. The full
        // walk never reaches such a component, so it has no paint rank and re-rendering it draws nothing - yet it used to
        // force a FULL tree rebuild EVERY frame it was geometry-dirty (a detached/pooled text block, a text block inside a
        // collapsed panel, an auto-hide scrollbar's parts). Skip it: it holds no units (a real detach/collapse is
        // STRUCTURAL and already removed them via a full walk), so there is nothing to draw or reclaim here.
        if (!component.IsAttachedToVisualTree) return true;
        for (var a = component.VisualParent; a != null; a = a.VisualParent)
            if (a.Visibility != Visibility.Visible) return true;

        _drawingContextInternal.Clear();
        component.Render(_drawingContext);   // NB: consumes the dirty flag (Render sets IsGeometryValid back to true)
        var drawCommands = _drawingContextInternal.GetDrawCommands();

        _unitsByControl.TryGetValue(component.RenderId, out var units);
        var oldCount = units?.Count ?? 0;

        // Fast path: same command count and every unit still matches -> update in place; the paint-order list is untouched
        // (it already holds these exact unit objects at the right spot).
        if (units != null && drawCommands.Count == oldCount && oldCount > 0)
        {
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

        // Count (or a unit type) changed. Splice this component's units into the retained paint-order list in place.
        if (!_orderByControl.ContainsKey(component.RenderId))
        {
            // The component has no recorded paint rank - it was invisible/absent during the last full walk and has now
            // appeared (e.g. an auto-hide ScrollBar fading in on mouse activity). Rather than re-render the WHOLE tree,
            // re-derive paint ranks with a cheap ORDER-ONLY walk (no Render, no unit work) so this one component can be
            // spliced in. O(N) dictionary writes (~tens of us) vs a full render of thousands of units.
            ReassignOrders();
            if (!_orderByControl.ContainsKey(component.RenderId))
                return false;   // genuinely not in the tree -> let the caller do a full walk
        }

        // Where the OLD block sits (contiguous). Capture BEFORE BuildUnitsFor mutates the list in place.
        var oldStart = oldCount > 0 ? _renderUnits.IndexOf(units[0]) : -1;
        if (oldCount > 0 && oldStart < 0) return false;   // shouldn't happen; be safe and fall back

        var newUnits = BuildUnitsFor(component, drawCommands, _projectionMatrix);

        if (oldStart >= 0)
        {
            _renderUnits.RemoveRange(oldStart, oldCount);
            _renderUnits.InsertRange(oldStart, newUnits);   // same slot -> paint order preserved
        }
        else if (newUnits.Count > 0)
        {
            // Old block was empty (a background appearing): insert by the component's DFS rank - before the first unit
            // whose component ranks after it.
            var order = _orderByControl[component.RenderId];
            var pos = _renderUnits.Count;
            for (var i = 0; i < _renderUnits.Count; i++)
            {
                if (_orderByControl.GetValueOrDefault(_renderUnits[i].Component.RenderId, int.MaxValue) > order) { pos = i; break; }
            }
            _renderUnits.InsertRange(pos, newUnits);
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
        _renderUnits.Clear();
        _worldCache.Clear();   // new frame: drop last frame's transform + clip memos
        _clipCache.Clear();

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
                ProcessRenderCommands(component, projectionMatrix, wasGeometryValid);
            }
        }

        // Free the units of any component dropped from the list since the last build.
        List<Guid> stale = null;
        foreach (var id in _unitsByControl.Keys)
            if (!present.Contains(id)) (stale ??= new List<Guid>()).Add(id);
        if (stale != null)
            foreach (var id in stale) RemoveAndDeferDispose(id);
    }

    /// <summary>
    /// Immediately disposes every cached render unit and empties the cache. The caller must ensure the GPU is
    /// idle first (e.g. after a DeviceWaitIdle). Used by the off-screen designer, which builds a brand-new tree
    /// each render: those controls never detach (each owns its own root window), so the attachment-based
    /// reconciliation can't reclaim them - the designer resets the cache between renders instead.
    /// </summary>
    public void DisposeUnits()
    {
        foreach (var units in _unitsByControl.Values)
        {
            foreach (var unit in units)
                unit?.Dispose();
        }

        _unitsByControl.Clear();
        _renderUnits.Clear();
    }

    public void ProcessCommands(Matrix4x4F projectionMatrix, double renderScale)
    {
        _renderScale = renderScale;
        _projectionMatrix = projectionMatrix;
        foreach (var unit in _renderUnits)
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

    private Matrix4x4F World(IUIComponent c)
    {
        if (_worldCache.TryGetValue(c, out var m)) return m;
        var parent = c.VisualParent;
        m = parent != null ? c.LocalTransform * World(parent) : c.LocalTransform;
        _worldCache[c] = m;
        return m;
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
        var parentClip = CumulativeClip(c.VisualParent);
        var result = parentClip;
        if (c.ClipToBounds)
        {
            var rect = new Rect(0, 0, c.RenderSize.Width, c.RenderSize.Height).TransformToAABB(World(c));
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
        foreach (var unit in _renderUnits)
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

        var scissorNarrowed = false;   // whether the active scissor is currently narrower than fullScissor

        _recording = device != null;   // a device walk records its op stream for a later clean-frame replay
        if (_recording) { _ops.Clear(); _opsReplayable = true; }

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

        foreach (var unit in _renderUnits)
        {
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
                if (cull) continue;
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
                if (_rectBatch.TryAdd(rru.RectPayload, wt, rru.FillOpacity, scissor, LogicalBounds(unit.Component, wt)))
                {
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
                if (_gradientRectBatch.TryAdd(grru.RectPayload, wt, grru.FillOpacity, scissor, LogicalBounds(unit.Component, wt)))
                {
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
                if (_ellipseBatch.TryAdd(eru.EllipsePayload, wt, eru.FillOpacity, scissor, LogicalBounds(unit.Component, wt)))
                {
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
                if (_gradientEllipseBatch.TryAdd(geru.EllipsePayload, wt, geru.FillOpacity, scissor, LogicalBounds(unit.Component, wt)))
                {
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
                if (_textBatch.TryAdd(tc, wt, scissor, atlas, LogicalBounds(unit.Component, wt)))
                {
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;   // baked into the batch - drawn at the next flush
                }
                // else: rotated/sheared or overflow -> fall through to the per-block direct draw below
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

            unit.Render();
            if (_recording) _ops.Add(new RenderOp { Kind = RenderOpKind.Unit, Unit = unit });
        }

        // Drain the tail batches (rects under fills under text), then leave the device on the full scissor for next pass.
        if (device != null) FlushBatches(device, fullScissor, ref scissorNarrowed);
        if (scissorNarrowed) { device.SetScissors(fullScissor); RecordScissor(fullScissor); }

        if (_recording) { _opsRecorded = true; _recording = false; }
    }

    private void RecordScissor(Rect2D scissor)
    {
        if (_recording) _ops.Add(new RenderOp { Kind = RenderOpKind.Scissor, Scissor = scissor });
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
    private static Rect LogicalBounds(IUIComponent component, Matrix4x4F worldTransform)
        => new Rect(0, 0, component.RenderSize.Width, component.RenderSize.Height).TransformToAABB(worldTransform);

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

        // Is the unit's own owner fully outside the clip on any axis? Then none of it shows -> let the caller cull it.
        // Use the SAME world transform the caller will bake into the GPU draw, not a fresh component.WorldTransform read:
        // layout runs on another thread, so a re-read here could differ from what is actually drawn (cull says "inside"
        // while the GPU paints it outside -> the off-viewport spill).
        var bounds = new Rect(0, 0, component.RenderSize.Width, component.RenderSize.Height).TransformToAABB(worldTransform);
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
        _orderByControl.Clear();
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

    private void BuildRenderCommands(IRootVisualComponent visualRoot)
    {
        _renderUnits.Clear();
        _orderByControl.Clear();
        _lastVisualRoot = visualRoot;
        var order = 0;
        var projectionMatrix = visualRoot.GetProjectionMatrix();
        var stack = new Stack<IUIComponent>();
        var visited = new HashSet<Guid>();
        stack.Push(visualRoot);
        while (stack.Count > 0)
        {
            var component = stack.Pop();

            if (component.Visibility != Visibility.Visible) continue;

            // A component must render exactly once per frame. If the visual tree somehow makes one reachable twice in
            // this walk (e.g. a templated content host whose child is referenced from two places), processing it again
            // would add its units to _renderUnits a second time -> every such element is drawn TWICE (overdraw at the
            // same spot). Guard against that here so each component (and its subtree) is built once.
            if (!visited.Add(component.RenderId)) continue;

            _orderByControl[component.RenderId] = order++;   // paint-order rank (for the incremental partial-patch)

            // Capture dirtiness BEFORE Render: a clean control's Render() is a no-op (records nothing),
            // so an empty command list means "reuse the cached units". A dirty control re-records, so an
            // empty list then means "this control now draws nothing" and its stale units must be cleared.
            var wasGeometryValid = component.IsGeometryValid;

            _drawingContextInternal.Clear();
            component.Render(_drawingContext);
            ProcessRenderCommands(component, projectionMatrix, wasGeometryValid);

            PushChildrenInPaintOrder(stack, component.VisualChildren);
        }

        ReconcileDetachedControls();
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
    // replace a type-changed one, create the extra, dispose the surplus. Updates _unitsByControl but does NOT touch the
    // paint-order list (_renderUnits) - the caller places them (full build appends; a partial patch splices by order).
    private List<IRenderUnit> BuildUnitsFor(IUIComponent component, IReadOnlyList<IDrawCommand> drawCommands, Matrix4x4F projectionMatrix)
    {
        if (!_unitsByControl.TryGetValue(component.RenderId, out var units))
        {
            units = new List<IRenderUnit>();
            _unitsByControl[component.RenderId] = units;
        }

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

        return units;
    }

    private void ProcessRenderCommands(IUIComponent component, Matrix4x4F projectionMatrix, bool wasGeometryValid)
    {
        var drawCommands = _drawingContextInternal.GetDrawCommands();
        if (drawCommands.Count > 0)
        {
            _renderUnits.AddRange(BuildUnitsFor(component, drawCommands, projectionMatrix));
        }
        else
        {
            // No commands this frame. Distinguish the two cases (see wasGeometryValid in BuildRenderCommands):
            //  - was clean: Render() didn't re-record -> reuse the cached units.
            //  - was dirty: the control re-rendered to nothing -> clear its stale units so they stop drawing.
            if (_unitsByControl.TryGetValue(component.RenderId, out var units))
            {
                if (wasGeometryValid)
                {
                    _renderUnits.AddRange(units);
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
        foreach (var pair in _unitsByControl)
        {
            var units = pair.Value;
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
    /// still be using them). Build-phase only (EndDraw). Idempotent.
    /// </summary>
    private void RemoveAndDeferDispose(Guid renderId)
    {
        if (!_unitsByControl.Remove(renderId, out var units)) return;

        foreach (var unit in units)
            unit?.DeferDispose();
    }
}