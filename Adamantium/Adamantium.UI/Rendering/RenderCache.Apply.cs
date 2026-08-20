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
    private void AdoptReadyGlyphs()
    {
        if (_renderUnitFactory.GraphicsDevice == null) return;
        if (!Adamantium.Graphics.Fonts.FontAtlasStore.PumpReadyGlyphs()) return;

        foreach (var group in _groups)
        foreach (var unit in group.Units)
        {
            if (unit is RenderUnits.TextRenderUnit text) text.RefreshGlyphsIfArrived();
        }
    }

    // What the recorded op stream actually baked out of a snapshot: where the element is, how big it is, what clips it.
    // Opacity is NOT among them - it is re-composed per unit on every patch - so a fade must not cost a re-record.
    private static bool SameGeometry(LayoutSnapshot a, LayoutSnapshot b) =>
        a.LocalTransform == b.LocalTransform
        && a.RenderSize == b.RenderSize
        && a.ClipToBounds == b.ClipToBounds
        && a.IsMotionNode == b.IsMotionNode
        && ReferenceEquals(a.RenderParent, b.RenderParent);

    // Everything SameGeometry looks at except WHERE the element is. A motion node that only moved differs here and
    // nowhere else, and that difference is exactly the one its transform-table slot carries for the whole subtree.
    private static bool SameGeometryApartFromPlace(LayoutSnapshot a, LayoutSnapshot b) =>
        a.RenderSize == b.RenderSize
        && a.ClipToBounds == b.ClipToBounds
        && a.IsMotionNode == b.IsMotionNode
        && ReferenceEquals(a.RenderParent, b.RenderParent);

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
        // proves every drawn unit under the node is node-aware). The one thing the move CAN stale is a recorded SCISSOR -
        // a world-space rect nothing re-derives - so a node whose subtree clips still forces the rebuild.
        _forgivenMoves.Clear();
        foreach (var node in packet.MovedNodes)
        {
            if (SubtreeClips(node)) _layoutChangedSinceRecord = true;
            else _forgivenMoves.Add(node);
        }

        foreach (var entry in packet.SnapDelta)
        {
            var known = _applySnap.TryGetValue(entry.Key, out var previous);
            if (!known || !SameGeometry(previous, entry.Value))
            {
                // A forgiven node's entry differs in its PLACE and nothing else - that is the move itself, and the slot
                // write carries it. Anything more (it resized, gained a clip, changed parent) is a real layout change.
                if (!known || !_forgivenMoves.Contains(entry.Key) || !SameGeometryApartFromPlace(previous, entry.Value))
                    _layoutChangedSinceRecord = true;
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
                LastBuildTransformDirty |= packet.IsTransformDirty;
                _partialDirty.AddRange(packet.PartialDirty);
                _movedNodesBuf.AddRange(packet.MovedNodes);
                break;
            }

            case RenderBuildKind.Structural:
                ApplyStructural(packet);
                // A Full recorded later in this same drain supersedes it; else this is the strongest kind so far.
                if (LastBuildKind != RenderBuildKind.Full) LastBuildKind = RenderBuildKind.Structural;
                LastBuildTransformDirty = true;
                // The unit set changed, so the op stream + recorded rect slots are stale and the draw pass must re-walk
                // (only Clean/Partial replay). Any earlier partial slot-patch state in this drain is therefore moot.
                _partialDirty.Clear();
                _movedNodesBuf.AddRange(packet.MovedNodes);
                _partialSpliced = false;
                break;

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
            // WITH the draw.
            var pos = _groups.Count;
            for (var i = 0; i < _groups.Count; i++)
            {
                if (_groups[i].Order > order) { pos = i; break; }
            }
            _groups.Insert(pos, group);
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

