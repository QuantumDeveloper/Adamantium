using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.Vulkan.Core;
using Adamantium.UI.Rendering.RenderUnits;

namespace Adamantium.UI.Rendering;

public partial class RenderCache
{
    // Replay a recorded frame's op stream (a Clean frame): re-issue scissor changes, per-unit draws and batch segments in
    // order. No walk, no bake, no upload - the batch buffers still hold last frame's bytes, and each unit's RenderData its
    // baked transform (nothing moved on a Clean frame).
    private void ExecuteOps(IGraphicsDevice device, Rect2D fullScissor)
    {
        // LAYER by layer, and inside a layer in the order it was recorded. The two are the same sequence - a layer owns a
        // contiguous range of the stream - and saying it this way is what makes the structure of a recorded frame legible:
        // the stream is a flat list, the layers are what it MEANS.
        foreach (var layer in _layers)
        for (var i = layer.OpFirst; i < layer.OpFirst + layer.OpCount; i++)
        {
            var op = _ops[i];
            switch (op.Kind)
            {
                case RenderOpKind.Scissor:
                    device.SetScissors(op.Scissor);
                    break;
                case RenderOpKind.Unit:
                    // A per-unit draw bakes its full world into RenderData at RECORD time and never reads the transform
                    // slot - while every batched draw follows its slot matrix LIVE. So on any replay where something has
                    // moved, the two disagree: measured on a scrolling tab strip, the batched fill had followed but this
                    // Border was still drawn 48 px back, one scroll step behind. That gap is the flicker.
                    //
                    // Only the compositor-driven ones. Re-pointing EVERY per-unit draw looks like it fixes the opposite
                    // problem (a per-unit outline lagging its batched fill), but it introduces the mirror of it: the
                    // batched half still follows its slot matrix, which a replay does not recompute, so the two halves
                    // end up a fraction of a step apart and the element jitters. Coherence on a replay comes from
                    // refusing to replay once the layout moved (see _layoutChangedSinceRecord), not from updating one
                    // half of the frame.
                    //
                    // ...and the same holds for a unit under a node that MOVED this frame: RefreshMovedNodes has just
                    // written that node's matrix, so both halves are being taken from one position in one frame - which
                    // is the condition the paragraph above is really about.
                    RepointIfItMoved(op.Unit);
                    op.Unit.Render();
                    break;
                case RenderOpKind.Segment:
                    switch (op.Batch)
                    {
                        case 0: _rectBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 1: _ellipseBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 3: _gradientRectBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 4: _gradientEllipseBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 5: _patternBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 6: _fractalBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 7: _texRectBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 8: _haloUnder.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 9: _haloOver.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 10: _haloLivingUnder.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 11: _haloLivingOver.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        case 12: _polygonBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        default: _textBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                    }
                    break;
                case RenderOpKind.InstancedFlush:
                    _instancedFill.ReplayFlush(op.SegId, fullScissor, _projectionMatrix);
                    break;
            }
        }
    }

    // A draw that baked its transform at RECORD time, on a frame where its element is somewhere else: re-point it. Two
    // kinds of mover qualify, and only those two - the compositor, which moved it on this thread, and a motion node whose
    // matrix this frame has just rewritten (RefreshMovedNodes). Both mean the batched half and this half are being taken
    // from one position in one frame; re-pointing anything else is what tears a frame in two.
    private void RepointIfItMoved(IRenderUnit unit)
    {
        if (_compositedOwners.Count == 0 && _movedNodeOwners.Count == 0) return;
        if (unit.Component is not { } c) return;
        if (!_compositedOwners.Contains(c) && !(_movedNodeOwners.Count > 0 && _movedNodeOwners.Contains(NodeOf(c)))) return;

        unit.Update(World(c), _projectionMatrix, _renderScale);
    }

    // WHERE a band gets its distance from - asked once and answered here, so the still band and the living one can never
    // disagree about what shape they are wrapping. A unit with no answer simply wears no band rather than a wrong one.
    private static bool TryHaloShape(IRenderUnit unit, out Rect shape, out ProceduralGeometry.CornerRadius corners, out HaloShape kind,
        out ITexture field, out double fieldRange)
    {
        shape = default;
        corners = ProceduralGeometry.CornerRadius.Empty;
        kind = HaloShape.RoundedRect;
        field = null;
        fieldRange = 0;

        switch (unit)
        {
            case RectangleRenderUnit rect:
                shape = rect.RectPayload.DestinationRect;
                corners = rect.RectPayload.CornerRadius;
                kind = HaloShape.RoundedRect;
                return true;
            case EllipseRenderUnit ell when ell.EllipsePayload.StartAngle <= 0.0 && ell.EllipsePayload.SweepAngle >= 360.0:
                shape = ell.EllipsePayload.DestinationRect;
                kind = HaloShape.Ellipse;
                return true;
            // Arbitrary geometry: no closed-form distance, so the band reads one baked per shape. The box comes from the
            // MESH, not the element - a Polygon's element box and its outline's box are not the same thing.
            case GeometryRenderUnit geom:
                field = geom.HaloField(out shape, out fieldRange);
                kind = HaloShape.Field;
                return field != null;
            // A regular polygon has an exact field of its own, but the halo pass reads rect/ellipse in closed form and
            // everything else from a baked one - so it takes the baked route too, and pays for the mesh only here.
            case RegularPolygonRenderUnit poly:
                field = poly.HaloField(out shape, out fieldRange);
                kind = HaloShape.Field;
                return field != null;
            default:
                return false;
        }
    }

    // Collect this unit's LIVING aura - the band whose reach wanders. Its own collector and its own pass: a still band
    // must not pay for the noise, so the two are never mixed into one draw.
    private bool CollectLivingHalo(IGraphicsDevice device, IRenderUnit unit, Matrix4x4F wt, Rect2D scissor, bool inner)
    {
        if (unit.RenderData.LivingHalo is not { } band || band.Inner != inner) return false;
        if (!TryHaloShape(unit, out var shape, out var corners, out var kind, out var field, out var fieldRange)) return false;

        ref var batch = ref inner ? ref _haloLivingOver : ref _haloLivingUnder;
        if (batch == null)
        {
            batch = new HaloLivingCollector { BatchId = (byte)(inner ? 11 : 10), TransformsAddress = _transformTable?.DeviceAddress ?? 0 };
            batch.BeginFrame(device);
        }

        if (!batch.SameField(field)) return false;

        var bounds = LogicalBounds(unit.Component, wt);
        var bakeWorld = ResolveBake(device, unit.Component, wt, out var slot);
        if (!batch.TryAdd(band, shape, corners, kind, bakeWorld, unit.RenderData.Opacity, scissor, bounds,
                band.Color, slot, field, fieldRange))
        {
            return false;
        }

        if (inner) _haloOverOwner = unit.Component;
        _batchScissor = scissor;
        _batchOpen = true;
        return true;
    }

    // Collect this unit's soft bands (aura / shadow) into the halo batch. The SHAPE comes from the payload - only a rect
    // or a full ellipse has a signed distance to read, and arbitrary geometry is a separate story (a distance field baked
    // per mesh key). A shape that has none simply wears no band rather than getting a wrong one.
    private bool CollectHalo(IGraphicsDevice device, IRenderUnit unit, Matrix4x4F wt, Rect2D scissor, bool inner)
    {
        var bands = unit.RenderData.Halo;
        if (!HaloRectCollector.HasSide(bands, inner)) return false;

        if (!TryHaloShape(unit, out var shape, out var corners, out var kind, out var field, out var fieldRange))
        {
            return false;
        }

        // Created on FIRST use: the GPU ring is dead weight in the windows that never draw a halo, which is most of them.
        ref var batch = ref inner ? ref _haloOver : ref _haloUnder;
        if (batch == null)
        {
            batch = new HaloRectCollector { BatchId = (byte)(inner ? 9 : 8), TransformsAddress = _transformTable?.DeviceAddress ?? 0 };
            batch.BeginFrame(device);
        }

        // One field per draw, so a second shape's field ends the run - the same rule the textured batch follows.
        if (!batch.SameField(field))
        {
            return false;
        }

        var haloBounds = LogicalBounds(unit.Component, wt);
        var bakeWorld = ResolveBake(device, unit.Component, wt, out var slot);
        if (!batch.TryAdd(bands, inner, shape, corners, kind, bakeWorld,
                unit.RenderData.Opacity, scissor, haloBounds, slot, field, fieldRange))
        {
            return false;
        }

        if (inner) _haloOverOwner = unit.Component;
        _batchScissor = scissor;
        _batchOpen = true;
        return true;
    }

    // A unit's own viewport (local 0,0..RenderSize) in window-logical space - what ResolveScissor clips against, reused
    // here for the batches' paint-order overlap test.
    private Rect LogicalBounds(IUIComponent component, Matrix4x4F worldTransform)
    {
        var size = ApplySnap(component).RenderSize;
        return new Rect(0, 0, size.Width, size.Height).TransformToAABB(worldTransform);
    }

    // Flush all batches in LAYER order - item-background rects, then instanced geometry fills (+ their deferred
    // fringe/stroke), then text on top - and mark the group closed. Each Flush leaves the device on fullScissor.
    private void FlushBatches(IGraphicsDevice device, Rect2D fullScissor, ref bool scissorNarrowed)
    {
        // FIRST, before every fill in this clip group: that is what puts an OUTER aura / shadow under the shapes it
        // belongs to. Its INNER twin is flushed after the fills instead - see below.
        if (_haloUnder != null) RecordSegment(8, _haloUnder.Flush(device, fullScissor, _projectionMatrix));
        if (_haloLivingUnder != null) RecordSegment(10, _haloLivingUnder.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(0, _rectBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(1, _ellipseBatch.Flush(device, fullScissor, _projectionMatrix));
        if (_polygonBatch != null) RecordSegment(12, _polygonBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(3, _gradientRectBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(4, _gradientEllipseBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(5, _patternBatch.Flush(device, fullScissor, _projectionMatrix));   // pattern layer: after gradients, before instanced
        RecordSegment(6, _fractalBatch.Flush(device, fullScissor, _projectionMatrix));   // fractal layer: after pattern, before instanced
        if (_texRectBatch != null) RecordSegment(7, _texRectBatch.Flush(device, fullScissor, _projectionMatrix));   // textured layer: after fractal, before instanced
        // The general instanced-fill flush is retained too: Flush records the group and returns its index, replayed via
        // ReplayFlush - so a vector icon no longer disables replay for the whole window.
        if (_instancedFill != null)
        {
            var fi = _instancedFill.Flush(fullScissor, _projectionMatrix);
            if (_recording && fi >= 0)
            {
                RecordOp(new RenderOp { Kind = RenderOpKind.InstancedFlush, SegId = fi, Order = _recordOrder });
            }
        }

        // An INNER band lies inside the shape, so it belongs OVER every fill - drawn under, the shape's own fill covers
        // it and nothing is on screen at all. Still below text: a glow is chrome, a label is content.
        if (_haloOver != null)
        {
            RecordSegment(9, _haloOver.Flush(device, fullScissor, _projectionMatrix));
            _haloOverOwner = null;
        }
        if (_haloLivingOver != null)
        {
            RecordSegment(11, _haloLivingOver.Flush(device, fullScissor, _projectionMatrix));
        }

        RecordSegment(2, _textBatch.Flush(device, fullScissor, _projectionMatrix));
        if (_flushedSomething) { LayerProbe.Cycle(); _flushedSomething = false; }

        // The cycle is over, and with it the LAYER: a flush happens precisely when the next draw can no longer be
        // reordered with what is pending, so whatever is recorded from here belongs strictly after all of it.
        CloseLayer();
        scissorNarrowed = false;
        _batchOpen = false;
    }

    // Record a batch segment op (the immediate draw already happened in Flush; this only appends it for a clean-frame
    // replay). A Flush that drew nothing returns -1 and records nothing.
    // A LAYER is one flush cycle - the set whose mutual order does not matter (§5a). Counted here because this is where
    // one ends: two increments per cycle, which is what phase 3 is verified against.
    private bool _flushedSomething;

    private void RecordSegment(byte batch, int segId)
    {
        if (segId >= 0) { _flushedSomething = true; LayerProbe.Segment(); }
        if (!_recording || segId < 0) return;

        // A segment's paint span runs from the first group that filled it to the one being recorded when it flushed. Only
        // the RECT batch is ever spliced into, so only its span is tracked; for the others the span is a point, which is
        // all their ops are compared by.
        RecordOp(new RenderOp
        {
            Kind = RenderOpKind.Segment, Batch = batch, SegId = segId,
            Order = _recordOrder, OrderFirst = batch == 0 ? _rectSegStart : _recordOrder
        });
    }

    private static bool ScissorEquals(Rect2D a, Rect2D b)
        => a.Offset.X == b.Offset.X && a.Offset.Y == b.Offset.Y
           && a.Extent.Width == b.Extent.Width && a.Extent.Height == b.Extent.Height;

    // The scissor for a unit: the intersection of every ClipToBounds ancestor's viewport (framebuffer pixels), or
    // fullScissor if none clip (`clipped` false then). `cull` is true when the unit's own bounds fall ENTIRELY outside.
    private Rect2D ResolveScissor(IUIComponent component, Matrix4x4F worldTransform, Rect2D fullScissor, out bool clipped, out bool cull)
    {
        // Intersection of every ClipToBounds ancestor's viewport - memoized per component (units under one clip share it).
        var clip = CumulativeClip(component);

        cull = false;
        if (clip is not { } logical)
        {
            clipped = false;
            return fullScissor;
        }

        // A PERSPECTIVE world (a 3D-rotated tile: M34/M14/M24 carry the w term) can't be AABB-tested by the affine box
        // below (TransformToAABB does no w-divide -> garbage box -> the tilted tile VANISHED mid-flip). Rare and
        // pixel-clipped by the scissor anyway: skip the cull.
        if (worldTransform.M34 != 0 || worldTransform.M14 != 0 || worldTransform.M24 != 0)
        {
            clipped = true;
            return ToFramebufferScissor(logical, fullScissor);
        }

        // A unit under a render MOTION NODE is drawn through the node's slot matrix, which the O(1)-scroll replay REWRITES
        // every frame WITHOUT re-recording the op stream - so its record-time world is NOT where later frames draw it (an
        // off-viewport buffer row scrolls INTO view under the same recorded op). Culling it would drop it from the stream,
        // leaving the row to "materialise" a frame late. Don't cull motion-node units: the scissor still clips them, and
        // the realized window is bounded (viewport + a couple of buffer rows), so recording the few off-screen ones is cheap.
        if (NodeOf(component) != null)
        {
            clipped = true;
            return ToFramebufferScissor(logical, fullScissor);
        }

        // Is the owner fully outside the clip on any axis? Then let the caller cull it. Use the SAME world the caller will
        // bake into the GPU draw, not a fresh WorldTransform read: layout runs on another thread, so a re-read could differ
        // (cull says "inside" while the GPU paints it outside -> the off-viewport spill).
        var scissorSize = ApplySnap(component).RenderSize;
        var bounds = new Rect(0, 0, scissorSize.Width, scissorSize.Height).TransformToAABB(worldTransform);
        cull = bounds.Right <= logical.X || bounds.X >= logical.Right || bounds.Bottom <= logical.Y || bounds.Y >= logical.Bottom;

        clipped = true;
        return ToFramebufferScissor(logical, fullScissor);
    }

    // Window-logical rect -> Vulkan scissor in framebuffer pixels (logical x RenderScale), clamped to the window scissor so
    // it never exceeds the attachment and collapses to empty when fully scrolled out.
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

    // RECORD half of the full walk (DEVICE-FREE): DFS the tree, run component.Render, and COPY each component's commands
    // into the packet in paint order (the shared context is reused for the next component, so snapshot now). No GPU - the
    // applier realizes them (ApplyFullWalk). This is what lets the recorder run on the update thread (docs/RENDER_THREAD_PLAN.md).
    private void RecordFullWalk(IRootVisualComponent visualRoot, RenderPacket packet)
    {
        // Renumber from scratch with FRESH GAPS. Safe to mutate in place: the ranks no longer cross the seam (each draw
        // carries its own, and the applier stores it on the group).
        _orderByControl.Clear();
        _lastVisualRoot = visualRoot;
        long order = 0;
        packet.ProjectionMatrix = visualRoot.GetProjectionMatrix();
        // Reused, not re-allocated per frame: a full walk runs on EVERY structural frame and these grew to one entry per
        // component (~9000). Keyed by the COMPONENT, not RenderId (a reference hash is cheaper than a Guid hash).

        var stack = _walkStack;
        var visited = _walkVisited;
        stack.Clear();
        visited.Clear();
        stack.Push((visualRoot, false));
        while (stack.Count > 0)
        {
            var (component, hiddenByAncestor) = stack.Pop();

            // COLLAPSED leaves the paint order - it is out of the layout too, and nothing under it has a place. HIDDEN
            // does not: it holds its slot and its rank and simply paints nothing. Dropping it here is what made showing
            // it again unplaceable - no rank to splice into - and every hover affordance coming back cost the most
            // expensive frame there is, a full walk (measured: 120 of them, `dirtyNotPlaceable<Button>`).
            if (component.Visibility == Visibility.Collapsed) continue;

            // Hidden is INHERITED: hiding an element hides what is inside it. The walk used to stop at a hidden element
            // and so never reached its children at all; now that it walks through them to keep their ranks, it has to
            // carry the fact down itself - otherwise a hidden button's visible content goes on drawing, which is a close
            // button showing its glyph on every tab until the pointer touches one.
            var hidden = hiddenByAncestor || component.Visibility != Visibility.Visible;

            // A component must render exactly once per frame: if the tree makes one reachable twice (a content host
            // referenced from two places), it would join the paint order twice -> drawn TWICE (overdraw). Guard here.
            if (!visited.Add(component)) continue;

            _orderByControl[component.RenderId] = order;   // paint-order rank, SPARSE (see OrderGap)

            // Capture dirtiness BEFORE Render: a clean control's Render() is a no-op, so an empty command list means
            // "reuse the cached units"; a dirty one re-records, so an empty list then means "now draws nothing" -> clear
            // its stale units.
            var wasGeometryValid = component.IsGeometryValid;



            _drawingContextInternal.Clear();
            component.Render(_drawingContext);   // rendered even when hidden: that is what consumes its dirty flag
            var commands = hidden
                ? CopyCommands(System.Array.Empty<IDrawCommand>())   // holds its place, draws nothing
                : CopyCommands(_drawingContextInternal.GetDrawCommands());
            packet.Draws.Add(new ComponentDraw(component, commands, hidden ? false : wasGeometryValid, order, component.RenderClones));
            MirrorUnits(component, commands.Count, hidden ? false : wasGeometryValid);
            order += OrderGap;

            PushChildrenInPaintOrder(stack, component.VisualChildren, hidden);
        }

        // Mirror the applier's ReconcileDetachedControls: it frees the units of controls no longer in the tree, so the
        // recorder's own "who holds units" view must drop them too (this walk never visits them). Parked controls are
        // kept for the same reason the applier keeps them - they are coming back.
        _staleUnitIds.Clear();
        foreach (var (id, entry) in _recordedUnits)
            if (!entry.Component.IsAttachedToVisualTree && !entry.Component.IsParked) _staleUnitIds.Add(id);
        foreach (var id in _staleUnitIds) _recordedUnits.Remove(id);

    }

    // APPLY half of the full walk (GPU): rebuild the paint-order groups from the recorded draws (create/update/free the
    // units per component via BuildUnitsFor/ProcessRenderCommands), then reclaim any control that dropped off the tree.
    private void ApplyFullWalk(RenderPacket packet)
    {
        ClearOrder();   // the walk re-derives the whole order; whoever it does not visit is simply not in it any more
        foreach (var draw in packet.Draws)
            ProcessRenderCommands(draw.Component, draw.Commands, packet.ProjectionMatrix, draw.WasGeometryValid, draw.Order, draw.Clones);
        ReconcileDetachedControls();
    }

    // Empties the paint order. Every group must learn it is out - a group whose InOrder stayed true would never be
    // re-inserted by a later splice (its "already there" would be a lie) and would silently stop drawing.
    private void ClearOrder()
    {
        foreach (var group in _groups)
        {
            group.InOrder = false;

            // Everyone leaves here, and whoever the walk does not put back has instances left in the arena with nobody
            // to speak for them - a segment is issued as a RANGE, so they are drawn along with their neighbours. The ones
            // that DO come back are skipped when the sweep runs (they are in the order again by then), so this costs a
            // flag check apiece. Leaving it out is what painted a departed tab's sliders across the tab strip after a
            // theme swap: the swap walks in full, and a full walk empties the order right here.
            _leftTheOrder.Add(group);
        }

        _groups.Clear();
    }

    // Snapshot a component's just-recorded draw commands (the shared drawing context is reused for the next component).
    // Empty -> a shared empty array. Allocates per NON-empty component per FULL walk (rare - structural changes only).
    private static IReadOnlyList<IDrawCommand> CopyCommands(IReadOnlyList<IDrawCommand> commands)
    {
        if (commands.Count == 0) return Array.Empty<IDrawCommand>();
        var copy = new IDrawCommand[commands.Count];
        for (var i = 0; i < commands.Count; i++) copy[i] = commands[i];
        return copy;
    }

    // Push a component's children so the stack pops them in PAINT order (drawn first = underneath). Fast path (the norm):
    // no explicit ZIndex -> document order (push reversed). Otherwise composite by ZIndex then document order - the same
    // precedence the hit-test's ZSort uses - so a raised child (e.g. a tab mid-drag) draws over its siblings.
    private static void PushChildrenInPaintOrder(Stack<(IUIComponent Node, bool Hidden)> stack, IReadOnlyCollection<IUIComponent> children, bool hidden)
    {
        var anyZ = false;
        foreach (var child in children)
            if (child.ZIndex != 0) { anyZ = true; break; }

        if (!anyZ)
        {
            // Reverse WITHOUT allocating (children.Reverse() buffers them all): VisualChildren is an IReadOnlyList, so walk
            // it back-to-front by index (this runs per component on every full walk).
            if (children is IReadOnlyList<IUIComponent> list)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                    stack.Push((list[i], hidden));
            }
            else
            {
                foreach (var child in children.Reverse())
                    stack.Push((child, hidden));
            }
            return;
        }

        foreach (var child in children
                     .Select((child, index) => (child, index))
                     .OrderByDescending(x => x.child.ZIndex)
                     .ThenByDescending(x => x.index)
                     .Select(x => x.child))
        {
            stack.Push((child, hidden));
        }
    }

    // Refresh a component's cached units from its freshly recorded draw commands: reuse a still-matching unit in place,
    // replace a type-changed one, create the extra, dispose the surplus. Mutates the component's GROUP in place but NOT the
    // paint order (_groups) - the caller places a NEW group; an existing group already sits at its spot.
    private ControlGroup BuildUnitsFor(IUIComponent component, IReadOnlyList<IDrawCommand> drawCommands, Matrix4x4F projectionMatrix)
    {
        if (!_groupById.TryGetValue(component.RenderId, out var group))
        {
            group = new ControlGroup { ControlId = component.RenderId, Component = component };
            _groupById[component.RenderId] = group;
        }

        var units = group.Units;
        for (int i = 0; i < drawCommands.Count; i++)
        {
            var command = drawCommands[i];
            command.RenderData.ProjectionMatrix = projectionMatrix;
            Core.Diagnostics.RuntimeStats.CommandsApplied++;
            if (i >= units.Count)
            {
                Core.Diagnostics.RuntimeStats.UnitsCreated++;
                units.Add(_renderUnitFactory.CreateRenderUnitFromCommand(command));
            }
            else
            {
                var unit = units[i];
                if (unit.Match(command))
                {
                    Core.Diagnostics.RuntimeStats.UnitsUpdated++;
                    unit.UpdateWithDrawCommand(command);
                }
                else
                {
                    Core.Diagnostics.RuntimeStats.UnitsCreated++;
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

    private void ProcessRenderCommands(IUIComponent component, IReadOnlyList<IDrawCommand> drawCommands, Matrix4x4F projectionMatrix, bool wasGeometryValid, long order, IReadOnlyList<Matrix4x4F> clones = null)
    {
        // A CLONE HOST takes its place in the paint order even when it draws nothing of its own (§4o): the clone run
        // starts at its group and covers the subtree that follows. A prototype that is a bare container - the visual
        // carried by its children - would otherwise never be seen by the draw walk, and its subtree would draw once.
        if (drawCommands.Count == 0 && clones is { Count: > 0 })
        {
            var hostGroup = BuildUnitsFor(component, drawCommands, projectionMatrix);
            hostGroup.Clones = clones;
            hostGroup.Order = order;
            _groups.Add(hostGroup);
            hostGroup.InOrder = true;
            return;
        }

        if (drawCommands.Count > 0)
        {
            var group = BuildUnitsFor(component, drawCommands, projectionMatrix);
            group.Clones = clones;
            group.Order = order;   // _groups stays sorted by rank - a later structural splice merges into it
            _groups.Add(group);
            group.InOrder = true;
        }
        else
        {
            // No commands this frame: was clean -> Render() didn't re-record, reuse the cached units; was dirty ->
            // re-rendered to nothing, clear its stale units so they stop drawing.
            if (_groupById.TryGetValue(component.RenderId, out var group))
            {
                if (wasGeometryValid)
                {
                    group.Order = order;
                    _groups.Add(group);
                    group.InOrder = true;
                }
                else
                {
                    RemoveAndDeferDispose(component.RenderId);
                }
            }
        }
    }

    /// <summary>Frees the cached units of any control no longer attached to the visual tree. Must run during the build
    /// (EndDraw): disposal is deferred and drained M frames later, so calling it earlier (from the detach event during
    /// Update) would dispose a unit still in flight. Attachment, not visibility, is the keep signal - hidden-but-attached
    /// controls retain their resources.
    /// <para>A PARKED control is the third case: out of the tree, but coming back. Freeing its units would throw away
    /// exactly what parking exists to keep - rebuilding them is the pause a kept-alive view is meant to avoid - so the
    /// keep signal is "attached OR parked".</para></summary>
    // Out of the tree and not parked - whatever else is true of it, it does not draw. Read off the GROUP's own component:
    // a group can hold no units at all (a control whose draws are all instanced), and units[0] would then say nothing.
    private static bool LeftTheTree(ControlGroup group)
    {
        var component = group.Component ?? (group.Units.Count > 0 ? group.Units[0].Component : null);
        return component != null && !component.IsAttachedToVisualTree && !component.IsParked;
    }

    private int ReconcileDetachedControls()
    {
        List<Guid> detached = null;
        foreach (var pair in _groupById)
        {
            if (!LeftTheTree(pair.Value)) continue;
            (detached ??= new List<Guid>()).Add(pair.Key);
        }

        // The PAINT ORDER is a separate list, and it is the one that draws. A group can sit in it while the dictionary no
        // longer names it, and the sweep above would then never reach it - so sweep the order for itself.
        var removed = 0;
        for (var i = _groups.Count - 1; i >= 0; i--)
        {
            if (!LeftTheTree(_groups[i])) continue;
            _groups[i].InOrder = false;
            _groups.RemoveAt(i);
            removed++;
        }

        if (detached != null)
        {
            removed += detached.Count;
            foreach (var id in detached)
                RemoveAndDeferDispose(id);
        }

        return removed;
    }

    /// <summary>Drops the cache entry and defer-disposes its units (deferred until the frame fence signals, as the GPU may
    /// still be using them). Build-phase only (EndDraw). Idempotent. Also drops the group from the paint order - a no-op
    /// miss on a full walk that just rebuilt _groups; on any other path it keeps order and cache in sync.</summary>
    private void RemoveAndDeferDispose(Guid renderId)
    {
        if (!_groupById.Remove(renderId, out var group)) return;


        foreach (var unit in group.Units)
            unit?.DeferDispose();
        RemoveFromOrder(group);
        // Return its transform slot to the pool. Every drawn element holds one now (ResolveBake stopped world-baking), so
        // without this a list that recycles rows would consume slots forever. Safe here: this runs in the build phase of a
        // walk that re-records the whole arena, so no still-drawn instance references the slot by the time it is reused.
        _transformTable?.ReleaseSlot(renderId);
    }
}

