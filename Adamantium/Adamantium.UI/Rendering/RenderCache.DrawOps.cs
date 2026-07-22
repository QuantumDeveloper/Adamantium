using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

public partial class RenderCache
{
    // Replay a recorded frame's op stream (a Clean frame): re-issue scissor changes, per-unit draws and batch segments in
    // order. No walk, no bake, no upload - the batch buffers still hold last frame's bytes, and each unit's RenderData its
    // baked transform (nothing moved on a Clean frame).
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
                    // A per-unit draw baked its full world into RenderData at record time (it doesn't read the transform
                    // slot), so a compositor-driven motion node (the theme-swap spinner's stroked arc) would replay frozen.
                    // Re-point it at its owner's freshly-composited world before drawing.
                    if (_compositedOwners.Count > 0 && op.Unit.Component is { } c && _compositedOwners.Contains(c))
                        op.Unit.Update(World(c), _projectionMatrix, _renderScale);
                    op.Unit.Render();
                    break;
                case RenderOpKind.Segment:
                    switch (op.Batch)
                    {
                        case 0: _rectBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                        case 1: _ellipseBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                        case 3: _gradientRectBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                        case 4: _gradientEllipseBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                        case 5: _patternBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                        default: _textBatch.DrawRecordedSegment(device, op.SegIndex, fullScissor, _projectionMatrix); break;
                    }
                    break;
                case RenderOpKind.InstancedFlush:
                    _instancedFill.ReplayFlush(op.SegIndex, fullScissor, _projectionMatrix);
                    break;
            }
        }
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
        RecordSegment(0, _rectBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(1, _ellipseBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(3, _gradientRectBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(4, _gradientEllipseBatch.Flush(device, fullScissor, _projectionMatrix));
        RecordSegment(5, _patternBatch.Flush(device, fullScissor, _projectionMatrix));   // pattern layer: after gradients, before instanced
        // The general instanced-fill flush is retained too: Flush records the group and returns its index, replayed via
        // ReplayFlush - so a vector icon no longer disables replay for the whole window.
        if (_instancedFill != null)
        {
            var fi = _instancedFill.Flush(fullScissor, _projectionMatrix);
            if (_recording && fi >= 0)
            {
                _ops.Add(new RenderOp { Kind = RenderOpKind.InstancedFlush, SegIndex = fi });
                _opsHaveInstancedFlush = true;
            }
        }

        RecordSegment(2, _textBatch.Flush(device, fullScissor, _projectionMatrix));
        scissorNarrowed = false;
        _batchOpen = false;
    }

    // Record a batch segment op (the immediate draw already happened in Flush; this only appends it for a clean-frame
    // replay). A Flush that drew nothing returns -1 and records nothing.
    private void RecordSegment(byte batch, int segIndex)
    {
        if (_recording && segIndex >= 0)
            _ops.Add(new RenderOp { Kind = RenderOpKind.Segment, Batch = batch, SegIndex = segIndex });
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
        stack.Push(visualRoot);
        while (stack.Count > 0)
        {
            var component = stack.Pop();

            if (component.Visibility != Visibility.Visible) continue;

            // A component must render exactly once per frame: if the tree makes one reachable twice (a content host
            // referenced from two places), it would join the paint order twice -> drawn TWICE (overdraw). Guard here.
            if (!visited.Add(component)) continue;

            _orderByControl[component.RenderId] = order;   // paint-order rank, SPARSE (see OrderGap)

            // Capture dirtiness BEFORE Render: a clean control's Render() is a no-op, so an empty command list means
            // "reuse the cached units"; a dirty one re-records, so an empty list then means "now draws nothing" -> clear
            // its stale units.
            var wasGeometryValid = component.IsGeometryValid;



            _drawingContextInternal.Clear();
            component.Render(_drawingContext);
            var commands = CopyCommands(_drawingContextInternal.GetDrawCommands());
            packet.Draws.Add(new ComponentDraw(component, commands, wasGeometryValid, order));
            MirrorUnits(component, commands.Count, wasGeometryValid);
            order += OrderGap;

            PushChildrenInPaintOrder(stack, component.VisualChildren);
        }

        // Mirror the applier's ReconcileDetachedControls: it frees the units of controls no longer in the tree, so the
        // recorder's own "who holds units" view must drop them too (this walk never visits them).
        _staleUnitIds.Clear();
        foreach (var (id, entry) in _recordedUnits)
            if (!entry.Component.IsAttachedToVisualTree) _staleUnitIds.Add(id);
        foreach (var id in _staleUnitIds) _recordedUnits.Remove(id);

    }

    // APPLY half of the full walk (GPU): rebuild the paint-order groups from the recorded draws (create/update/free the
    // units per component via BuildUnitsFor/ProcessRenderCommands), then reclaim any control that dropped off the tree.
    private void ApplyFullWalk(RenderPacket packet)
    {
        ClearOrder();   // the walk re-derives the whole order; whoever it does not visit is simply not in it any more
        foreach (var draw in packet.Draws)
            ProcessRenderCommands(draw.Component, draw.Commands, packet.ProjectionMatrix, draw.WasGeometryValid, draw.Order);
        ReconcileDetachedControls();
    }

    // Empties the paint order. Every group must learn it is out - a group whose InOrder stayed true would never be
    // re-inserted by a later splice (its "already there" would be a lie) and would silently stop drawing.
    private void ClearOrder()
    {
        foreach (var group in _groups) group.InOrder = false;
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
    private static void PushChildrenInPaintOrder(Stack<IUIComponent> stack, IReadOnlyCollection<IUIComponent> children)
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
    // replace a type-changed one, create the extra, dispose the surplus. Mutates the component's GROUP in place but NOT the
    // paint order (_groups) - the caller places a NEW group; an existing group already sits at its spot.
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

    private void ProcessRenderCommands(IUIComponent component, IReadOnlyList<IDrawCommand> drawCommands, Matrix4x4F projectionMatrix, bool wasGeometryValid, long order)
    {
        if (drawCommands.Count > 0)
        {
            var group = BuildUnitsFor(component, drawCommands, projectionMatrix);
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
    /// controls retain their resources.</summary>
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

    /// <summary>Drops the cache entry and defer-disposes its units (deferred until the frame fence signals, as the GPU may
    /// still be using them). Build-phase only (EndDraw). Idempotent. Also drops the group from the paint order - a no-op
    /// miss on a full walk that just rebuilt _groups; on any other path it keeps order and cache in sync.</summary>
    private void RemoveAndDeferDispose(Guid renderId)
    {
        if (!_groupById.Remove(renderId, out var group)) return;

        foreach (var unit in group.Units)
            unit?.DeferDispose();
        RemoveFromOrder(group);
    }
}
