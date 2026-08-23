using System.Collections.Generic;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Rendering.Payloads;

namespace Adamantium.UI.Rendering;

public partial class RenderCache
{
    private readonly System.Text.StringBuilder _glyphWarmBuf = new();
    private readonly HashSet<Adamantium.Graphics.Fonts.FontAtlas> _warmAtlases = new();  // WarmTextAtlases: one batched glyph rasterization per atlas

    private readonly List<ControlGroup> _pendingInserts = new();   // ApplyStructural: groups to place, merged into the order once
    private readonly HashSet<ControlGroup> _pendingSet = new();
    private readonly List<ControlGroup> _mergedGroups = new();

    /// <summary>APPLY half of the frame build (GPU / render thread): consumes the recorder's packets - Clean re-draws the
    /// retained units, Partial updates dirty groups in place, Structural splices count changes, Full rebuilds the
    /// paint-order groups. Freezes the layout snapshot the draw pass replays. (RenderDirty is cleared ONCE per frame after
    /// every window has recorded - see below - not here.)</summary>
    public void ApplyFrame()
    {
        BeginApplyFrame();
        AdoptReadyGlyphs();   // glyphs that finished rasterizing since the last frame land here
        while (_published.TryDequeue(out var packet))
        {
            ApplyPacket(packet);
            packet.Reset(RenderBuildKind.Clean);
            _spare.Add(packet);   // back to the pool for the recorder
        }

        // RenderDirty (a GLOBAL set shared by every window) is NOT cleared per-window here: with two windows the first to
        // apply would wipe the set before the second records, so the second never re-records its content. Both the
        // single-threaded and the decoupled path now clear ONCE after ALL windows have recorded (single-threaded in
        // UIApplication.DispatchRenderFrame after ExecuteDrawSequence; decoupled in RecordRenderFrame).
    }

    /// <summary>Resets the per-DRAW merged apply state - the build kind, dirty set and moved nodes accumulate across every
    /// packet applied for this draw, so they must start empty.</summary>
    private void BeginApplyFrame()
    {
        LastBuildKind = RenderBuildKind.Clean;
        LastBuildTransformDirty = false;
        _partialDirty.Clear();
        _movedNodesBuf.Clear();
        _movedOwnersBuf.Clear();
        _partialSpliced = false;
    }

    // Warm every text block's atlas in ONE batch per atlas. Glyph rasterization is parallel MSDF work, but a text unit
    // built one at a time can only hand it ITS block's characters, so a cold fill rasterized ~50 blocks' glyphs serially
    // (1.1 s of a 1.9 s 4K fill). Pooling the whole packet's characters lets the generator spread them across cores.
    // Still lazy - only glyphs the UI actually shows are rasterized.
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
        // ASKED for, not waited on: the batch goes to a worker and this frame goes out with whatever the atlas
        // already holds. Pooling the packet's characters still matters - the generator parallelises across the glyphs it is
        // handed, so one batch keeps every core busy where fifty single-glyph requests would not.
        foreach (var atlas in _warmAtlases) atlas.RequestAsync(text);
    }

    /// <summary>Take in the glyphs the workers finished - device work, so it belongs on this side - and rebuild the text
    /// blocks that were built before their letters arrived. Nothing else about the frame changes, so this costs a walk of
    /// the text units and the quad rebuild of the few that were waiting.</summary>
    /// <summary>The glyph-arrival version this cache has already taken account of. Per CACHE, because the arrival is
    /// global and the adoption is not.</summary>
    private int _seenGlyphVersion;

    private void AdoptReadyGlyphs()
    {
        if (_renderUnitFactory.GraphicsDevice == null) return;

        // Pumping the atlas and ADOPTING into this cache are two different things, and tying them together was the bug.
        // PumpReadyGlyphs drains a QUEUE: whoever asks first takes the batch and returns true, everyone after it gets
        // false. There is more than one cache - window content, the adorner stage, the popup stage - and each asks in its
        // own Apply, so the first one adopted the new letters for ITS groups and the popup's cache bailed out here and
        // left its text with no recorded run. A SlidePanel opened with a blank close cross the first time and a correct
        // one the second, once the atlas was warm: `by=TextRenderUnit<TextBlock> noRecordedRun`.
        //
        // So the gate is kept - walking every unit on every frame would declare the stream stale constantly and cost the
        // patch its frame - but it is asked PER CACHE: pump for the side effect, then compare a version this cache
        // remembers. Whoever wins the race to the queue, everyone sees that something landed exactly once.
        Adamantium.Graphics.Fonts.FontAtlasStore.PumpReadyGlyphs();

        var landedVersion = Adamantium.Graphics.Fonts.FontAtlasStore.LandedVersion;
        if (landedVersion == _seenGlyphVersion) return;
        _seenGlyphVersion = landedVersion;

        var arrived = false;
        foreach (var group in _groups)
        foreach (var unit in group.Units)
        {
            if (unit is RenderUnits.TextRenderUnit text) arrived |= text.RefreshGlyphsIfArrived();
        }

        // Letters that land are a change to what the frame DRAWS, and this one is announced by nobody: the marks are the
        // loop thread's and this runs on the render thread, so a mark made here can be cleared before the recorder ever
        // sees it. Say it where it cannot be lost - the retained stream describes a run that is no longer the run - and
        // ask for the frame that re-records it. Until this, a block whose letters arrived late stayed BLANK until some
        // unrelated event (a mouse move) happened to force a walk.
        if (!arrived) return;

        StreamStaleBecause("glyphsArrived");
        Core.LoopSignal.Request();
    }

    // What the recorded op stream actually baked out of a snapshot: where the element is, how big it is, what clips it.
    // Opacity is NOT among them - it is re-composed per unit on every patch - so a fade must not cost a re-record.
    private static bool SameGeometry(LayoutSnapshot a, LayoutSnapshot b) =>
        a.LocalTransform == b.LocalTransform
        && a.RenderSize == b.RenderSize
        && a.ClipToBounds == b.ClipToBounds
        && a.IsMotionNode == b.IsMotionNode
        && ReferenceEquals(a.RenderParent, b.RenderParent);

    // ...and what it can survive one entry CHANGING, which is the whole difference between a drag that patches and a drag
    // that re-records the window every frame.
    //
    // Its PLACE, always: where an element sits lives in its transform-table slot, and the draw writes that slot
    // (RefreshMovedNodes for a motion node, RefreshMovedComponents for anything else).
    //
    // Its SIZE, but only while the element is being RE-BAKED on this same frame AND nothing under it clips. A size is not
    // in the matrix, it is in the drawn payload - so unless the patch is already rewriting that payload there is nothing
    // to carry it. Arrange marks a resized element geometry-invalid without exception (MeasurableUIComponent: "A size
    // change must re-run OnRender"), so that half is the ordinary case, not a lucky one. The clip half is the harder
    // one: a viewport that changes SHAPE changes which units fall outside it, and a unit culled at record time has no op
    // in the stream to correct.
    //
    // Nothing else. It started clipping, became a motion node, changed parent - each of those changes what the stream
    // baked in a way no slot write reaches.
    private bool StreamSurvives(IUIComponent c, LayoutSnapshot was, LayoutSnapshot now) =>
        _forgivenMoves.Contains(c)
        && was.ClipToBounds == now.ClipToBounds
        && was.IsMotionNode == now.IsMotionNode
        && ReferenceEquals(was.RenderParent, now.RenderParent)
        && (was.RenderSize == now.RenderSize || (_rebakedThisPacket.Contains(c) && _forgivenResize.Contains(c)));

    // The components this packet's patch will re-bake - packet.PartialDirty, as a set (StreamSurvives asks per entry).
    private readonly HashSet<IUIComponent> _rebakedThisPacket = new();

    /// <summary>Index of the first group ranked AFTER <paramref name="order"/>, by bisection - `_groups` is kept sorted by
    /// paint rank, so nothing needs to be scanned to find a place in it.</summary>
    private int FirstGroupAfter(long order)
    {
        int low = 0, high = _groups.Count;
        while (low < high)
        {
            var mid = (low + high) >> 1;
            if (_groups[mid].Order > order) high = mid;
            else low = mid + 1;
        }

        return low;
    }

    // TEMP: name what staled the stream, so why=4 says which of its causes it was.
    private void StreamStaleBecause(string reason)
    {
        _layoutChangedSinceRecord = true;
        if (Core.Diagnostics.FrameTrace.Enabled) Core.Diagnostics.FrameTrace.LayoutChangedBy = reason;
    }

    // Realize ONE packet. The per-frame results the draw pass reads are MERGED across the packets drained this frame: a
    // Full supersedes everything before it; two Partials union their dirty sets.
    private void ApplyPacket(RenderPacket packet)
    {
        WarmTextAtlases(packet);   // one batched glyph rasterization for the whole packet, before any unit is built

        _projectionMatrix = packet.ProjectionMatrix;

        // The applier's OWN derived memos (world/clip/node transforms), dropped here not by the recorder: they are
        // applier-resident, and the recorder only says WHEN they went stale.
        if (packet.ClearMemos)
        {
            _worldCache.Clear();
            _clipCache.Clear();
            _relWorldCache.Clear();
            _nodeCache.Clear();
        }

        // Fold this packet's layout delta into the applier's snapshot replica - the only thing the draw pass reads for a
        // component's transform/size/clip. A full walk resets it and carries the whole scene.
        if (packet.SnapReset) _applySnap.Clear();

        // A packet that changes the LAYOUT invalidates the retained op stream, whatever kind it calls itself. The stream
        // bakes the layout of the frame that recorded it into its scissors and its per-unit worlds; folding a new snapshot
        // in without re-recording leaves the two describing different frames, and a replay then draws that mixture - old
        // clips and old per-unit positions under an already-updated snapshot. It is invisible to a write probe (nothing is
        // written incorrectly) and to the validation layer (every command is legal); the frame is simply built from two
        // moments at once. Measured: dozens of Clean packets per second of scrolling arrive carrying snapshot deltas, and
        // the flicker disappears exactly when replay is refused.
        // A packet that MOVES things leaves the recorded stream describing the previous positions. The per-unit draws are
        // re-pointed at replay (see ExecuteOps), but a recorded SCISSOR is a world-space rect baked at record time and
        // nothing re-derives it - so a move still has to force a rebuild. A packet that changes nothing about layout
        // (a recolour) leaves the stream perfectly valid and keeps its replay.
        // ...but a snapshot ENTRY is not a layout change: it is re-published whenever a component re-renders, and a hover
        // re-publishes an entry whose transform, size and clip are word for word the ones the stream already baked. So
        // compare what the stream actually baked, instead of taking the entry's presence as proof of movement.
        // ...and a moved MOTION NODE is not a layout change either, for the same reason a composited move isn't: the
        // batches read the node's slot matrix live, and the draw re-points what rides it (RefreshMovedNodes, which also
        // proves every drawn unit under the node is node-aware).
        //
        // A recorded SCISSOR used to be the exception - a world-space rect nothing re-derived - so a node whose subtree
        // clipped forced a rebuild. It is derived again now (RefreshMovedScissors), so the clip no longer decides. That
        // matters most where it looked least important: a TAB TRANSITION slides a whole view rigidly, and every one of
        // those frames re-recorded the window because there was a scroll viewer somewhere inside it.
        //
        // A RESIZE is different and still refuses when anything under it clips: a clip that changes SHAPE changes which
        // units fall outside it, and a unit culled at record time has no op in the stream at all. Deriving the rect again
        // cannot conjure a draw that was never recorded.
        _forgivenMoves.Clear();
        _forgivenResize.Clear();
        foreach (var node in packet.MovedNodes)
        {
            _forgivenMoves.Add(node);
            if (!SubtreeClips(node)) _forgivenResize.Add(node);
        }

        // An ORDINARY mover is forgiven on the same terms, and it is the same fact about the frame: where the element
        // sits lives in its transform-table slot, so a move is a slot write, not a re-record. The difference is only in
        // HOW MANY slots - a node moves its whole subtree by one matrix, an ordinary mover has to have its subtree's
        // written one by one (RefreshMovedComponents).
        //
        // This was tried once WITHOUT that write and it was wrong: forgiving the move while nothing carried it left the
        // drag-and-drop gap shut until a walk arrived, and then everything jumped at once. That is not an argument
        // against forgiving a move; it is what forgiving one without doing its work looks like.
        //
        // An unnameable mover (a bare Transform with no owner) forgives nothing: then Moved is not the whole story and
        // there is no subtree to carry.
        _rebakedThisPacket.Clear();
        foreach (var dirty in packet.PartialDirty) _rebakedThisPacket.Add(dirty);

        var moversCarried = !packet.TransformUnknown;
        if (!moversCarried) Core.Diagnostics.FrameTrace.NoteNotCarried("transformUnknown");   // TEMP
        foreach (var mover in packet.Moved)
        {
            if (SubtreeClips(mover))
            {
                StreamStaleBecause($"movedClips<{mover.GetType().Name}>");
                if (moversCarried) Core.Diagnostics.FrameTrace.NoteNotCarried($"clips<{mover.GetType().Name}>");   // TEMP
                moversCarried = false;
            }
            else { _forgivenMoves.Add(mover); _forgivenResize.Add(mover); }
        }

        foreach (var entry in packet.SnapDelta)
        {
            var known = _applySnap.TryGetValue(entry.Key, out var previous);
            if (!known || !SameGeometry(previous, entry.Value))
            {
                if (!known || !StreamSurvives(entry.Key, previous, entry.Value))
                    StreamStaleBecause(known ? $"moved<{entry.Key.GetType().Name}>" : $"new<{entry.Key.GetType().Name}>");
            }
            _applySnap[entry.Key] = entry.Value;
        }

        // Something left the tree since the last build. Withdrawing what it drew is the reconcile's job, and it used to
        // ride on a FULL walk - which the redesign made rare, so a detached view kept its place in the order and the
        // retained op stream went on re-issuing it, frozen at the size it had when it left.
        if (_reconciledDetachGen != Dirty.DetachGeneration && packet.Kind != RenderBuildKind.Full)
        {
            _reconciledDetachGen = Dirty.DetachGeneration;
            if (ReconcileDetachedControls() > 0)
            {
                // Those units are gone, so the op stream and the recorded slots no longer describe the scene: the draw
                // pass must re-walk instead of replaying, exactly as after a splice.
                if (LastBuildKind != RenderBuildKind.Full) LastBuildKind = RenderBuildKind.Structural;
                _partialDirty.Clear();
                _partialSpliced = false;
            }
        }

        switch (packet.Kind)
        {
            case RenderBuildKind.Clean:
                // Nothing to realize - but a node that MOVED still has to have its matrix written before the replay, or
                // the frame draws the subtree where it was last recorded.
                _movedNodesBuf.AddRange(packet.MovedNodes);
                break;

            case RenderBuildKind.Partial:
            {
                // APPLY pass (GPU): realize the recorded draws - update the units in place / splice a count change.
                foreach (var draw in packet.Draws)
                    ApplyReRender(draw.Component, draw.Commands, draw.Order, draw.Clones);

                if (LastBuildKind != RenderBuildKind.Full) LastBuildKind = RenderBuildKind.Partial;
                _partialDirty.AddRange(packet.PartialDirty);
                _movedNodesBuf.AddRange(packet.MovedNodes);

                // A move only forbids the patch when nobody is going to carry it. When every mover is named and clip-free
                // the draw writes their subtrees' slots instead, so the flag - which is frame-wide, and therefore speaks
                // for 22k nodes when one thumb moved - stays down.
                LastBuildTransformDirty |= packet.IsTransformDirty && !moversCarried;
                if (moversCarried) _movedOwnersBuf.AddRange(packet.Moved);
                break;
            }

            case RenderBuildKind.Structural:
            {
                // A control that starts drawing is a count change from nothing, which is exactly what the splice repairs:
                // it is given its own segment, placed by its own paint rank, and no recorded op moves. So an ARRIVAL does
                // not have to cost the frame a walk of the window - a hover affordance, a scroll chevron, an edge fade.
                // A DEPARTURE does, and stays on the old path: what left has no group left to name, so the splice cannot
                // reach the ops still drawing it, and a patched frame would keep painting a control that is gone. That is
                // the phantom the removal tests pin - AControlThatStoppedDrawing_IsGoneFromAPlainREPLAY and its family.
                // Ranks must also be untouched: a RENUMBER moves everyone, which is not a local change by any reading.
                var local = packet.Removed.Count == 0 && packet.Undrawn.Count == 0
                            && packet.Reranks.Count == 0 && !packet.SnapReset && !_layoutChangedSinceRecord;

                ApplyStructural(packet);
                if (LastBuildKind != RenderBuildKind.Full)
                    LastBuildKind = local ? RenderBuildKind.Partial : RenderBuildKind.Structural;
                LastBuildTransformDirty = !local;
                _partialDirty.Clear();
                if (local)
                {
                    foreach (var draw in packet.Draws) _partialDirty.Add(draw.Component);
                    _partialSpliced = true;
                }
                else
                {
                    _partialSpliced = false;
                }

                _movedNodesBuf.AddRange(packet.MovedNodes);
                break;
            }

            case RenderBuildKind.Full:
                ApplyFullWalk(packet);   // GPU: rebuild the paint-order groups from the packet (reconciles as it goes)
                _reconciledDetachGen = Dirty.DetachGeneration;
                _built = true;
                // A full walk re-records the whole scene, so earlier packets' dirty entries are covered - and their unit
                // sets are gone (groups rebuilt), which would mis-patch the batch. Drop them.
                LastBuildKind = RenderBuildKind.Full;
                LastBuildTransformDirty = true;
                _partialDirty.Clear();
                _movedNodesBuf.Clear();
                _movedOwnersBuf.Clear();
                _partialSpliced = false;
                break;
        }
    }

    // The record+apply decision for one dirty component: Skip = reuse its cached units as-is (nothing recorded);
    // Fallback = the caller must do a full walk; Recorded = its commands were captured into the packet for the applier;
    // Undrawn = it is HIDDEN and keeps its place in the paint order, so it records ZERO commands (see RecordReRender).
    private enum PartialRecord { Skip, Fallback, Recorded, Undrawn }

    // The DECISION for one geometry-dirty component - PURE: it reads state and renders nothing, so the structural pass can
    // pre-validate the frame before it commits to anything (see RecordStructuralFrame).
    private PartialRecord ClassifyReRender(IUIComponent component)
    {
        // COLLAPSED - out of the layout as well as out of the frame: draws NOTHING and nothing to record. Its units are
        // retained and its dirty flag stays set until it is shown again, so it re-records at the right time (the
        // structural splice that puts it back), not now. A full walk here meant a whole-tree re-record for every
        // collapsed container that so much as re-bound.
        if (component.Visibility == Visibility.Collapsed) return PartialRecord.Skip;

        // Not in the live paint tree: DETACHED, or hidden by a COLLAPSED ancestor. The full walk never reaches it, so it
        // has no rank and draws nothing - yet it used to force a full rebuild EVERY dirty frame. Skip: it holds no units
        // (a real detach/collapse is STRUCTURAL and already removed them).
        if (!component.IsAttachedToVisualTree) return PartialRecord.Skip;

        var hidden = component.Visibility == Visibility.Hidden;
        for (var a = component.VisualParent; a != null; a = a.VisualParent)
        {
            if (a.Visibility == Visibility.Collapsed) return PartialRecord.Skip;
            if (a.Visibility == Visibility.Hidden) hidden = true;
        }

        // HIDDEN (itself, or under something hidden): it holds its slot and its rank and simply paints nothing. Saying so
        // - recording zero commands - empties its group in place, which is a count change the retained frame patches.
        // Without a rank there is nothing to patch INTO (a full walk while it was hidden never gave it one), and the
        // structural path has to put it back.
        if (hidden) return HasRank(component) ? PartialRecord.Undrawn : PartialRecord.Skip;

        // A component from a FOREIGN tree (a popup, a menu, a tooltip - drawn by that stage's OWN cache) does not reach
        // here at all any more: marks go to the scope of the surface that draws them, and this cache reads only its own
        // (see RenderDirtyRouter). It used to arrive, and had to be recognised and stepped over WITHOUT rendering,
        // because rendering it would consume the IsGeometryValid the popup stage's gate polls - the main cache eating it
        // starved the gate and a menu hover never redrew. That was a symptom of one set with no owner, not a rule.

        // Marked dirty EXTERNALLY (an animation heartbeat) while its own geometry is still VALID: Render() would no-op and
        // record ZERO commands, read as "now draws nothing" -> the units get DELETED (the mass tile vanish on ease-back).
        // Its recorded geometry is unchanged - keep the units as-is.
        if (component.IsGeometryValid) return PartialRecord.Skip;

        // No paint rank: invisible/absent when the order was last derived, now appearing with no structural mark to place
        // it (an auto-hide ScrollBar fading in). Hand to a full walk. A component that DRAWS always has a rank, so this is
        // the appearing-content case only.
        if (!HasRank(component)) return PartialRecord.Fallback;

        return PartialRecord.Recorded;
    }

    // RECORD half of a partial re-render for ONE component (DEVICE-FREE): decide, then component.Render and copy the
    // commands into the packet. No GPU - the applier (ApplyReRender) realizes them.
    private PartialRecord RecordReRender(IUIComponent component, RenderPacket packet)
    {
        var decision = ClassifyReRender(component);
        if (decision is PartialRecord.Skip or PartialRecord.Fallback) return decision;

        var rank = RankOf(component);
        _drawingContextInternal.Clear();
        component.Render(_drawingContext);   // NB: consumes the dirty flag (Render sets IsGeometryValid back to true)
        // A HIDDEN element is rendered and its commands DROPPED, rather than not rendered at all: rendering is what
        // consumes the dirty flag, and an element that stays dirty is re-recorded every frame forever. What it says it
        // would draw is simply not what it draws while hidden.
        var commands = decision == PartialRecord.Undrawn
            ? CopyCommands(System.Array.Empty<IDrawCommand>())
            : CopyCommands(_drawingContextInternal.GetDrawCommands());
        packet.Draws.Add(new ComponentDraw(component, commands, false, rank, component.RenderClones));
        MirrorUnits(component, commands.Count, false);   // it WAS dirty: no commands now means "draws nothing" -> units freed
        return PartialRecord.Recorded;
    }

    // APPLY half (GPU): realize ONE recorded partial draw - update the group's units in place (same count+type) or splice
    // in the count/type change. `order` (the paint rank) rides WITH the draw, so a group appearing for the first time is
    // placed without the applier ever reading the recorder's rank map.
    private void ApplyReRender(IUIComponent component, IReadOnlyList<IDrawCommand> drawCommands, long order,
        IReadOnlyList<Adamantium.Mathematics.Matrix4x4F> clones = null)
    {
        _groupById.TryGetValue(component.RenderId, out var group);

        // The clone set travels with the contribution on EVERY path, this one included: a partial re-render that left it
        // untouched went on drawing the previous frame's set (caught by DroppingTheClones_ReturnsToASingleDraw).
        if (group != null) group.Clones = clones;
        var oldCount = group?.Units.Count ?? 0;

        // Fast path: same command count and every unit still matches -> update in place; nothing structural changed. Gate
        // on InOrder: a group that fell OUT of the paint order (its container was hidden/parked, then rebound and re-drawn
        // here) MUST be re-inserted, not just patched in place - so let it fall through to the re-insert below.
        if (group is { InOrder: true } && drawCommands.Count == oldCount && oldCount > 0)
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
                group.Order = order;
                return;
            }
        }

        // Count/type changed. The change stays LOCAL to this control's group (BuildUnitsFor refreshes its Units in place,
        // no other group moves). The recorded op stream + rect-slot map still reference the old unit set, so the draw
        // phase re-walks this frame (per-group op patching is the planned follow-up).
        _partialSpliced = true;
        // (Re)insert into the paint order when the group is NEW *or* exists but has fallen OUT of the order - the same
        // check ApplyStructural makes. Without the InOrder half, a container that was hidden (its group left the order,
        // units kept) then rebound and re-recorded here got its units rebuilt but was never put back in _groups, so it
        // drew NOWHERE: the "dead" selection/hover highlight on a scrolled-then-returned list row (recycled container).
        var needsInsert = group is not { InOrder: true };

        group = BuildUnitsFor(component, drawCommands, _projectionMatrix);
        group.Order = order;

        if (needsInsert)
        {
            // Insert by paint rank, before the first group that ranks after it. Existing groups never move; the rank came
            // WITH the draw. Found by BISECTION, not by a scan: the list is kept sorted by rank, and a scan from the front
            // is O(scene) for every control that starts drawing - which, now that an arrival is patched instead of walked,
            // happens on the cheap path where a scene-sized loop has no business being.
            _groups.Insert(FirstGroupAfter(order), group);
            group.InOrder = true;
        }
    }

    // APPLY half of a STRUCTURAL frame (GPU): free what left, realize what arrived, and re-sort the paint order - all
    // O(changed) plus one linear merge, instead of rebuilding every group from a full walk.
    private void ApplyStructural(RenderPacket packet)
    {
        // 1. DETACHED: gone for good - free its units.
        foreach (var component in packet.Removed)
        {
            RemoveAndDeferDispose(component.RenderId);
            _applySnap.Remove(component);
        }

        // 2. HIDDEN: it stops DRAWING, and that is all. Its group + units survive, so a re-show (a recycled container a
        //    few rows later) re-inserts a ready group instead of rebuilding buffers.
        foreach (var component in packet.Undrawn)
        {
            if (_groupById.TryGetValue(component.RenderId, out var hidden)) RemoveFromOrder(hidden);
            _applySnap.Remove(component);
        }

        // 3. What ARRIVED (or re-recorded): build/refresh its units. Groups to place are collected for ONE merge below (a
        //    linear scan per insert would be O(new x scene) on a fill).
        _pendingInserts.Clear();
        _pendingSet.Clear();
        foreach (var draw in packet.Draws)
        {
            if (draw.Commands.Count == 0)
            {
                // Recorded nothing. Clean -> draws what it already drew (a panel with no background); dirty -> now draws
                // nothing, so its stale units must go. Same disambiguation as the full walk's ProcessRenderCommands.
                // Re-rendered and drew nothing. That is an ordinary, frequent state - a hover background that just lost
                // the pointer, a close button that faded out - and it happens dozens of times a second while a tab strip
                // scrolls under a still cursor. EMPTY the group; do NOT drop it. Dropping it took the control out of the
                // paint ORDER, so coming back a frame later it had to be re-inserted and its neighbours re-ranked, and
                // whatever the retained stream still said about them no longer held. An empty group draws nothing at zero
                // cost and keeps its rank, so the return is a refill instead of a structural change.
                if (!draw.WasGeometryValid && _groupById.TryGetValue(draw.Component.RenderId, out var emptied))
                {
                    foreach (var unit in emptied.Units) unit?.DeferDispose();
                    emptied.Units.Clear();
                }
                continue;
            }

            _groupById.TryGetValue(draw.Component.RenderId, out var existing);
            // (Re)place in the paint order? Brand new, coming back from hidden, or MOVED (a recycled container re-added
            // elsewhere). The InOrder check matters: a container shown again at the SAME rank still needs re-inserting - a
            // rank compare alone would silently leave it out of the order, drawn nowhere.
            var replace = existing == null || !existing.InOrder || existing.Order != draw.Order;

            var group = BuildUnitsFor(draw.Component, draw.Commands, packet.ProjectionMatrix);
            group.Clones = draw.Clones;   // the THIRD apply path - a clone set has to arrive on all of them, not two
            group.Order = draw.Order;

            if (replace) QueueInsert(group);
        }

        // 4. Kept its units, but its place changed (a recycled container, one shown again, or everything at once after a
        //    renumber): nothing to re-record - just put its group back where the tree now says it belongs.
        foreach (var (component, order) in packet.Reranks)
        {
            if (!_groupById.TryGetValue(component.RenderId, out var group)) continue;
            group.Order = order;
            QueueInsert(group);
        }

        if (_pendingInserts.Count == 0) return;

        // One merge of two sorted sequences - the retained paint order and this frame's arrivals.
        _pendingInserts.Sort(static (a, b) => a.Order.CompareTo(b.Order));
        _mergedGroups.Clear();
        var i2 = 0;
        var j2 = 0;
        while (i2 < _groups.Count && j2 < _pendingInserts.Count)
            _mergedGroups.Add(_groups[i2].Order <= _pendingInserts[j2].Order ? _groups[i2++] : _pendingInserts[j2++]);
        while (i2 < _groups.Count) _mergedGroups.Add(_groups[i2++]);
        while (j2 < _pendingInserts.Count) _mergedGroups.Add(_pendingInserts[j2++]);

        _groups.Clear();
        _groups.AddRange(_mergedGroups);
        foreach (var group in _pendingInserts) group.InOrder = true;
    }

    // Takes a group OUT of the paint order (it stops drawing) without touching its units.
    private void RemoveFromOrder(ControlGroup group)
    {
        if (!group.InOrder) return;
        _groups.Remove(group);
        group.InOrder = false;
        _leftTheOrder.Add(group);   // its instances are still in the arena - see BlankOrphanInstances
    }

    // Queue a group for this frame's ONE merge into the paint order. Deduped: the same group can be named twice in a packet
    // (a renumber reranks everything, and a re-recorded component carries its rank on its draw) - inserting twice would
    // draw it twice.
    private void QueueInsert(ControlGroup group)
    {
        RemoveFromOrder(group);   // no-op when it is not in the order (new, or hidden)
        if (_pendingSet.Add(group)) _pendingInserts.Add(group);
    }

}

