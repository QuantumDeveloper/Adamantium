using System.Collections.Generic;
using System.Linq;
using Adamantium.UI.Core;

namespace Adamantium.UI.Rendering;

public partial class RenderCache
{
    private bool _needRenumber;   // PlanNewChildren ran out of rank space -> renumber (a cheap order-walk), not a full-walk fallback

    private readonly List<int> _prevVisible = new();   // BuildNeighbourMaps: nearest painted sibling before i
    private readonly List<int> _nextAnchor = new();    // BuildNeighbourMaps: nearest ranked, not-being-placed sibling after i

    private readonly List<(IUIComponent Component, long Rank)> _plannedList = new();
    private readonly HashSet<IUIComponent> _plannedSet = new();
    private readonly List<IUIComponent> _removedList = new();    // DETACHED: free the units
    private readonly List<IUIComponent> _undrawnList = new();    // HIDDEN: keep the units, just stop drawing
    private readonly HashSet<IUIComponent> _removedSet = new();  // either fate - "left the paint order"

    private readonly List<IUIComponent> _childBuf2 = new();      // TryLastRankOfSubtree / SuccessorRank (never nest with _childBuf)
    private readonly List<IUIComponent> _localChildBuf = new();  // CollectSubtreeInPaintOrder scratch
    private readonly Stack<IUIComponent> _collectStack = new();

    // The pre-validated record decision for each entry of _geometryDirtyBuffer, by INDEX (see RecordStructuralFrame).
    private readonly List<PartialRecord> _dirtyDecisions = new();

    // PLAN, then COMMIT: a refusal must leave the recorder EXACTLY as it found it. The full-walk fallback re-derives the
    // scene from the components' dirty flags, so anything this pass half-rendered would look CLEAN to it and a new tile
    // would never be drawn again. Nothing is rendered/ranked/mirrored until the whole frame is known to be placeable.
    private bool RecordStructuralFrame(IRootVisualComponent visualRoot, RenderPacket packet)
    {
        // The ranks describe the tree of the last full walk. A different root (or none yet) - nothing to splice into.
        if (!ReferenceEquals(_lastVisualRoot, visualRoot) || !HasRank(visualRoot))
        {
            return GaveUp("otherRoot");
        }
        var planStart = System.Diagnostics.Stopwatch.GetTimestamp();
        Core.Diagnostics.RuntimeStats.LastRecordStructuralMarks = 0;
        Core.Diagnostics.RuntimeStats.LastRecordPlanScans = 0;
        Core.Diagnostics.RuntimeStats.LastRecordPlanRuns = 0;
        Core.Diagnostics.RuntimeStats.LastRecordPlanParents = 0;
        Core.Diagnostics.RuntimeStats.LastRecordRenumberMs = 0;
        if (!PlanStructuralChange())
        {
            // Ran out of rank SPACE, not information (each insert into a gap halves it). Renumbering is cheap - an
            // order-only walk + a re-sort - unlike re-recording the whole tree, so don't fall back for it.
            if (!_needRenumber) return GaveUp("cannotPlace");

            RenumberOrder(visualRoot, packet);
            if (!PlanStructuralChange()) return GaveUp("cannotPlaceAfterRenumber");
        }

        // The geometry-dirty components ride along (as in a Partial). Pre-validate against the PLAN before anything
        // renders - a dirty component the partial path cannot place is a refusal, and it must be found first.
        Core.Diagnostics.RuntimeStats.LastRecordPlanOnlyMs = System.Diagnostics.Stopwatch.GetElapsedTime(planStart).TotalMilliseconds;

        Dirty.SnapshotGeometryInto(_geometryDirtyBuffer);
        Core.Diagnostics.RuntimeStats.LastRecordDirty = _geometryDirtyBuffer.Count;
        // KEPT, positionally: the commit loop below walks this very buffer again and would otherwise re-derive every one
        // of these decisions - the same ancestor walk, twice per component per frame. Skip is the placeholder for the
        // entries this loop steps over; the commit loop steps over exactly the same ones.
        _dirtyDecisions.Clear();
        foreach (var component in _geometryDirtyBuffer)
        {
            if (_plannedSet.Contains(component) || _removedSet.Contains(component))
            {
                _dirtyDecisions.Add(PartialRecord.Skip);
                continue;
            }

            var decision = ClassifyReRender(component);
            if (decision == PartialRecord.Fallback) return GaveUp($"dirtyNotPlaceable<{component.GetType().Name}>");
            _dirtyDecisions.Add(decision);
        }
        Core.Diagnostics.RuntimeStats.LastRecordPlanMs = System.Diagnostics.Stopwatch.GetElapsedTime(planStart).TotalMilliseconds;

        // ---- COMMIT: from here nothing can refuse. ----
        _structRecorded.Clear();
        foreach (var (component, rank) in _plannedList) RankAndRecord(component, rank, packet);
        foreach (var component in _removedList) FreeComponent(component, packet, detached: true);
        foreach (var component in _undrawnList) FreeComponent(component, packet, detached: false);

        for (var i = 0; i < _geometryDirtyBuffer.Count; i++)
        {
            var component = _geometryDirtyBuffer[i];
            if (_structRecorded.Contains(component) || _removedSet.Contains(component)) continue;
            RecordReRender(component, packet, _dirtyDecisions[i]);   // pre-validated above: it cannot fall back now
        }

        // A component's Render can mark MORE geometry dirty (an image decode). Those marks clear with the rest, so a
        // component that arrived mid-pass would be lost and stay dirty forever - force a full walk NEXT frame instead
        // (the request keeps it from being dropped; the loop only runs when something asks - see LoopSignal).
        if (Dirty.GeometryCount != _geometryDirtyBuffer.Count)
        {
            _forceFullNextFrame = true;
            Core.LoopSignal.Request();
        }

        // A splice changes WHICH units exist and where they sit, so the applier re-bakes every transform and re-walks the
        // draw (no op-stream replay). That cost lands on the render thread - the point of moving the record off the loop.
        packet.IsTransformDirty = true;
        packet.ClearMemos = true;
        Dirty.SnapshotNodesInto(packet.MovedNodes);
        Dirty.SnapshotMovedInto(_movedBuf);
        return true;
    }

    // Re-derive the paint order with FRESH GAPS and nothing else: only the NUMBERS change, so each group just moves to its
    // new sorted place (the applier re-sorts on the Reranks below). No Render, no draw commands, no unit work.
    private void RenumberOrder(IRootVisualComponent visualRoot, RenderPacket packet)
    {
        LayerProbe.Renumbers++;
        var renumberStart = System.Diagnostics.Stopwatch.GetTimestamp();
        packet.Renumbered = true;
        _orderByControl.Clear();

        var stack = _walkStack;
        var visited = _walkVisited;
        stack.Clear();
        visited.Clear();
        stack.Push((visualRoot, false));
        long order = 0;

        while (stack.Count > 0)
        {
            var (component, _) = stack.Pop();
            // Same rule as the full walk: only COLLAPSED leaves the order. A hidden element keeps its rank, so coming
            // back is a refill of a group that never moved rather than a placement.
            if (component.Visibility == Visibility.Collapsed) continue;
            if (!visited.Add(component)) continue;

            _orderByControl[component] = order;
            // Only a component that actually DRAWS has a group to move; the rest just need the number.
            if (HoldsUnits(component)) packet.Reranks.Add(new KeyValuePair<IUIComponent, long>(component, order));
            order += OrderGap;

            PushChildrenInPaintOrder(stack, component.VisualChildren, false);
        }

        _needRenumber = false;
        Core.Diagnostics.RuntimeStats.LastRecordRenumberMs += System.Diagnostics.Stopwatch.GetElapsedTime(renumberStart).TotalMilliseconds;
    }

    // TEMP: name the give-up so a full walk in the trace says WHY it is one.
    private static bool GaveUp(string reason)
    {
        if (Core.Diagnostics.FrameTrace.Enabled) Core.Diagnostics.FrameTrace.FullWalkReason = reason;
        return false;
    }

    private bool PlanStructuralChange()
    {
        _needRenumber = false;
        // The sibling index describes the tree as it stands NOW. A plan only reads the tree, so it is good for the whole
        // of one - but a renumber runs a SECOND plan, and between frames the tree really does change.
        _nextSibling.Clear();
        _siblingsIndexed.Clear();
        Dirty.SnapshotStructuralInto(_structuralBuf);
        Core.Diagnostics.RuntimeStats.LastRecordStructuralMarks += _structuralBuf.Count;
        _placeRoots.Clear();
        _addsByParent.Clear();
        _plannedList.Clear();
        _plannedSet.Clear();
        _plannedRanks.Clear();
        _removedList.Clear();
        _undrawnList.Clear();
        _removedSet.Clear();

        // 1. Classify by CURRENT state: a container removed and re-added in the same frame (a recycled tile) is an ADD.
        foreach (var component in _structuralBuf)
        {
            if (component == null || !IsDrawn(component)) continue;

            // Climb to the OUTERMOST unranked component. A new subtree is assembled BEFORE it is attached, so all its
            // marks resolve to the same root here - which is what makes the run contiguous.
            var top = component;
            while (top.VisualParent != null && !HasRank(top.VisualParent)) top = top.VisualParent;
            if (top.VisualParent == null) continue;   // the visual root itself - it never moves

            _placeRoots.Add(top);
        }

        // 2. Group by parent: a run of new siblings shares one rank gap, so they must be ranked together.
        foreach (var component in _placeRoots)
        {
            var parent = component.VisualParent;
            if (!HasRank(parent))   // the climb should have made this impossible - refuse rather than guess
            {

                return false;
            }
            if (!_addsByParent.TryGetValue(parent, out var list))
            {
                list = new List<IUIComponent>();
                _addsByParent[parent] = list;
            }
            list.Add(component);
        }

        foreach (var (parent, _) in _addsByParent)
            if (!PlanNewChildren(parent)) return GaveUp($"newChildren<{parent.GetType().Name}>");

        // 3. What LEFT the drawn set. The subtree is still intact (a detach doesn't tear it apart), so walk it; anything
        //    inside that actually MOVED was planned above and is skipped.
        foreach (var component in _structuralBuf)
        {
            if (component == null || IsDrawn(component)) continue;
            PlanRemoval(component);
        }

        return true;
    }

    // Rank every component of every NEW subtree under one parent strictly between its ranked neighbours - no existing
    // component is renumbered. Neighbours come from TWO index maps built in ONE pass over the children, NOT a per-run
    // sibling re-scan: recycled skeleton cards split the adds into many runs, so that re-scan was O(children x runs).
    private bool PlanNewChildren(IUIComponent parent)
    {
        Core.Diagnostics.RuntimeStats.LastRecordPlanParents++;
        ChildrenInPaintOrder(parent, _childBuf); Core.Diagnostics.RuntimeStats.ScansParent += _childBuf.Count;
        BuildNeighbourMaps(parent);

        for (var i = 0; i < _childBuf.Count; i++)
        {
            if (!_placeRoots.Contains(_childBuf[i])) continue;

            // The contiguous RUN of new children starting here: one gap serves them all.
            var j = i;
            while (j + 1 < _childBuf.Count && _placeRoots.Contains(_childBuf[j + 1])) j++;

            Core.Diagnostics.RuntimeStats.LastRecordPlanRuns++;

            // The rank immediately BEFORE the run: walk back over hidden siblings to the nearest visible one that HAS a
            // rank (incl. a tile an earlier run of this plan just placed), take the last rank in its subtree; none -> the parent.
            long prev;
            if (!TryRankBefore(i, parent, out prev)) return false;

            // ...and immediately AFTER it: the nearest visible sibling not itself being placed (a later run squeezes
            // between our last rank and this same anchor), else whatever follows the parent's whole subtree.
            var after = _nextAnchor[j];
            var next = after >= 0 ? RankOf(_childBuf[after]) : SuccessorRank(parent);

            // The run's subtrees in paint order - the sequence of ranks to hand out.
            _subtreeBuf.Clear();
            for (var k = i; k <= j; k++) CollectSubtreeInPaintOrder(_childBuf[k], _subtreeBuf);

            var needed = _subtreeBuf.Count;
            if (needed == 0) { i = j; continue; }

            long step;
            if (next == long.MaxValue) step = OrderGap;   // appending at the end: there is always room
            else
            {
                step = (next - prev) / (needed + 1);
                if (step < 1)
                {

                    _needRenumber = true;   // not a refusal - just a renumber, then this plan runs again with room to spare
                    return false;
                }
            }

            var cursor = prev;
            foreach (var component in _subtreeBuf)
            {
                cursor += step;
                _plannedList.Add((component, cursor));
                _plannedSet.Add(component);
                _plannedRanks[component] = cursor;   // ...so a LATER run of this same plan can place itself against it
            }

            i = j;
        }
        return true;
    }

    // One pass over the parent's children, giving each position two anchors: _prevVisible[i] = nearest PAINTED sibling
    // before i; _nextAnchor[i] = nearest painted sibling after i that already HOLDS a rank and isn't being placed. Per-run
    // lookup is then O(1) instead of a walk of the whole sibling list.
    private void BuildNeighbourMaps(IUIComponent parent)
    {
        var count = _childBuf.Count;
        _prevVisible.Clear();
        _nextAnchor.Clear();
        for (var i = 0; i < count; i++) { _prevVisible.Add(-1); _nextAnchor.Add(-1); }

        // SPANS over the three lists. These two loops walk EVERY child of the parent - up to 21 000 on the tile panel,
        // twice - and a List<T> indexer is a call plus a bounds check on each of those. The spans are taken after the
        // fills above and nothing here resizes a list, so they stay valid for the walk.
        var children = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_childBuf);
        var prev = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_prevVisible);
        var next = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_nextAnchor);

        var lastVisible = -1;
        for (var i = 0; i < count; i++)
        {
            prev[i] = lastVisible;
            if (children[i].Visibility == Visibility.Visible) lastVisible = i;
        }

        var nextAnchored = -1;
        for (var i = count - 1; i >= 0; i--)
        {
            next[i] = nextAnchored;
            var child = children[i];
            if (child.Visibility == Visibility.Visible && !_placeRoots.Contains(child) && HasRank(child)) nextAnchored = i;
        }
    }

    // The rank the run at `i` comes AFTER: the last rank inside the subtree of the nearest PAINTED sibling before it that
    // holds a rank (committed or planned by an earlier run); none painted before -> the parent. Fails when a painted
    // sibling holds no rank at all - it entered with nothing naming it, so the splice has nothing to place against.
    private bool TryRankBefore(int i, IUIComponent parent, out long rank)
    {
        rank = 0;
        var at = _prevVisible[i];
        if (at < 0)
        {
            rank = RankOf(parent);   // nothing is painted before it: the parent bounds the run
            return true;
        }

        var before = _childBuf[at];
        if (!HasPlannedRank(before)) return false;   // painted, but unnamed - not locally decidable
        return TryLastRankOfSubtree(before, out rank);
    }

    // Everything under a component that left the DRAWN set - two very different fates:
    //  - DETACHED from the tree: gone. Free its units.
    //  - HIDDEN (or under something hidden): still there and likely back soon (a recycled list container). It leaves the
    //    paint order but KEEPS its units, so a re-show is a re-insert, not a GPU-buffer rebuild - freeing here would churn
    //    every container on every scroll step.
    // Either way its RANK and frozen layout go (re-frozen when it returns), else both maps grow forever on a recycling list.
    private void PlanRemoval(IUIComponent component)
    {
        _subtreeStack.Clear();
        _subtreeStack.Push(component);
        while (_subtreeStack.Count > 0)
        {
            var c = _subtreeStack.Pop();
            if (_plannedSet.Contains(c)) continue;   // it did not leave - it MOVED, and was planned above

            // Ask the mirror of the applier's groups (_recordedUnits), NOT the rank: the rank is dropped the moment a
            // component leaves the drawn set, so a component whose rank was gone but whose GROUP still existed was silently
            // skipped - the applier kept painting it, frozen at its last slot on top of real content (the stuck skeleton card).
            var known = HoldsUnits(c) || HasRank(c);
            if (known && _removedSet.Add(c))
            {
                // Hidden keeps its units; so does PARKED - out of the tree on purpose and coming back, which is the whole
                // point of the mark. Only an unmarked detach means gone.
                if (c.IsAttachedToVisualTree || c.IsParked) _undrawnList.Add(c);
                else _removedList.Add(c);
            }

            // Descend regardless of the child's own visibility: the whole subtree stops being drawn with its root.
            foreach (var child in c.VisualChildren) _subtreeStack.Push(child);
        }
    }

    // Give one component its new rank: a re-record when it draws something new (a freshly realized tile), or just the rank
    // when it kept its units and only moved in the order (a recycled container re-added at another index).
    private void RankAndRecord(IUIComponent component, long rank, RenderPacket packet)
    {
        _orderByControl[component] = rank;
        _structRecorded.Add(component);

        if (component.IsGeometryValid && HoldsUnits(component))
        {
            Core.Diagnostics.RuntimeStats.LastRecordReranks++;
            packet.Reranks.Add(new KeyValuePair<IUIComponent, long>(component, rank));
            return;
        }

        var wasGeometryValid = component.IsGeometryValid;
        var renderStart = System.Diagnostics.Stopwatch.GetTimestamp();
        _drawingContextInternal.Clear();
        component.Render(_drawingContext);
        Core.Diagnostics.RuntimeStats.LastRecordRenderMs += System.Diagnostics.Stopwatch.GetElapsedTime(renderStart).TotalMilliseconds;
        var copyStart = System.Diagnostics.Stopwatch.GetTimestamp();
        var commands = CopyCommands(_drawingContextInternal.GetDrawCommands());
        Core.Diagnostics.RuntimeStats.LastRecordCopyMs += System.Diagnostics.Stopwatch.GetElapsedTime(copyStart).TotalMilliseconds;
        // Only when it really re-recorded: a component that was already geometry-VALID no-ops inside Render, so its empty
        // command list says "nothing was re-recorded", not "it draws nothing".
        if (!wasGeometryValid) component.DrawsNothing = commands.Count == 0;
        if (commands.Count == 0)
        {
            Core.Diagnostics.RuntimeStats.LastRecordEmptyDraws++;
            Core.Diagnostics.RuntimeStats.NoteEmptyDraw(component.GetType());
        }

        packet.Draws.Add(new ComponentDraw(component, commands, wasGeometryValid, rank, component.RenderClones));
        MirrorUnits(component, commands.Count, wasGeometryValid);
    }

    // COMMIT a planned removal. Both fates drop the RANK and the frozen layout (re-frozen on return); they differ in the
    // units - a DETACHED component's are freed, a HIDDEN one's are kept (and so is its mirror entry, for a re-show).
    private void FreeComponent(IUIComponent component, RenderPacket packet, bool detached)
    {
        _orderByControl.Remove(component);
        _snap.Remove(component);

        if (detached)
        {
            _recordedUnits.Remove(component);
            packet.Removed.Add(component);
        }
        else
        {
            packet.Undrawn.Add(component);
        }
    }

    // The LAST rank inside a component's painted subtree (its deepest last-painted descendant) - the rank a sibling
    // appended after it must come after. Fails if the subtree is not ranked (nothing to interpolate against).
    private bool TryLastRankOfSubtree(IUIComponent component, out long rank)
    {
        if (!TryPlannedRank(component, out rank)) return false;

        var last = component;
        while (true)
        {
            ChildrenInPaintOrder(last, _childBuf2); Core.Diagnostics.RuntimeStats.ScansLastRank += _childBuf2.Count;
            IUIComponent tail = null;
            for (var i = _childBuf2.Count - 1; i >= 0; i--)
            {
                var candidate = _childBuf2[i];   // read ONCE - this ran three indexer calls on the same element
                if (candidate.Visibility == Visibility.Visible && HasPlannedRank(candidate)) { tail = candidate; break; }
            }
            if (tail == null) break;
            last = tail;
        }
        return TryPlannedRank(last, out rank);
    }

    // The rank painted immediately AFTER a component's whole subtree: its next painted sibling, else its parent's, up to
    // the root. long.MaxValue = nothing follows (the run appends at the very end).
    private long SuccessorRank(IUIComponent component)
    {
        for (var c = component; c?.VisualParent != null; c = c.VisualParent)
        {
            IndexSiblings(c.VisualParent);
            for (var s = NextSibling(c); s != null; s = NextSibling(s))
                if (s.Visibility == Visibility.Visible && TryPlannedRank(s, out var rank)) return rank;
        }
        return long.MaxValue;
    }

    // WHO comes next among a parent's painted children - built ONCE per parent per plan. This used to be answered by
    // copying the whole child list and scanning it for `c`, per call, and the call is made once per parent being placed:
    // a tab arriving into a container of 5000 siblings placed 228 marks and re-read 1.14 MILLION children to do it, all
    // of it in here (ScansSuccessor was 99.98% of the total). The same "re-scan the siblings per placement" shape
    // BuildNeighbourMaps was already written to avoid - it just was not applied to this one.
    //
    // The forward scan below is still O(siblings) in the worst case (nothing after `c` holds a rank at all); what it is
    // not any more is O(siblings) PER PLACEMENT, which is the part that made a tab switch stutter.
    private readonly Dictionary<IUIComponent, IUIComponent> _nextSibling = new();
    private readonly HashSet<IUIComponent> _siblingsIndexed = new();

    private void IndexSiblings(IUIComponent parent)
    {
        if (!_siblingsIndexed.Add(parent)) return;

        ChildrenInPaintOrder(parent, _childBuf2);
        Core.Diagnostics.RuntimeStats.ScansSuccessor += _childBuf2.Count;
        // Span + carry the NEXT element into the following iteration: the list was indexed twice per step (i and i+1) for
        // the same elements, and this runs over the whole child list of every parent a plan touches.
        var siblings = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_childBuf2);
        for (var i = 0; i + 1 < siblings.Length; i++) _nextSibling[siblings[i]] = siblings[i + 1];
    }

    private IUIComponent NextSibling(IUIComponent component) =>
        _nextSibling.TryGetValue(component, out var next) ? next : null;

    // A component's visible subtree, in paint order. Iterative with a reused stack (the recursive version allocated an
    // array per node per structural frame, and a fill collects thousands of subtrees).
    private void CollectSubtreeInPaintOrder(IUIComponent root, List<IUIComponent> into)
    {
        if (root.Visibility != Visibility.Visible) return;

        _collectStack.Clear();
        _collectStack.Push(root);
        while (_collectStack.Count > 0)
        {
            var component = _collectStack.Pop();
            if (component.Visibility != Visibility.Visible) continue;
            into.Add(component);

            // Push reversed, so they pop in paint order.
            ChildrenInPaintOrder(component, _localChildBuf); Core.Diagnostics.RuntimeStats.ScansCollect += _localChildBuf.Count;
            for (var i = _localChildBuf.Count - 1; i >= 0; i--) _collectStack.Push(_localChildBuf[i]);
        }
    }

    // A component's children in PAINT order (drawn first = underneath) - the same precedence the full walk's stack uses.
    private static void ChildrenInPaintOrder(IUIComponent parent, List<IUIComponent> into)
    {
        into.Clear();
        foreach (var child in parent.VisualChildren) into.Add(child);
        Core.Diagnostics.RuntimeStats.LastRecordPlanScans += into.Count;

        var anyZ = false;
        foreach (var child in into)
            if (child.ZIndex != 0) { anyZ = true; break; }
        if (!anyZ) return;

        // Stable sort by ZIndex (document order within a Z), mirroring PushChildrenInPaintOrder / the hit-test's ZSort.
        var ordered = into.Select((child, index) => (child, index))
            .OrderBy(x => x.child.ZIndex)
            .ThenBy(x => x.index)
            .Select(x => x.child)
            .ToArray();
        into.Clear();
        into.AddRange(ordered);
    }

}
