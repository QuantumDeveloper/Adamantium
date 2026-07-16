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
    /// paint-order groups. Freezes the layout snapshot the draw pass replays, then clears RenderDirty.</summary>
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

        // Only the single-threaded path (record+apply inline in BeginDraw) clears here. The decoupled path hoists the
        // clear to loop level (UIApplication.RecordRenderFrame), once after ALL windows record - so a second window still
        // sees the full dirty set this frame.
        if (drew && RenderThreadOptions.SingleThreaded) RenderDirty.Clear();

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
        foreach (var atlas in _warmAtlases) atlas.Warm(text);
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
        foreach (var entry in packet.SnapDelta) _applySnap[entry.Key] = entry.Value;

        switch (packet.Kind)
        {
            case RenderBuildKind.Clean:
                break;   // re-draw the retained units as-is (nothing to realize)

            case RenderBuildKind.Partial:
            {
                // APPLY pass (GPU): realize the recorded draws - update the units in place / splice a count change.
                foreach (var draw in packet.Draws)
                    ApplyReRender(draw.Component, draw.Commands, draw.Order);

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
                ApplyFullWalk(packet);   // GPU: rebuild the paint-order groups from the packet
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
    // Fallback = the caller must do a full walk; Recorded = its commands were captured into the packet for the applier.
    private enum PartialRecord { Skip, Fallback, Recorded }

    // The DECISION for one geometry-dirty component - PURE: it reads state and renders nothing, so the structural pass can
    // pre-validate the frame before it commits to anything (see RecordStructuralFrame).
    private PartialRecord ClassifyReRender(IUIComponent component)
    {
        // Invisible (Collapsed/Hidden - a recycled container, an auto-hide ScrollBar re-marking dirty on mouse-move):
        // draws NOTHING, nothing to record. Its units are retained and its dirty flag stays set until it is shown again,
        // so it re-records at the right time (the structural splice that puts it back), not now. A full walk here meant a
        // whole-tree re-record for every hidden container that so much as re-bound.
        if (component.Visibility != Visibility.Visible) return PartialRecord.Skip;

        // Not in the live paint tree: DETACHED, or hidden by a COLLAPSED ancestor. The full walk never reaches it, so it
        // has no rank and draws nothing - yet it used to force a full rebuild EVERY dirty frame. Skip: it holds no units
        // (a real detach/collapse is STRUCTURAL and already removed them).
        if (!component.IsAttachedToVisualTree) return PartialRecord.Skip;
        for (var a = component.VisualParent; a != null; a = a.VisualParent)
            if (a.Visibility != Visibility.Visible) return PartialRecord.Skip;

        // From a FOREIGN tree (a popup/menu/tooltip PopupRoot, drawn by the popup stage's OWN cache). Skip WITHOUT
        // rendering: Render() would consume its IsGeometryValid=false, and that flag is what PopupRenderProcessor's
        // rebuild gate (OverlayChanged) polls - the MAIN cache eating it starved the gate (a menu hover never redrew).
        if (_lastVisualRoot != null)
        {
            var top = component;
            while (top.VisualParent != null) top = top.VisualParent;
            if (!ReferenceEquals(top, _lastVisualRoot)) return PartialRecord.Skip;
        }

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
        if (decision != PartialRecord.Recorded) return decision;

        var rank = RankOf(component);
        _drawingContextInternal.Clear();
        component.Render(_drawingContext);   // NB: consumes the dirty flag (Render sets IsGeometryValid back to true)
        var commands = CopyCommands(_drawingContextInternal.GetDrawCommands());
        packet.Draws.Add(new ComponentDraw(component, commands, false, rank));
        MirrorUnits(component, commands.Count, false);   // it WAS dirty: no commands now means "draws nothing" -> units freed
        return PartialRecord.Recorded;
    }

    // APPLY half (GPU): realize ONE recorded partial draw - update the group's units in place (same count+type) or splice
    // in the count/type change. `order` (the paint rank) rides WITH the draw, so a group appearing for the first time is
    // placed without the applier ever reading the recorder's rank map.
    private void ApplyReRender(IUIComponent component, IReadOnlyList<IDrawCommand> drawCommands, long order)
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
                group.Order = order;
                return;
            }
        }

        // Count/type changed. The change stays LOCAL to this control's group (BuildUnitsFor refreshes its Units in place,
        // no other group moves). The recorded op stream + rect-slot map still reference the old unit set, so the draw
        // phase re-walks this frame (per-group op patching is the planned follow-up).
        _partialSpliced = true;
        var isNewGroup = group == null;

        group = BuildUnitsFor(component, drawCommands, _projectionMatrix);
        group.Order = order;

        if (isNewGroup)
        {
            // First units this control ever drew (a background appearing 0->1): insert its group by paint rank, before the
            // first group that ranks after it. Existing groups never move; the rank came WITH the draw.
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
                if (!draw.WasGeometryValid) RemoveAndDeferDispose(draw.Component.RenderId);
                continue;
            }

            _groupById.TryGetValue(draw.Component.RenderId, out var existing);
            // (Re)place in the paint order? Brand new, coming back from hidden, or MOVED (a recycled container re-added
            // elsewhere). The InOrder check matters: a container shown again at the SAME rank still needs re-inserting - a
            // rank compare alone would silently leave it out of the order, drawn nowhere.
            var replace = existing == null || !existing.InOrder || existing.Order != draw.Order;

            var group = BuildUnitsFor(draw.Component, draw.Commands, packet.ProjectionMatrix);
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
