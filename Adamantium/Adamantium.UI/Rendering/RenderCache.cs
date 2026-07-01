using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
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

    private readonly IRenderUnitFactory _renderUnitFactory;

    // Last render scale seen in ProcessCommands; maps a unit's window-logical clip rect to framebuffer-pixel scissor.
    private double _renderScale = 1.0;

    public RenderCache(IDrawingContext context, IRenderUnitFactory renderUnitFactory)
    {
        _drawingContext = context;
        _drawingContextInternal = (IDrawingContextInternal)context;
        _renderUnitFactory = renderUnitFactory;
    }
    
    public void BuildFromVisualTree(IRootVisualComponent visualRoot)
    {
        _commands.Clear();
        BuildRenderCommands(visualRoot);
    }

    /// <summary>
    /// Builds units from a FLAT list of components (the adorner stage), instead of walking a visual tree. Each
    /// component renders and is cached by RenderId exactly as in the tree build; units of components no longer in the
    /// list (e.g. a deselected adorner) are disposed. Used for overlays that aren't part of the content tree.
    /// </summary>
    public void BuildFromComponents(IReadOnlyList<IUIComponent> components, Matrix4x4F projectionMatrix)
    {
        _commands.Clear();
        _renderUnits.Clear();

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
            var transform = unit.Component.WorldTransform;
            unit.Update(transform, projectionMatrix, renderScale);
        }
    }

    private Matrix4x4F _projectionMatrix;

    // CPU pre-transform text batch aggregator (docs/TEXT_GLYPH_BATCH_PLAN.md §9 Stage 2). Created lazily on the first
    // Render that has a device (GPU-free test renders never batch). Frame-scoped state lives inside it.
    private TextBatchCollector _textBatch;

    // Item-background batch (the "подложки" instancing). Sibling of the text batch: solid rounded-rect fills batched
    // into one SDF-AA'd instanced draw. Rects are the LOWER layer - FlushBatches draws rects THEN text. Both batches
    // share one clip GROUP (_batchScissor): a scissor (or text-atlas) change flushes both together, preserving order.
    private RectBatchCollector _rectBatch;
    private Rect2D _batchScissor;
    private bool _batchOpen;

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
        var scissorNarrowed = false;   // whether the active scissor is currently narrower than fullScissor

        // Text + item-background batches: reset per frame. Device renders only - GPU-free tests skip batching.
        if (device != null)
        {
            _textBatch ??= new TextBatchCollector();
            _rectBatch ??= new RectBatchCollector();
            _textBatch.BeginFrame(device);
            _rectBatch.BeginFrame(device);
            _batchOpen = false;
        }

        foreach (var unit in _renderUnits)
        {
            // Read the world transform ONCE: the bounds-cull below evaluates it and the GPU is re-baked with the very
            // same value before drawing. Using one read for both the cull decision and the re-bake keeps them from
            // diverging (the cull approving "inside" while the GPU draws the element elsewhere = the off-viewport spill).
            var wt = unit.Component.WorldTransform;

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
                if (_rectBatch.TryAdd(rru.RectPayload, wt, rru.GeometryRenderer.RenderData.Opacity, scissor, LogicalBounds(unit.Component, wt)))
                {
                    _batchScissor = scissor;
                    _batchOpen = true;
                    continue;
                }
                // else: rotated/overflow -> fall through to the per-unit draw below
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
            else if (device != null && (_rectBatch.Active || _textBatch.Active))
            {
                // A non-batchable unit that overlaps either pending batch: flush both first so this unit paints OVER
                // them, as its later source order requires. Spatially disjoint units (a list's items) don't flush.
                var lb = LogicalBounds(unit.Component, wt);
                if (_rectBatch.OverlapsPending(lb) || _textBatch.OverlapsPending(lb))
                    FlushBatches(device, fullScissor, ref scissorNarrowed);
            }

            if (device != null)
            {
                if (clipped)
                {
                    device.SetScissors(scissor);
                    scissorNarrowed = true;
                }
                else if (scissorNarrowed)
                {
                    // First unclipped unit after a clipped one (or after a flush): restore the full window scissor.
                    device.SetScissors(fullScissor);
                    scissorNarrowed = false;
                }
            }

            unit.Render();
        }

        // Drain the tail batches (rects under text), then leave the device on the full scissor for the next pass.
        if (device != null) FlushBatches(device, fullScissor, ref scissorNarrowed);
        if (scissorNarrowed) device.SetScissors(fullScissor);
    }

    // A unit's own viewport (local 0,0..RenderSize) mapped into window-logical space - the same box ResolveScissor
    // clips against, reused here for the batches' paint-order overlap test.
    private static Rect LogicalBounds(IUIComponent component, Matrix4x4F worldTransform)
        => new Rect(0, 0, component.RenderSize.Width, component.RenderSize.Height).TransformToAABB(worldTransform);

    // Flush both batches in LAYER order - item-background rects first, then text on top - and mark the group closed.
    // Both Flush calls leave the device on fullScissor, so the per-unit scissor state resets to "not narrowed".
    private void FlushBatches(IGraphicsDevice device, Rect2D fullScissor, ref bool scissorNarrowed)
    {
        _rectBatch.Flush(device, fullScissor, _projectionMatrix);
        _textBatch.Flush(device, fullScissor, _projectionMatrix);
        scissorNarrowed = false;
        _batchOpen = false;
    }

    private static bool ScissorEquals(Rect2D a, Rect2D b)
        => a.Offset.X == b.Offset.X && a.Offset.Y == b.Offset.Y
           && a.Extent.Width == b.Extent.Width && a.Extent.Height == b.Extent.Height;

    // The scissor for a unit: the intersection of every ancestor viewport that ClipToBounds (in framebuffer pixels),
    // or fullScissor if none clip. `clipped` is false in the latter case so the caller keeps the window scissor.
    // `cull` is true when the unit's own bounds fall ENTIRELY outside that clip (so the caller skips drawing it).
    private Rect2D ResolveScissor(IUIComponent component, Matrix4x4F worldTransform, Rect2D fullScissor, out bool clipped, out bool cull)
    {
        Rect? clip = null;
        for (var c = component; c != null; c = c.VisualParent)
        {
            if (!c.ClipToBounds) continue;
            // The element's own viewport (local 0,0..RenderSize) mapped into window-logical space by its WorldTransform.
            var rect = new Rect(0, 0, c.RenderSize.Width, c.RenderSize.Height).TransformToAABB(c.WorldTransform);
            clip = clip is { } existing ? existing.Intersect(rect) : rect;
        }

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
    
    private void BuildRenderCommands(IRootVisualComponent visualRoot)
    {
        _renderUnits.Clear();
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

            // Capture dirtiness BEFORE Render: a clean control's Render() is a no-op (records nothing),
            // so an empty command list means "reuse the cached units". A dirty control re-records, so an
            // empty list then means "this control now draws nothing" and its stale units must be cleared.
            var wasGeometryValid = component.IsGeometryValid;

            _drawingContextInternal.Clear();
            component.Render(_drawingContext);
            ProcessRenderCommands(component, projectionMatrix, wasGeometryValid);

            foreach (var uiComponent in component.VisualChildren.Reverse())
            {
                stack.Push(uiComponent);
            }
        }

        ReconcileDetachedControls();
    }

    private void ProcessRenderCommands(IUIComponent component, Matrix4x4F projectionMatrix, bool wasGeometryValid)
    {
        var drawCommands = _drawingContextInternal.GetDrawCommands();
        if (drawCommands.Count > 0)
        {
            bool isNewControl = false;
            if (!_unitsByControl.TryGetValue(component.RenderId, out var units))
            {
                units = new List<IRenderUnit>();
                _unitsByControl[component.RenderId] = units;
                isNewControl = true;
            }

            for (int i = 0; i < drawCommands.Count; i++)
            {
                var command = drawCommands[i];
                command.RenderData.ProjectionMatrix = projectionMatrix;
                if (i >= units.Count || isNewControl)
                {
                    var unit = _renderUnitFactory.CreateRenderUnitFromCommand(command);
                    units.Add(unit);
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
                        unit = _renderUnitFactory.CreateRenderUnitFromCommand(command);
                        units[i] = unit;
                    }
                }
            }

            // Remove extra units
            if (units.Count > drawCommands.Count)
            {
                for (int i = drawCommands.Count; i < units.Count; i++)
                    units[i].DeferDispose();

                units.RemoveRange(drawCommands.Count, units.Count - drawCommands.Count);
            }

            _unitsByControl[component.RenderId] = units;
            _renderUnits.AddRange(units);
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