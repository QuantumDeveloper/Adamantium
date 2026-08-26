namespace Adamantium.UI.Core.Diagnostics;

/// <summary>
/// Lightweight live counters for a runtime diagnostics overlay, so the otherwise-invisible work of the layout manager,
/// the binding batcher and the animation heartbeat can be SEEN at runtime (verification, not just unit tests). All
/// writes are cheap field updates on the UI thread; a reader (the overlay) samples them once per frame. The last-pass
/// fields are snapshots of the most recent layout pass; the cumulative counters are meant to be sampled by per-frame
/// delta.
/// </summary>
public static class RuntimeStats
{
    /// <summary>Wall-clock duration of the most recent layout pass, in milliseconds (~0 on an idle frame, which is the
    /// whole point of the dirty-queue model: no per-frame tree walk).</summary>
    public static double LastLayoutPassMs;

    /// <summary>True if the most recent layout pass hit the frame budget and deferred work to a later frame.</summary>
    public static bool LastPassBudgetDeferred;

    // Per-frame render-pipeline phase timings (ms), snapshots of the most recent frame. Phase 0 of the render-cache
    // redesign (docs/RENDER_CACHE_REDESIGN.md): make the otherwise-invisible per-frame render cost measurable, so the
    // retained rewrite can be aimed at the phase that actually dominates (today the per-frame cache REBUILD, not layout).
    /// <summary>RenderCache.BuildFromVisualTree - the per-frame walk that re-records every component's draw commands.</summary>
    public static double LastRenderBuildMs;
    /// <summary>RenderCache.ProcessCommands - re-bake every cached unit's world transform.</summary>
    /// <summary>The two halves of the render BUILD, so a spike says WHICH: the device-free record (walk + component.Render
    /// into the packet) or the apply that realizes it into units.</summary>
    public static double LastRecordMs;
    public static double LastApplyMs;

    /// <summary>What one APPLY FRAME did, so a long apply is attributed instead of guessed at. Per FRAME, not per packet:
    /// ApplyFrame drains every packet the recorder published since the last one, and describing only the last of them is
    /// how a 293ms apply came to be reported as "Clean, 0 draws". The sum of the parts against LastApplyMs is the only
    /// thing that proves the whole is accounted for - and twice in one session it was not.</summary>
    public static string LastApplyKind = "-";
    public static int LastApplyPackets;
    public static int LastApplyDraws;
    public static double LastApplyStructuralMs;
    public static double LastApplyReRenderMs;
    public static double LastApplyGlyphMs;
    public static double LastApplyBuildMs;
    public static double LastApplyMergeMs;

    /// <summary>Of the build loop, how much is spent INSIDE the render units (create/update) versus everything else the
    /// loop does. The two have completely different fixes, and the per-draw cost varies tenfold between frames - which
    /// no count explains, so the split has to be timed rather than reasoned about.</summary>
    public static double LastApplyUnitMs;

    /// <summary>The single slowest unit update of the frame, and what kind of unit it was. A per-type total would need a
    /// map on the hot path; the worst one names the culprit just as well when the spread is 26-73us against a fast path
    /// that should cost nothing.</summary>
    public static string LastApplySlowestUnit = "-";
    public static double LastApplySlowestUnitMs;

    /// <summary>The RECORD half, split the same way: how long the components' own Render calls took, how many draws came
    /// out empty, and how many components got away with a rank change instead of a re-record. A structural frame that
    /// re-records ten times more components than there are tiles on screen is a question these three answer.</summary>
    public static double LastRecordRenderMs;
    public static int LastRecordEmptyDraws;
    public static int LastRecordReranks;

    /// <summary>The rest of the record, so the four parts can be held against LastRecordMs. Timing only the structural
    /// placement path is how a 432ms record came to report zero of everything: a resize re-records through the
    /// geometry-dirty path, which places nothing.</summary>
    public static double LastRecordPlanMs;
    public static double LastRecordCopyMs;
    public static double LastRecordSnapMs;
    public static int LastRecordDirty;
    public static int LastRecordClassifySkips;

    /// <summary>WHICH types re-record into zero draw commands, by count. Three quarters of a resize record is spent
    /// rendering elements that draw nothing; whether that is fixed by a type rule or by a per-element memo depends on
    /// what they ARE, and only a histogram says. Written on the record thread, read once a second.</summary>
    /// <summary>Keyed by TYPE, not by its name: this is written once per recorded component, and hashing a string there
    /// is a cost the thing being measured does not have.</summary>
    public static readonly System.Collections.Generic.Dictionary<System.Type, int> EmptyDrawsByType = new();

    /// <summary>Guards BOTH histograms below. They are written from the RECORD thread and read + cleared once a second
    /// from the probe thread, and a Dictionary torn by that race does not report a wrong number - it crashes the reader
    /// with a NullReferenceException inside the enumerator. It did, on a tab switch. A diagnostic must not be able to
    /// kill the thing it measures; the writes are a few thousand a second, so an uncontended lock costs nothing that
    /// matters.</summary>
    public static readonly object HistogramLock = new();

    public static void NoteEmptyDraw(System.Type type)
    {
        lock (HistogramLock)
        {
            EmptyDrawsByType.TryGetValue(type, out var n);
            EmptyDrawsByType[type] = n + 1;
        }
    }

    /// <summary>...and which types the record actually SPENDS its time in, in ms. Skipping the components that draw
    /// nothing left the record at 188ms over ~5300 real records - 35us each - and no count says which of them that is.</summary>
    public static readonly System.Collections.Generic.Dictionary<System.Type, double> RecordMsByType = new();

    public static void NoteRecordMs(System.Type type, double ms)
    {
        lock (HistogramLock)
        {
            RecordMsByType.TryGetValue(type, out var t);
            RecordMsByType[type] = t + ms;
        }
    }

    /// <summary>The snapshot freeze, split by WHICH loop: the packet's re-records, or the whole geometry-dirty set (which
    /// the skip deliberately left intact - a container that clips must clip at its new size). Predicting that the second
    /// would fall with the first was wrong; the two are separately measured now.</summary>
    public static double LastSnapDrawsMs;
    public static double LastSnapDirtyMs;
    public static double LastSnapOpacityMs;
    public static double LastSnapTailMs;
    public static int LastSnapPublished;

    /// <summary>...and how much each half ALLOCATES. A record that allocates 1.7MB a frame pays for it later as a GC
    /// pause, which no stage timer inside the frame can see - the whole-loop lesson again.</summary>
    public static long LastRecordRenderBytes;
    public static long LastRecordCopyBytes;
    public static long LastSnapBytes;

    /// <summary>What the APPLY allocates. A tab switch adds ~145MB to the heap in one second and never gives it back
    /// (gen2 never runs), which is 40KB per attached component - orders of magnitude more than the record's per-command
    /// objects. Splitting record from apply is what says which half to look in.</summary>
    public static long LastApplyBytes;

    /// <summary>...and the two phases that run on EVERY frame whether or not anything changed. An idle window allocates
    /// ~70KB a frame while recording and applying nothing at all, so the waste is in one of these or in the loop around
    /// them. Cumulative - sampled by per-second delta.</summary>
    /// <summary>What the LAYOUT pass allocates, cumulative. Splits a tab build into "laying it out" and "constructing it".</summary>
    public static long LayoutBytes;

    /// <summary>The pass's three phases apart - style, measure, arrange - plus how many ITERATIONS of the drain loop it
    /// took. The pass costs 300-490ms on a tab switch and is the whole loop; a re-dirty loop that runs the queues several
    /// times over would look identical from outside, which is why the iteration count is here too. Cumulative.</summary>
    public static double LayoutStyleMs;
    public static double LayoutMeasureMs;
    public static double LayoutArrangeMs;
    public static int LayoutIterations;

    /// <summary>...and how many PASSES those iterations belong to. 34 iterations is a re-dirty spin if it is one pass and
    /// perfectly normal if it is 34 passes; the number alone cannot tell them apart.</summary>
    public static int LayoutPasses;

    /// <summary>...and WHICH component types allocate it. A tab switch spends ~125MB inside the layout pass over ~10500
    /// measure/arrange calls - 12KB each, which is far more than the elements themselves weigh, so it is temporaries.
    /// Guarded like the other histograms: arrange runs on worker threads too (see MarkGeometry), and a torn Dictionary
    /// crashes its reader rather than mis-reporting.</summary>
    public static readonly System.Collections.Generic.Dictionary<System.Type, long> LayoutBytesByType = new();
    public static readonly System.Collections.Generic.Dictionary<System.Type, int> LayoutCountByType = new();

    /// <summary>Bytes this thread has attributed to the CHILDREN of the layout call currently on its stack. Measure and
    /// arrange RECURSE, so timing a call from the outside charges every descendant's allocation to the ancestor: the
    /// first version of this histogram reported 408MB against StackPanel on a frame that allocated 147MB in total, which
    /// names the root of the tree rather than whoever is doing the allocating.</summary>
    [System.ThreadStatic] private static long _layoutChildBytes;

    /// <summary>Open a layout frame: returns the allocation mark to close it with, and parks the enclosing call's child
    /// total so this one starts from zero.</summary>
    /// <summary>Same nesting subtraction, for TIME: measure recurses into children, so timing a call from the outside
    /// charges every descendant to the ancestor. 6716 measures in 163-278ms is 24-41us a call ONLY if the nesting is
    /// taken out first - otherwise the deepest ancestor gets the whole tree.</summary>
    [System.ThreadStatic] private static double _layoutChildMs;

    public static readonly System.Collections.Generic.Dictionary<System.Type, double> LayoutMsByType = new();

    public static (long Mark, long OuterChildren, long Ticks, double OuterMs) BeginLayoutFrame()
    {
        var outer = _layoutChildBytes;
        var outerMs = _layoutChildMs;
        _layoutChildBytes = 0;
        _layoutChildMs = 0;
        return (System.GC.GetAllocatedBytesForCurrentThread(), outer, System.Diagnostics.Stopwatch.GetTimestamp(), outerMs);
    }

    /// <summary>Close it: charge the type with what it allocated and SPENT itself, and hand the whole to the enclosing call.</summary>
    public static void EndLayoutFrame(System.Type type, (long Mark, long OuterChildren, long Ticks, double OuterMs) frame)
    {
        var total = System.GC.GetAllocatedBytesForCurrentThread() - frame.Mark;
        var self = total - _layoutChildBytes;
        _layoutChildBytes = frame.OuterChildren + total;

        var totalMs = System.Diagnostics.Stopwatch.GetElapsedTime(frame.Ticks).TotalMilliseconds;
        var selfMs = totalMs - _layoutChildMs;
        _layoutChildMs = frame.OuterMs + totalMs;

        lock (HistogramLock)
        {
            LayoutBytesByType.TryGetValue(type, out var b);
            LayoutBytesByType[type] = b + self;
            LayoutMsByType.TryGetValue(type, out var t);
            LayoutMsByType[type] = t + selfMs;
            LayoutCountByType.TryGetValue(type, out var n);
            LayoutCountByType[type] = n + 1;
        }
    }

    public static long PreRenderBytes;
    public static long DrawBytes;

    /// <summary>...split inside the draw. A CLEAN frame only re-issues the recorded op stream, and it allocates ~70KB
    /// doing it; these say whether that is the replay itself or the work in front of it. Cumulative.</summary>
    public static long ExecuteOpsBytes;
    public static long DrawSetupBytes;
    public static int LastOpsExecuted;

    /// <summary>...and by KIND of op, because scissor changes, per-unit draws and batch segments allocate for completely
    /// different reasons. Indexed by RenderOpKind. Cumulative bytes and per-frame counts.</summary>
    public static readonly long[] OpBytesByKind = new long[4];
    public static readonly int[] OpCountByKind = new int[4];

    /// <summary>Inside ONE batched draw: applying the effect pass (push data + heap offsets, which go through the
    /// generated Vulkan marshalling) against issuing the draw itself (dynamic state, likewise). ~711 bytes are allocated
    /// per segment draw and neither the parameter writes nor the scissor arrays account for it.</summary>
    public static long PassApplyBytes;
    public static long DeviceDrawBytes;
    public static int PassApplyCount;

    /// <summary>DrawRecordedSegment, split three ways. The two SetScissors calls it makes per segment are the reason the
    /// standalone scissor ops undercounted them ~120x: a segment draw sets its own clip and restores the full one.</summary>
    /// <summary>The text batch draw, split: the stride lookup, the parameter/resource binding block, and the pass apply
    /// plus draw. It costs 440 bytes a segment against the SDF path's 195, and the difference is in one of these.</summary>
    public static long TextStrideBytes;

    public static long SegScissorBytes;
    public static long SegBindBytes;
    public static long SegDrawBytes;
    public static int SegCount;

    /// <summary>The structural PLAN on its own, apart from the dirty-set pre-validation the same timer used to cover, and
    /// how many structural marks it had to place. A tile drag churns containers (park/unpark/rebind), so this is the
    /// cost of deciding WHERE things go rather than of drawing them.</summary>
    public static double LastRecordPlanOnlyMs;
    public static int LastRecordStructuralMarks;

    /// <summary>What the placement actually TOUCHED. A frame that placed TWO structural marks and spent 77ms doing it is
    /// not paying per mark, so the question is how many sibling lists it walked - the same "runs x children" shape a
    /// neighbour-map rewrite already fixed once in this file. Scans is the sum of every child list re-read.</summary>
    public static long LastRecordPlanScans;

    /// <summary>...and WHICH of the three sibling-list readers did it. One counter said a million children were re-read
    /// to place 678; three say which helper to fix.</summary>
    public static long ScansSuccessor;    // SuccessorRank - walks UP, re-reading every ancestor level's children
    public static long ScansLastRank;     // TryLastRankOfSubtree - walks DOWN the previous sibling's subtree
    public static long ScansCollect;      // CollectSubtreeInPaintOrder - the placed subtree itself
    public static long ScansParent;       // the one list PlanNewChildren legitimately needs

    /// <summary>TEMP: how often the layout-snapshot sweep ran, and how many entries it dropped. A sweep written but never
    /// reached looks exactly like a sweep that finds nothing.</summary>
    public static int SnapSweeps, SnapSwept;
    public static int LastRecordPlanRuns;
    public static int LastRecordPlanParents;
    public static double LastRecordRenumberMs;
    public static int LastApplyInserts;
    public static int LastApplyGroups;

    /// <summary>What the apply actually did this frame: units built from scratch, units updated in place, and the draw
    /// commands it consumed. A structural frame that CREATES its units costs a different thing from one that updates them,
    /// and the totals alone cannot tell the two apart.</summary>
    /// <summary>WHY a unit was created: the group GREW (a component that draws more than it did, or a new one), or the
    /// existing unit did not MATCH the command and had to be replaced. The first is work the scene asked for; the second
    /// is churn, and only a count says which of them ~900 creations a second are. Plus the time each half costs -
    /// creating a unit builds GPU buffers, updating one writes into buffers that exist.</summary>
    /// <summary>WHICH units get created, and for whom. Creating one costs 0.7us during a tab build and 62-90us in a
    /// second with almost nothing attaching - the same operation, a hundred times apart - and ~910 of the expensive kind
    /// are built every second on a settled scene. Only a histogram says what they are. Written on the RENDER thread, so
    /// it shares HistogramLock (a torn Dictionary crashes its reader - it already did once today).</summary>
    public static readonly System.Collections.Generic.Dictionary<string, (int Count, double Ms)> UnitsCreatedByKind = new();

    public static void NoteUnitCreated(string kind, double ms)
    {
        lock (HistogramLock)
        {
            UnitsCreatedByKind.TryGetValue(kind, out var e);
            UnitsCreatedByKind[kind] = (e.Count + 1, e.Ms + ms);
        }
    }

    public static long UnitsCreatedGrow;
    public static long UnitsCreatedMismatch;
    public static double UnitCreateMs;
    public static double UnitUpdateMs;

    public static long UnitsCreated;
    public static long UnitsUpdated;
    public static long CommandsApplied;

    public static double LastRenderProcMs;
    /// <summary>RenderCache.Render - the content draw pass (batch + command recording).</summary>
    public static double LastRenderDrawMs;

    /// <summary>TEMP: the out-of-pass PreRender sweep - it visits every unit of every group on EVERY frame, so whether
    /// that matters is a question a number answers, not a guess.</summary>
    public static double LastPreRenderMs;
    /// <summary>Overlay stages (adorner + popup) draw.</summary>
    public static double LastProcessorsMs;

    /// <summary>The four steps of a frame that are NOT recording or drawing content, and which together were most of a
    /// frame while nobody was looking: BeginDraw (its fence wait is where a frame waits for the GPU to be done with the
    /// slot, plus the acquire), EndDraw (finalize + blit to the swapchain image), Submit (hand the queue the work), and
    /// Present. Named separately because they have four different fixes and only one of them is "the GPU is busy".</summary>
    public static double LastBeginDrawMs;
    public static double LastEndDrawMs;
    public static double LastSubmitMs;
    public static double LastPresentMs;

    /// <summary>Cumulative count of frames actually PRESENTED. With a dedicated render thread this is the only honest frame
    /// rate: the loop's own rate measures Update + record, and the two are deliberately decoupled - a heavy Update must not
    /// drag the presented frame rate down with it (that is the entire point of the split). Sample by delta.</summary>
    public static long PresentedFrames;

    /// <summary>Cumulative count of binding target writes - every time a <c>{Binding}</c> pushes a value to its target:
    /// the initial connect, a DataContext re-resolve (e.g. a recycled list container rebinding on scroll), AND a batched
    /// source-property change. Sample by delta to see how many landed this frame (idle ~0; spikes on scroll rebinds and
    /// on a binding storm, where the per-flush cap bounds it).</summary>
    public static long BindingUpdatesApplied;
}
