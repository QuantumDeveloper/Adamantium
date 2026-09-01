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
        var opsBytes0 = System.GC.GetAllocatedBytesForCurrentThread();
        var executed = 0;
        // LAYER by layer, and inside a layer in the order it was recorded. The two are the same sequence - a layer owns a
        // contiguous range of the stream - and saying it this way is what makes the structure of a recorded frame legible:
        // the stream is a flat list, the layers are what it MEANS.
        foreach (var layer in _layers)
            for (var i = layer.OpFirst; i < layer.OpFirst + layer.OpCount; i++)
            {
                var op = _ops[i];
                executed++;
                var kind = (int)op.Kind;
                if (kind >= 0 && kind < 4) Core.Diagnostics.RuntimeStats.OpCountByKind[kind]++;
                var opBytes0 = System.GC.GetAllocatedBytesForCurrentThread();
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
                        RefreshOverlayFade(op.Unit);
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
                            case 13: _materialBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                            default: _textBatch.DrawRecordedSegment(device, op.SegId, fullScissor, _projectionMatrix); break;
                        }
                        break;
                    case RenderOpKind.InstancedFlush:
                        _instancedFill.ReplayFlush(op.SegId, fullScissor, _projectionMatrix);
                        break;
                }
                if (kind >= 0 && kind < 4)
                    Core.Diagnostics.RuntimeStats.OpBytesByKind[kind] += System.GC.GetAllocatedBytesForCurrentThread() - opBytes0;
            }

        Core.Diagnostics.RuntimeStats.ExecuteOpsBytes += System.GC.GetAllocatedBytesForCurrentThread() - opsBytes0;
        Core.Diagnostics.RuntimeStats.LastOpsExecuted = executed;
    }

    // A draw that baked its transform at RECORD time, on a frame where its element is somewhere else: re-point it. Three
    // kinds of mover qualify, and only those - the compositor, which moved it on this thread; a motion node whose matrix
    // this frame has just rewritten (RefreshMovedNodes); and an element inside a subtree whose slots this frame has just
    // rewritten (RefreshMovedComponents). All three mean the batched half and this half are being taken from one position
    // in one frame; re-pointing anything else is what tears a frame in two.
    /// <summary>Hand a PER-UNIT draw the alpha its slot carries right now.
    ///
    /// <para>These draws (a stroke, a fill fringe, a per-unit body) are re-issued by the CPU on every replayed frame -
    /// ExecuteOps calls Render() again - so they never needed to read the table from a shader: they only needed someone
    /// to tell them the current number. Nobody did, which is why a unit wearing one used to keep the whole opacity
    /// CHAIN in its baked colour and be re-baked by a slot-blind list whenever an ancestor faded. Now the chain comes
    /// from the table (one lookup, already composed) and multiplies the element's own alpha, so the same unit's
    /// INSTANCED fill can ride the slot like every other family.</para></summary>
    private void RefreshOverlayFade(IRenderUnit unit)
    {
        if (_transformTable == null || unit?.Component == null) return;
        unit.SetEffectiveOpacity(ApplySnap(unit.Component).SelfOpacity * _transformTable.AlphaAt(unit.FadeSlot));
    }

    /// <summary>The two things a deferred overlay needs before it draws: WHERE it is (if something moved) and HOW
    /// FADED it is. One hook, so the instanced collector's deferred strokes and the recorded per-unit ops cannot drift
    /// apart on what they were told.</summary>
    private void PrepareOverlayForDraw(IRenderUnit unit)
    {
        RepointIfItMoved(unit);
        RefreshOverlayFade(unit);
    }

    private void RepointIfItMoved(IRenderUnit unit)
    {
        if (_compositedOwners.Count == 0 && _movedNodeOwners.Count == 0 && _movedOwners.Count == 0) return;
        if (unit.Component is not { } c) return;
        if (!_compositedOwners.Contains(c) && !_movedOwners.Contains(c)
            && !(_movedNodeOwners.Count > 0 && _movedNodeOwners.Contains(NodeOf(c)))) return;

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
        // The band reads the ancestor chain from the OPACITY SLOT now, so what goes into its colour is the element's
        // OWN alpha only - the same split every batched fill makes.
        FadeBySlot(unit);
        if (!batch.TryAdd(band, shape, corners, kind, bakeWorld, unit.RenderData.Opacity, scissor, bounds,
                band.Color, slot, field, fieldRange, RoundedClipSlot(unit.Component, _frameScissor), unit.FadeSlot))
        {
            return false;
        }

        if (_recording) NoteHaloRun(unit, inner, batch.LastSlot, 1, living: true);

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
        FadeBySlot(unit);   // the chain comes from the slot now - the colour carries the element's own alpha only
        if (!batch.TryAdd(bands, inner, shape, corners, kind, bakeWorld,
                unit.RenderData.Opacity, scissor, haloBounds, slot, field, fieldRange,
                RoundedClipSlot(unit.Component, _frameScissor), unit.FadeSlot))
        {
            return false;
        }

        // Note where they landed, so a repaint can re-bake them in place instead of waiting for the next walk.
        if (_recording) NoteHaloRun(unit, inner, batch.LastFirst, batch.LastCount, living: false);

        if (inner) _haloOverOwner = unit.Component;
        _batchScissor = scissor;
        _batchOpen = true;
        return true;
    }

    // Remember which halo records this unit took. Four ranges per unit at most - a still band and a living one, each on
    // either side of the fill - and a unit that wears none is never in the map at all.
    private void NoteHaloRun(IRenderUnit unit, bool inner, int first, int count, bool living)
    {
        if (!_haloRunsByUnit.TryGetValue(unit, out var runs))
            runs = new HaloRuns { LivingUnder = -1, LivingOver = -1 };

        if (living)
        {
            if (inner) runs.LivingOver = first; else runs.LivingUnder = first;
        }
        else if (inner)
        {
            runs.OverFirst = first; runs.OverCount = count;
        }
        else
        {
            runs.UnderFirst = first; runs.UnderCount = count;
        }

        _haloRunsByUnit[unit] = runs;
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
                RecordOp(new RenderOp { Kind = RenderOpKind.InstancedFlush, SegId = fi, Clip = _batchClip, Order = _recordOrder });
            }
        }

        // BACKDROP MATERIALS go LAST of the fills, and that ordering is the whole feature: the material copies what is
        // behind it out of the frame, so everything meant to be behind it has to be in the frame already. Flushed before
        // the halo's inner band and before text for the same reason those come after fills at all - a pane of glass is
        // still a fill, and a label on top of it is content.
        if (_materialBatch != null)
        {
            // Captured from what the INSTANCES cover, grown so the blur has neighbours to average at the edges - without
            // the margin a material darkens along its border towards whatever the clamp returns.
            //
            // Their bounds, not the clip group's scissor. The copy is downscaled fourfold, so its resolution is the
            // material's detail budget: taken from a whole scrolled panel, a 300x92 pane was reading about 75x23 texels
            // and looked like fog rather than frosting. The scissor is the fallback for a segment with no bounds.
            //
            // The grown box is then CUT BACK to the clip group. The margin reaches outside what the material covers, and
            // outside a scrolling panel is whatever is drawn OVER it - a scrolled pane picked up the tab strip along its
            // top edge and the blur dragged that darkness inward as a dense band. Cut there, the sampler's clamp extends
            // the panel's own edge pixels instead, which is what a blur against a clip boundary should do.
            const int blurMargin = 24;
            var limit = _batchOpen ? _batchScissor : fullScissor;
            var box = _materialBatch.HasPending
                ? ToFramebufferScissor(_materialBatch.PendingBounds, fullScissor)
                : limit;
            _materialBatch.SetCaptureRect(Intersect(new Rect2D
            {
                Offset = new Offset2D { X = box.Offset.X - blurMargin, Y = box.Offset.Y - blurMargin },
                Extent = new Extent2D
                {
                    Width = box.Extent.Width + blurMargin * 2,
                    Height = box.Extent.Height + blurMargin * 2
                }
            }, limit));
            RecordSegment(13, _materialBatch.Flush(device, fullScissor, _projectionMatrix));
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
            Kind = RenderOpKind.Segment, Batch = batch, SegId = segId, Clip = _batchClip,
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

    /// <summary>Where this window sits on the DESKTOP, in physical pixels. What mica needs, and the one number a
    /// material reading the wallpaper cannot get from the frame: the frame knows where things are inside the window,
    /// and the wallpaper is placed outside it.</summary>
    private Rect WindowOnDesktop()
    {
        if (_lastVisualRoot == null) return default;

        // LivePosition, not Position: the bindable one is updated through the loop thread's queue, so during a drag it
        // trails the window by a frame or more - and a backdrop drawn from a stale position slides about instead of
        // standing still on the desktop.
        // FROZEN while the window is dragged, so a desktop-anchored backdrop rides along instead of chasing. During a
        // drag the correct answer does not exist: the frame would need the position the window will have when it is
        // SHOWN, which the compositor decides afterwards - measured, 7-24% of frames arrive 8px out of date (peaks past
        // 30), and that was the shaking.
        if (_lastVisualRoot is Controls.WindowBase { IsBeingMoved: true })
        {
            if (!_frozenWhileMoving) { _frozenPosition = _lastVisualRoot.LivePosition; _frozenWhileMoving = true; }
            return new Rect(_frozenPosition.X, _frozenPosition.Y,
                _lastVisualRoot.ClientWidth * _renderScale, _lastVisualRoot.ClientHeight * _renderScale);
        }

        _frozenWhileMoving = false;

        var origin = _lastVisualRoot.LivePosition;

        return new Rect(origin.X, origin.Y,
            _lastVisualRoot.ClientWidth * _renderScale, _lastVisualRoot.ClientHeight * _renderScale);
    }

    // The position the desktop-anchored backdrop holds for the duration of a drag, taken on the drag's first frame.
    private PixelPoint _frozenPosition;
    private bool _frozenWhileMoving;

    /// <summary>The overlap of two device-pixel rects, empty when they do not meet.</summary>
    private static Rect2D Intersect(Rect2D a, Rect2D b)
    {
        var left = Math.Max(a.Offset.X, b.Offset.X);
        var top = Math.Max(a.Offset.Y, b.Offset.Y);
        var right = Math.Min(a.Offset.X + (int)a.Extent.Width, b.Offset.X + (int)b.Extent.Width);
        var bottom = Math.Min(a.Offset.Y + (int)a.Extent.Height, b.Offset.Y + (int)b.Extent.Height);

        return new Rect2D
        {
            Offset = new Offset2D { X = left, Y = top },
            Extent = new Extent2D { Width = (uint)Math.Max(0, right - left), Height = (uint)Math.Max(0, bottom - top) }
        };
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

            _orderByControl[component] = order;   // paint-order rank, SPARSE (see OrderGap)

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
            // Same rule as the other two record paths: believe the command count only when this walk actually re-recorded
            // it (a clean component's Render is a no-op, and a hidden one's commands were dropped on purpose).
            if (!hidden && !wasGeometryValid) component.DrawsNothing = commands.Count == 0;
            order += OrderGap;

            PushChildrenInPaintOrder(stack, component.VisualChildren, hidden);
        }

        // Mirror the applier's ReconcileDetachedControls: it frees the units of controls no longer in the tree, so the
        // recorder's own "who holds units" view must drop them too (this walk never visits them). Parked controls are
        // kept for the same reason the applier keeps them - they are coming back.
        _staleUnitIds.Clear();
        foreach (var (component, entry) in _recordedUnits)
            if (!entry.Component.IsAttachedToVisualTree && !entry.Component.IsParked) _staleUnitIds.Add(component);
        foreach (var component in _staleUnitIds) _recordedUnits.Remove(component);

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
        var unitStart = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int i = 0; i < drawCommands.Count; i++)
        {
            var command = drawCommands[i];
            command.RenderData.ProjectionMatrix = projectionMatrix;
            Core.Diagnostics.RuntimeStats.CommandsApplied++;
            if (i >= units.Count)
            {
                Core.Diagnostics.RuntimeStats.UnitsCreated++;
                Core.Diagnostics.RuntimeStats.UnitsCreatedGrow++;
                var growStart = System.Diagnostics.Stopwatch.GetTimestamp();
                var madeUnit = _renderUnitFactory.CreateRenderUnitFromCommand(command);
                units.Add(madeUnit);
                var growMs = System.Diagnostics.Stopwatch.GetElapsedTime(growStart).TotalMilliseconds;
                Core.Diagnostics.RuntimeStats.UnitCreateMs += growMs;
                Core.Diagnostics.RuntimeStats.NoteUnitCreated(
                    madeUnit == null ? "null" : madeUnit.GetType().Name + "<" + component.GetType().Name + ">", growMs);
            }
            else
            {
                var unit = units[i];
                if (unit.Match(command))
                {
                    Core.Diagnostics.RuntimeStats.UnitsUpdated++;
                    var oneStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    unit.UpdateWithDrawCommand(command);
                    var oneMs = System.Diagnostics.Stopwatch.GetElapsedTime(oneStart).TotalMilliseconds;
                    Core.Diagnostics.RuntimeStats.UnitUpdateMs += oneMs;
                    if (oneMs > Core.Diagnostics.RuntimeStats.LastApplySlowestUnitMs)
                    {
                        Core.Diagnostics.RuntimeStats.LastApplySlowestUnitMs = oneMs;
                        // The OWNER as well as the unit kind: "a RectangleRenderUnit" is a shape, "on a ScrollViewer
                        // 1200x800" is an element somebody can go and look at. The size matters too - a re-tessellation
                        // that costs 36ms is not costing it for a 24px tile.
                        Core.Diagnostics.RuntimeStats.LastApplySlowestUnit =
                            $"{unit.GetType().Name}<{component.GetType().Name}>{component.RenderSize.Width:0}x{component.RenderSize.Height:0}";
                    }
                }
                else
                {
                    Core.Diagnostics.RuntimeStats.UnitsCreated++;
                    Core.Diagnostics.RuntimeStats.UnitsCreatedMismatch++;
                    var swapStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    unit.DeferDispose();
                    units[i] = _renderUnitFactory.CreateRenderUnitFromCommand(command);
                    Core.Diagnostics.RuntimeStats.UnitCreateMs += System.Diagnostics.Stopwatch.GetElapsedTime(swapStart).TotalMilliseconds;
                }
            }
        }

        // Everything above happens INSIDE a render unit; the build loop's own work (group lookup, order bookkeeping,
        // the empty-commands path) is what is left when this is subtracted from LastApplyBuildMs.
        Core.Diagnostics.RuntimeStats.LastApplyUnitMs += System.Diagnostics.Stopwatch.GetElapsedTime(unitStart).TotalMilliseconds;

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
        // A departed subtree takes no place in the paint order, whatever the packet still says about it. The packet
        // describes the tree as it was WALKED, and a view can leave after that - a content transition finishing inside
        // the animation tick removes the outgoing one - so its components arrive here already detached. Recorded, they
        // bake real instances that are issued with their segment's RANGE on every replayed frame: the outgoing view's
        // scrollbar painting over the incoming one, at the coordinates it had. Evicting them afterwards cannot win,
        // because the next walk puts them straight back.
        if (LeftTheTree(component)) return;

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
        => LeftTheTree(group.Component ?? (group.Units.Count > 0 ? group.Units[0].Component : null));

    /// <summary>Has this control left the visual tree for good? Parked visuals have left it ON PURPOSE and are kept, so
    /// they are not departures. THE statement of it - the paint order refuses a departed subtree on the way in and
    /// evicts one that leaves while it is there, and both have to mean the same thing.</summary>
    private static bool LeftTheTree(IUIComponent component)
    {
        if (component == null) return false;

        // A part the template teardown DESTROYED has left, whatever the chain below says. It still carries a RenderParent
        // that reaches a live ancestor - the teardown does not unpick those links - so the walk kept answering "still
        // here" and this cache went on holding its group, its units and its draw commands. Nothing else in the process
        // knows the part is dead; the mark does.
        if (component is Core.FundamentalUIComponent { IsDiscarded: true }) return true;

        // An ADORNER is never in the visual tree - that is its design, not its departure. It draws in its target's
        // space, so what decides whether it still has anywhere to draw is the target: a focus ring outlives everything
        // except the control it rings. The chain, not one step of it - a ring built from the adorner's own TEMPLATE
        // reaches the adorned control only through the adorner.
        for (var c = component; c != null; c = c.RenderParent)
        {
            if (c.IsAttachedToVisualTree || c.IsParked) return false;
        }

        return true;
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
            var group = _groups[i];
            if (!LeftTheTree(group)) continue;
            // Through RemoveFromOrder, not by hand: leaving the order is not just a flag and a list entry, it is also the
            // one moment the sweep can be told that this group's instances are now nobody's. Dropped here, they keep
            // being issued with the range they sit in - a scrollbar the window outgrew, still painting at the size it had.
            RemoveFromOrder(group);
            removed++;
        }

        if (detached != null)
        {
            removed += detached.Count;
            foreach (var id in detached)
                RemoveAndDeferDispose(id);
        }

        // ...and the LAYOUT SNAPSHOTS, which this sweep never looked at. They are dropped on the two DEPARTURE loops in
        // the applier (packet.Removed / packet.Undrawn), and a template part destroyed by a re-template appears in
        // neither: it leaves the tree through the teardown, not through anything the record pass names. The map then
        // keeps its key - the control itself - for the life of the window. Measured on a theme swap: 39 destroyed
        // controls held here per swap, dead linear, while every other count in this cache stayed flat and so looked
        // innocent. The size of a map says nothing about what its entries point at.
        // DISCARDED, not LeftTheTree: a destroyed part still carries a RenderParent chain that reaches a live ancestor,
        // so the departure test says it is still here. The teardown's own mark is the one thing that cannot be wrong.
        List<IUIComponent> stale = null;
        foreach (var component in _applySnap.Keys)
        {
            if (component is Core.FundamentalUIComponent { IsDiscarded: true } || LeftTheTree(component))
                (stale ??= new List<IUIComponent>()).Add(component);
        }

        Core.Diagnostics.RuntimeStats.SnapSweeps++;   // TEMP: did this sweep run at all, and on which cache instance
        if (stale != null)
        {
            foreach (var component in stale) _applySnap.Remove(component);
            removed += stale.Count;
            Core.Diagnostics.RuntimeStats.SnapSwept += stale.Count;
        }

        // ...and the per-node walk memo, which nothing has ever removed from either. It is small by design - motion
        // nodes are counted in ones per window - but it is keyed by the NODE, so a departed one stays, and through it
        // its whole subtree. Found by walking the object graph from the strong handles: RenderCache ->
        // Dictionary<IUIComponent, int> -> a TabStripScroller's grid -> a whole discarded view.
        List<IUIComponent> staleNodes = null;
        foreach (var node in _nodeRefreshed.Keys)
        {
            if (LeftTheTree(node)) (staleNodes ??= new List<IUIComponent>()).Add(node);
        }

        if (staleNodes != null)
        {
            foreach (var node in staleNodes) _nodeRefreshed.Remove(node);
            removed += staleNodes.Count;
        }

        return removed;
    }

    /// <summary>Drops the cache entry and defer-disposes its units (deferred until the frame fence signals, as the GPU may
    /// still be using them). Build-phase only (EndDraw). Idempotent. Also drops the group from the paint order - a no-op
    /// miss on a full walk that just rebuilt _groups; on any other path it keeps order and cache in sync.</summary>
    private void RemoveAndDeferDispose(Guid renderId)
    {
        if (!_groupById.Remove(renderId, out var group)) return;

        // The withdrawal has to be UNCONDITIONAL here, and RemoveFromOrder below only speaks for a group that was still
        // in the order. A group can be dropped having already left it - the paint order now refuses departed subtrees on
        // the way in, so a view removed mid-frame never gets back into _groups and its disposal is the LAST moment
        // anyone can name its slots. Miss it and its instances keep being issued with their segment's range forever.
        if (group.Tag != 0 && !_leftTheOrder.Contains(group)) _leftTheOrder.Add(group);

        foreach (var unit in group.Units)
            unit?.DeferDispose();
        RemoveFromOrder(group);

        // ...and the TAG map, which nothing has ever removed from. _groupByTag exists so an arena slot can name its owner
        // however far its bytes have been copied, and it is written once per group and left. Every other map here is
        // swept - _groupById by name just above, the paint order by RemoveFromOrder - so the cache LOOKED clean while
        // this one held every group it had ever tagged, and through the group its control and all its units.
        //
        // Found by walking the object graph from the strong handles rather than by guessing: the path to a retained
        // Border ran MainWindow -> ForwardWindowRenderer -> RenderCache -> Dictionary<int, ControlGroup> -> the Border.
        // Two mentions in the whole codebase, the declaration and one write.
        //
        // AFTER _leftTheOrder has been told (above): that list is what blanks the instances still being issued, and it
        // reads the tag to do it.
        if (group.Tag != 0) _groupByTag.Remove(group.Tag);
        // Return its transform slot to the pool. Every drawn element holds one now (ResolveBake stopped world-baking), so
        // without this a list that recycles rows would consume slots forever. Safe here: this runs in the build phase of a
        // walk that re-records the whole arena, so no still-drawn instance references the slot by the time it is reused.
        _transformTable?.ReleaseSlot(renderId);
    }
}

