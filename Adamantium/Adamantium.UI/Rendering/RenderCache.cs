using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.UI.Rendering.Retained;
using Adamantium.UI.Rendering.RenderUnits;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

public partial class RenderCache
{
    private readonly List<DrawCommand> _commands = new();
    private IDrawingContext _drawingContext;
    private IDrawingContextInternal _drawingContextInternal;

    // One control's contiguous run in the paint order. A control's change - even its unit COUNT (hover bg 0->1, a chart
    // re-recording a different segment count) - mutates only ITS group's Units; that makes a dirty control O(itself).
    private sealed class ControlGroup
    {
        public Guid ControlId;

        // The control this group draws. Needed BEFORE its units are walked (a clone host may hold no units of its own
        // while its children do), so it cannot be read off Units[0].
        public IUIComponent Component;

        // The clone set as it was RECORDED with this group's contribution (§4o) - never read live off the component at
        // draw time, which is layout's to write while the render thread draws.
        public IReadOnlyList<Matrix4x4F> Clones;

        // Paint rank - _groups is kept sorted by it. Carried on the group (not a shared map) so the applier can place a
        // spliced group by comparison.
        public long Order;

        // In the paint order (_groups) right now? A hidden control keeps its group + units but leaves the order.
        public bool InOrder;

        // Written INTO every rect instance this group bakes, so the arena can always say whose a slot is - see
        // RectBatchCollector.TryAdd. Small and dense (an int per group, never reused within a frame).
        public int Tag;

        public readonly List<IRenderUnit> Units = new();

        // Per-group rect-batch slot runs + whether EVERY drawn unit is rect-batched, for the spliced-patch draw path;
        // anything else falls back to the full walk.
        public readonly List<(int First, int Count)> RectRuns = new();
        public bool PatchableRectOnly;
        public int WalkVersion = -1;   // which recording walk last described this group (distinguishes NEW groups)
    }

    private int _walkVersion;   // bumped per recording walk

    private ControlGroup _walkGroup;   // recording-walk group-boundary detector (resets each group's run records once)

    private readonly List<ControlGroup> _groups = new();              // the paint order (groups in DFS paint rank)
    private readonly Dictionary<Guid, ControlGroup> _groupById = new();   // control -> its group (the retained unit cache)

    // Paint rank of each drawn component (sort key of _groups). SPARSE (0, GAP, 2*GAP...) so an added tile takes a rank
    // strictly BETWEEN its neighbours' with nobody renumbered -> a structural change is O(changed), not O(scene).
    // RECORDER-owned; the applier never reads it (a changed rank travels ON the packet).
    private readonly Dictionary<Guid, long> _orderByControl = new();

    // ~30 inserts into one gap before it is used up (each halves it); an exhausted gap costs a full-walk renumber. The
    // 63-bit space is ample, so be generous.
    private const long OrderGap = 1L << 30;
    private IRootVisualComponent _lastVisualRoot;             // the tree these ranks describe
    private readonly Stack<IUIComponent> _walkStack = new();      // reused by RecordFullWalk (it ran on every structural frame)
    private readonly HashSet<IUIComponent> _walkVisited = new();
    private bool _forceFullNextFrame;                         // the applier hit a state only a full RECORD can fix (see ApplyPacket)

    // Recorder-side mirror of which components hold units - from the recorder's OWN decisions, NOT the applier's
    // _groupById (which the render thread mutates while the loop records -> reading it here is a data race). Lets the
    // recorder decide Skip vs Fallback for a no-longer-drawn component.
    private readonly Dictionary<Guid, (IUIComponent Component, int Units)> _recordedUnits = new();
    private readonly List<Guid> _staleUnitIds = new();   // scratch: detached controls to drop from the mirror

    // Mirror one recorded draw: >0 commands -> cache these units; 0 from a DIRTY control -> it now draws nothing, drop it.
    private void MirrorUnits(IUIComponent component, int commandCount, bool wasGeometryValid)
    {
        if (commandCount > 0) _recordedUnits[component.RenderId] = (component, commandCount);
        else if (!wasGeometryValid) _recordedUnits.Remove(component.RenderId);
    }

    private bool HoldsUnits(IUIComponent component) =>
        _recordedUnits.TryGetValue(component.RenderId, out var entry) && entry.Units > 0;

    // The detach generation this cache has already reconciled (see ApplyPacket).
    private long _reconciledDetachGen;

    private readonly IRenderUnitFactory _renderUnitFactory;

    // Reusable snapshot of the geometry-dirty set: a component's Render can mark MORE geometry mid-pass, so we can't
    // enumerate the live set directly.
    private readonly List<IUIComponent> _geometryDirtyBuffer = new();

    // Paint-dirty components: never re-rendered - they ride the packet's patch list so the draw pass re-bakes the units
    // they already have (see RenderDirty.MarkPaint).
    private readonly List<IUIComponent> _paintDirtyBuffer = new();

    // Paint-dirty snapshot for CaptureSnapshot: element Opacity lives in the snapshot, so an opacity change must re-freeze.
    private readonly List<IUIComponent> _opacityCheckBuf = new();

    // Draw-phase partial replay: a geometry-only partial patches only the dirty tiles' rect slots (UpdateSlot) and
    // REPLAYS the op stream - O(dirty). Any doubt falls back to the full walk, so replay is a pure speedup.
    private readonly List<IUIComponent> _partialDirty = new();               // dirty components of the last fast-path partial
    private readonly Dictionary<IRenderUnit, int> _rectSlotByUnit = new();   // batched rect unit -> its slot in _rectBatch

    // Same slot map for the other SDF batches, kept separate from the rect map (which the SPLICE path renumbers) - these
    // only serve the paint fast-path. Without them a paint-only change to a non-rect (spinning ring, pulsing ellipse)
    // fell through to a full tree walk every tick.
    private enum SdfSlotKind { Ellipse, GradientRect, GradientEllipse }
    private readonly Dictionary<IRenderUnit, (SdfSlotKind Kind, int Slot)> _sdfSlotByUnit = new();

    // A text block owns a RUN of glyph slots, not one - the same map widened to a range, plus the atlas its recorded
    // segment binds. Lets a block whose glyph COUNT and atlas are unchanged (a counter ticking over, an FPS readout) be
    // re-baked in place. Without it ANY dirty text refused the frame's patch and cost a full walk of the scene.
    private readonly Dictionary<IRenderUnit, (int First, int Count, Graphics.Fonts.FontAtlas Atlas)> _textRunByUnit = new();

    // The units each LIVE brush paints, built on the recording walk; the render thread re-bakes them all when a
    // composited PAINT animation changes the brush snapshot. Keyed by live brush by REFERENCE; one brush is shared by
    // hundreds of elements (the skeleton pulse) - the reason paint fans out where transform does not.
    private readonly Dictionary<Core.Media.Brush, List<IRenderUnit>> _unitsByBrush = new();

    private void IndexUnitBrush(IUIComponent _, IRenderUnit unit, Core.Media.Brush liveBrush)
    {
        if (liveBrush == null) return;
        if (!_unitsByBrush.TryGetValue(liveBrush, out var list))
            _unitsByBrush[liveBrush] = list = new List<IRenderUnit>();
        list.Add(unit);
    }
    private bool _partialSpliced;                                            // last partial mutated the paint-order list -> ops/slots stale

    // Last render scale seen in ProcessCommands; maps a unit's window-logical clip rect to framebuffer-pixel scissor.
    private double _renderScale = 1.0;

    private readonly List<IUIComponent> _structuralBuf = new();      // components that entered/left the drawn set this frame
    private readonly List<IUIComponent> _movedNodesCapture = new();  // moved motion nodes, for the snapshot re-freeze on a FULL frame
    private readonly List<IUIComponent> _partialConsumed = new();    // rendered by a partial pass that may yet be discarded

    // Structural-record (splice) scratch - the marks name what entered/left; new subtrees get a rank between neighbours' (RecordStructuralFrame):
    private readonly HashSet<IUIComponent> _placeRoots = new();       // outermost unranked (=new) drawn components to place
    private readonly HashSet<IUIComponent> _structRecorded = new();   // recorded by THIS structural pass (the dirty pass skips them)
    private readonly Dictionary<IUIComponent, List<IUIComponent>> _addsByParent = new();
    private readonly List<IUIComponent> _childBuf = new();            // one parent's children, in paint order
    private readonly List<IUIComponent> _subtreeBuf = new();          // one added subtree, in paint order
    private readonly Stack<IUIComponent> _subtreeStack = new();
    private readonly Dictionary<IUIComponent, long> _plannedRanks = new();   // ranks THIS plan assigned, not yet committed to _orderByControl

    public RenderCache(IDrawingContext context, IRenderUnitFactory renderUnitFactory)
    {
        _drawingContext = context;
        _drawingContextInternal = (IDrawingContextInternal)context;
        _renderUnitFactory = renderUnitFactory;
    }

    /// <summary>How much the most recent build did - diagnostics + the "skip proc" decision (only a Clean frame skips the
    /// transform re-bake).</summary>
    public RenderBuildKind LastBuildKind { get; private set; }

    /// <summary>The retained PAINT ORDER (controls holding units, in draw sequence). Tests hold it against a full walk of
    /// the same tree - drawing in the wrong order is drawing the wrong picture.</summary>
    internal IReadOnlyList<Guid> PaintOrder => _groups.Select(g => g.ControlId).ToArray();

    /// <summary>The draw pass's frozen replica of each component's layout. A stale entry draws a tile at its LAST size or
    /// place - exactly what gaps and overlaps are; tests assert it agrees with the live tree.</summary>
    internal IReadOnlyDictionary<IUIComponent, LayoutSnapshot> AppliedSnapshot => _applySnap;

    /// <summary>The components the draw pass will actually draw (those holding retained units).</summary>
    internal IEnumerable<IUIComponent> DrawnComponents =>
        _groups.SelectMany(g => g.Units).Select(u => u.Component).Distinct();

    /// <summary>Did the last build MOVE anything (transform-dirty / a full re-layout)? A geometry-only partial moved
    /// nothing, so the per-unit transform re-bake (proc) is redundant and skipped.</summary>
    public bool LastBuildTransformDirty { get; private set; }

    private bool _built;

    /// <summary>Brings the retained render scene up to date, doing only as much work as changed: clean -> re-draw last
    /// frame's units; non-structural -> re-render just the dirty components in place; structural / first build -> a full
    /// tree walk. (docs/RENDER_CACHE_REDESIGN.md §4a/§4i)</summary>
    public void BuildFromVisualTree(IRootVisualComponent visualRoot)
    {
        RecordFrame(visualRoot);
        ApplyFrame();
    }

    /// <summary>RECORD half of the frame build (DEVICE-FREE, update thread): decides the build kind, runs
    /// component.Render, and fills <see cref="_packet"/> with the draws the applier realizes. Captures the
    /// RenderDirty-derived draw state WHILE RenderDirty is still populated (the applier and the loop-level clear run
    /// later).</summary>
    public void RecordFrame(IRootVisualComponent visualRoot)
    {
        _packet = RentPacket();
        RecordFrameCore(visualRoot);
        // Freeze the snapshot at the END of the device-free record so the applier reads only the frozen packet, never a
        // live component.
        CaptureSnapshot();
        _published.Enqueue(_packet);   // hand it over; the applier drains the queue (see ApplyFrame)
        _packet = null;
    }


    private void RecordFrameCore(IRootVisualComponent visualRoot)
    {
        _movedBuf.Clear();   // filled only by a PARTIAL (a Full re-freezes everything from its packet anyway)
        _packet.Reset(RenderBuildKind.Clean);
        // LIVE read of the root - taken here (recorder thread), travels ON the packet.
        _packet.ProjectionMatrix = visualRoot.GetProjectionMatrix();

        // Fully clean: re-draw the retained units as-is. (Unless a prior frame deferred a full walk - pay that first.)
        if (_built && !RenderDirty.HasWork && !_forceFullNextFrame) return;

        // Non-structural change -> PARTIAL: re-render only the geometry-dirty components in place; the retained groups
        // stay. Drop world/clip memos ONLY on a MOVE (transform-dirty) - a geometry-only partial (a hover recolour) moved
        // nothing, so cached transforms stay valid (skipping the O(N) re-bake of thousands of units). An UNNAMEABLE move
        // (an unowned Transform ticking) can't tell which snapshot entries went stale -> escalate to a full record.
        if (_built && !RenderDirty.IsStructural && !RenderDirty.IsTransformUnknown && !_forceFullNextFrame)
        {
            _packet.Kind = RenderBuildKind.Partial;
            if (RenderDirty.IsTransform)
            {
                // Snapshot is re-frozen incrementally (CaptureSnapshot re-freezes only the moved entries); the applier
                // drops the world memos.
                _packet.ClearMemos = true;   // APPLIER-owned - it drops them itself (see RenderPacket.ClearMemos)
            }

            // Snapshot the dirty set under RenderDirty's write lock: Render can mark MORE geometry mid-loop, and marks
            // also arrive from other threads (parallel arrange, a Dispatcher-thread Image invalidation).
            RenderDirty.SnapshotGeometryInto(_geometryDirtyBuffer);

            // Record each dirty component; a Fallback stops the pass.
            var fellBack = false;
            _partialConsumed.Clear();
            foreach (var component in _geometryDirtyBuffer)
            {
                var recorded = RecordReRender(component, _packet);
                if (recorded == PartialRecord.Recorded) _partialConsumed.Add(component);
                if (recorded == PartialRecord.Fallback) { fellBack = true; break; }
            }

            // Partial stands ONLY if nothing structural surfaced and the dirty set didn't grow (a Render re-marking
            // geometry escalates to a full walk so that change isn't dropped).
            if (!fellBack && !RenderDirty.IsStructural && RenderDirty.GeometryCount == _geometryDirtyBuffer.Count)
            {
                // Geometry-only partial -> nothing moved -> the applier can skip the per-unit transform re-bake (proc).
                _packet.IsTransformDirty = RenderDirty.IsTransform;
                // What changed (draw phase patches these slots + replays) and which motion nodes moved. Read HERE, while
                // RenderDirty is still populated (it is cleared after the record phase; the applier must never touch it).
                _packet.PartialDirty.AddRange(_geometryDirtyBuffer);

                // Paint-dirty components ride the SAME list: not re-rendered, their commands hold the brush BY REFERENCE,
                // so the draw pass's slot patch re-bakes from the (now different) brush - a recolour costs a re-bake, not
                // a re-record.
                RenderDirty.SnapshotPaintInto(_paintDirtyBuffer);
                _packet.PartialDirty.AddRange(_paintDirtyBuffer);

                RenderDirty.SnapshotNodesInto(_packet.MovedNodes);   // under the write lock (MarkNodeTransform runs on other threads too)
                RenderDirty.SnapshotMovedInto(_movedBuf);            // the MOVED components - CaptureSnapshot re-freezes just these

                return;   // packet.Kind == Partial -> ApplyFrame runs the partial apply
            }
            // a structural change or a new invalidation surfaced during the partial pass -> escalate below

            // ...and everything this pass recorded is DISCARDED by the escalated record (it Resets the packet). But
            // RecordReRender already ran component.Render, which set IsGeometryValid back to true - so the full walk would
            // read "was clean", get no commands, and REUSE the units describing the component's PREVIOUS appearance
            // (a de-selected row keeping its highlight through a fast virtualized scroll, forever). Give the flag back:
            // the pass that supersedes this one must re-record them for real.
            foreach (var component in _partialConsumed)
            {
                component.InvalidateRender(false);
            }
        }

        // STRUCTURAL: content entered/left the drawn set; every change NAMES its component, so the paint order can be
        // spliced (O(changed)) instead of re-walking the whole tree. Refuses anything it can't place locally -> the full
        // walk below is the self-healing fallback (it renumbers with fresh gaps).
        if (_built && !RenderDirty.IsStructuralUnknown && !RenderDirty.IsTransformUnknown && !_forceFullNextFrame)
        {
            _packet.Reset(RenderBuildKind.Structural);
            _packet.ProjectionMatrix = visualRoot.GetProjectionMatrix();
            if (RecordStructuralFrame(visualRoot, _packet)) return;
        }

        RecordFullFrame(visualRoot);
    }

    // RECORD a full walk (DEVICE-FREE): first build, a structural change, or a partial that surfaced one. Fills the packet.
    private void RecordFullFrame(IRootVisualComponent visualRoot)
    {
        _commands.Clear();
        _forceFullNextFrame = false;      // this IS that full walk

        var projection = _packet.ProjectionMatrix;
        _packet.Reset(RenderBuildKind.Full);   // a full walk SUPERSEDES anything a partial had already recorded into it
        _packet.ProjectionMatrix = projection;
        _packet.IsTransformDirty = true;       // the paint order is rebuilt; every position must be re-baked
        _packet.ClearMemos = true;             // applier-owned - it drops them itself (see RenderPacket.ClearMemos)

        // A full walk rebuilds the PAINT ORDER, but the snapshot is a per-component freeze of transform/size/clip - so
        // keep it and re-freeze only what the marks NAME (geometry / move / motion node / structural). Anything the marks
        // CANNOT name (an unattributed move, a settling theme/DPI swap) drops the whole snapshot.
        var reFreezeEverything = RenderDirty.IsStructuralUnknown || RenderDirty.IsTransformUnknown;
        if (reFreezeEverything)
        {
            _snap.Clear();
            _packet.SnapReset = true;   // ...and so must the applier's replica
        }
        _snapFullCapture = false;

        // What changed, for CaptureSnapshot to re-freeze. Read HERE, while RenderDirty is still populated.
        RenderDirty.SnapshotGeometryInto(_geometryDirtyBuffer);
        RenderDirty.SnapshotStructuralInto(_structuralBuf);
        RenderDirty.SnapshotMovedInto(_movedBuf);
        RenderDirty.SnapshotNodesInto(_movedNodesCapture);

        RecordFullWalk(visualRoot, _packet);   // device-free: walk + component.Render + copy commands into the packet
    }

    private bool HasRank(IUIComponent component) => _orderByControl.ContainsKey(component.RenderId);
    private long RankOf(IUIComponent component) => _orderByControl[component.RenderId];

    // The rank a component has, OR the one THIS plan is about to give it: a panel's later run of new tiles must rank
    // against tiles an EARLIER run of the same plan just planned (not yet in _orderByControl). Without this the frame
    // falls back to re-recording the whole tree.
    private bool TryPlannedRank(IUIComponent component, out long rank) =>
        _plannedRanks.TryGetValue(component, out rank) || _orderByControl.TryGetValue(component.RenderId, out rank);

    private bool HasPlannedRank(IUIComponent component) => TryPlannedRank(component, out _);

    // DRAWN right now: attached, visible, no hidden ancestor, and rooted in OUR tree (a popup/tooltip lives in a foreign
    // root drawn by its own cache).
    private bool IsDrawn(IUIComponent component)
    {
        if (component.Visibility != Visibility.Visible || !component.IsAttachedToVisualTree) return false;
        var c = component;
        while (c.VisualParent != null)
        {
            c = c.VisualParent;
            if (c.Visibility != Visibility.Visible) return false;
        }
        return ReferenceEquals(c, _lastVisualRoot);
    }

}
