using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// One geometry KEY of the instanced fill, seen as a retained arena (<see cref="BatchArena"/>) - so a control drawing a
/// vector shape is repaired the way every other batched family is, instead of costing the frame a walk of the window.
/// <para>The fit is exact once named: a key's instances are one append-only array, and a flush record keeps, per key,
/// a contiguous <c>(first, count)</c> of it under ONE clip. That IS a segment. The arena is the KEY rather than the
/// collector because a slot number only means something inside its own array, and one collector holds one array per
/// shape; the segment ID is the FLUSH index, because a key appears in a record at most once, so the pair is unique.</para>
/// <para>SOLID fills only. A key carries four parallel instance families (solid, gradient, pattern, textured) and the
/// other three answer "not mine" - the same way a family with nothing to stage answers today, and visible by name in
/// the refusal rather than as a silent walk.</para>
/// </summary>
internal sealed class InstancedKeyArena : BatchArena
{
    private readonly InstancedFillCollector _collector;
    private readonly object _key;               // the KeySegment this arena speaks for (typed inside the collector)
    private readonly List<GeometryInstance> _stage = new();
    // The units staged alongside them. A flush draws its key runs AND the deferred fringe/stroke of the units collected
    // with them - a cross is a STROKED path, so a record without its units puts up the shape and none of the ink.
    private readonly List<IRenderUnit> _stagedUnits = new();

    public InstancedKeyArena(InstancedFillCollector collector, object key)
    {
        _collector = collector;
        _key = key;
        BatchId = InstancedFillCollector.ArenaBatchId;
    }

    /// <summary>A recorded instanced draw is an <see cref="RenderOpKind.InstancedFlush"/>, not a Segment op - the flush
    /// draws every key of one clip together under one coverage mark, which is the whole reason it exists.</summary>
    public override RenderOpKind OpKind => RenderOpKind.InstancedFlush;

    public override bool MatchesOp(RenderOpKind kind, int segId, int mySegment) =>
        kind == RenderOpKind.InstancedFlush && segId == mySegment;

    public override int RetainedCount => _collector.KeyCount(_key);

    public override int PatchCapacityLeft => _collector.KeyCapacityLeft(_key);

    public override bool HasSegment(int id) => _collector.KeyRange(_key, id).Count >= 0;

    public override int SegmentIdAt(int index) => -1;   // records are addressed by their own index, never by position

    public override int FindSegmentContaining(int slot) => _collector.KeyFlushContaining(_key, slot);

    public override (int First, int Count) SegmentRange(int id) => _collector.KeyRange(_key, id);

    public override Rect2D GetSegmentScissor(int id) => _collector.FlushScissor(id);

    public override void SetSegmentScissor(int id, Rect2D scissor) => _collector.SetFlushScissor(id, scissor);

    /// <summary>No footprint is kept per flush record, so a placement cannot prove it misses this one and cuts instead of
    /// joining. A cut is refused below anyway, so the honest answer is "unknown" rather than a guess in either direction.</summary>
    public override Rect SegmentBounds(int id) => Rect.Empty;

    public override void GrowSegmentBounds(int id, Rect bounds) { }

    /// <summary>A flush cannot be cut in two: its coverage mark is what makes "all fills, then all fringes" equal true
    /// paint order, and two halves would stamp two marks over one group's shapes.</summary>
    public override int SplitSegment(int id, int firstOfSecond) => -1;

    public override string DescribeSegment(int id)
    {
        var (first, count) = SegmentRange(id);
        return $"instanced key flush {id} [{first}..{first + count})";
    }

    public override void BlankSlots(IGraphicsDevice device, int first, int count)
        => _collector.BlankKeySlots(_key, first, count);

    public override void DropOverlayOf(IUIComponent component) => _collector.DropUnitsOf(component);

    public override void ClearStage()
    {
        _stage.Clear();
        _stagedUnits.Clear();
    }

    public override int StagedCount => _stage.Count;

    public override bool TryStage(IRenderUnit unit, Matrix4x4F world, int transformSlot, int ownerTag, int clipSlot = -1)
    {
        if (!_collector.TryStageSolid(_key, unit, world, transformSlot, _stage, clipSlot)) return false;

        _stagedUnits.Add(unit);
        return true;
    }

    public override bool ReplaceStagedInSegment(IGraphicsDevice device, int id, int at, int replaced, int stageFirst, int stageCount)
        => _collector.ReplaceInKey(_key, id, at, replaced, _stage, stageFirst, stageCount, _stagedUnits);

    /// <summary>Relocation has no meaning here: a key's array is its own, nothing else is packed after it, so an edit
    /// that does not fit in place is an edit the arena has no room for at all.</summary>
    public override bool RepointSegmentAroundStage(IGraphicsDevice device, int id, int first, int at, int replaced, int count,
        Rect2D scissor, int stageFirst, int stageCount) => false;

    public override int AllocateSegmentFromStage(IGraphicsDevice device, Rect2D scissor, int stageFirst, int stageCount)
        => _collector.AllocateFlushForKey(_key, scissor, _stage, stageFirst, stageCount, _stagedUnits);

    public override void UpdateSlotFromStage(IGraphicsDevice device, int slot, int stageIndex)
        => _collector.UpdateKeySlot(_key, slot, _stage[stageIndex]);
}
