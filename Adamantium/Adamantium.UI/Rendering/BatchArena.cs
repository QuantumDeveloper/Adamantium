using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.Vulkan.Core;



namespace Adamantium.UI.Rendering;

/// <summary>
/// What a retained batch is, said WITHOUT its item type. The patch paths repair a frame segment by segment, and every
/// batched family - rectangles, ellipses, their gradient forms, glyphs - is drawn from a segment in exactly the same
/// way. Only the bytes differ, and those never leave the collector: a patch STAGES its items inside the arena that will
/// hold them and then names the staged range, so nothing generic has to travel through the caller.
/// <para>Without this the repair could only be written against ONE closed type, and it was: a control that drew an
/// ellipse where a rectangle was expected cost a walk of the whole window, for no reason but the type parameter.</para>
/// </summary>
internal abstract class BatchArena
{
    /// <summary>Which <see cref="RenderOp.Batch"/> draws this arena - how a recorded op is matched back to it. Set per
    /// INSTANCE, not per type: the halo family runs two arenas of one type, one drawn under its shapes and one over.</summary>
    public byte BatchId { get; set; }

    /// <summary>Does a recorded op draw THIS arena segment? Almost every family is a Segment op named by its id; the
    /// instanced fill is drawn by a flush op instead, because a flush covers every key of one clip under one coverage
    /// mark. Asked of the arena rather than assumed by the caller.</summary>
    public virtual bool MatchesOp(RenderOpKind kind, int segId, int mySegment) =>
        kind == RenderOpKind.Segment && segId == mySegment;

    /// <summary>The op kind that draws this arena - a Segment for every batched family, a flush for the instanced fill.</summary>
    public virtual RenderOpKind OpKind => RenderOpKind.Segment;

    /// <summary>Retained slot count (the next staged append starts here).</summary>
    public abstract int RetainedCount { get; }

    public abstract int PatchCapacityLeft { get; }

    public abstract bool HasSegment(int id);

    public abstract int SegmentIdAt(int index);

    public abstract int FindSegmentContaining(int slot);

    /// <summary>The retained range [first, first+count) a recorded segment currently draws.</summary>
    public abstract (int First, int Count) SegmentRange(int id);

    public abstract Rect2D GetSegmentScissor(int id);

    public abstract Rect SegmentBounds(int id);

    public abstract void GrowSegmentBounds(int id, Rect bounds);

    public abstract int SplitSegment(int id, int firstOfSecond);

    public abstract string DescribeSegment(int id);

    /// <summary>Make a run of retained slots draw NOTHING, leaving the run where it is. What a control that stopped
    /// drawing needs: excising its slots costs its segment its shape and hands it a fresh one when it comes back - an op
    /// per hide/show, which only a full walk ever compacts. Blanked in place, it keeps its segment the way it already
    /// keeps its RANK, and coming back is an edit rather than a new place in the stream.</summary>
    public abstract void BlankSlots(IGraphicsDevice device, int first, int count);

    // ---- staging -----------------------------------------------------------------------------------------------
    // A patch validates the WHOLE frame before it changes anything, so baking and mutating are two phases. The baked
    // items wait here, in the arena that will hold them; a patch owns the range [first, first+count) it appended.

    /// <summary>Drop everything staged - once per patch, before its groups bake.</summary>
    public abstract void ClearStage();

    /// <summary>Where the next staged item lands, so a group can name the range it is about to append.</summary>
    public abstract int StagedCount { get; }

    /// <summary>Bake one unit into the stage. False = this family cannot hold it (wrong unit, rotated, gradient it does
    /// not draw) and the patch must refuse - which is what a family with nothing to stage answers to everything.</summary>
    public abstract bool TryStage(IRenderUnit unit, Matrix4x4F world, int transformSlot);

    /// <summary>Replace [at, at+replaced) inside a segment with a staged range, shifting only what follows. False when the
    /// result outgrows the room that segment owns; the caller then re-points it whole.</summary>
    public abstract bool ReplaceStagedInSegment(IGraphicsDevice device, int id, int at, int replaced, int stageFirst, int stageCount);

    /// <summary>Re-point a whole segment at [head of the old range | staged items | tail of the old range] - the relocate
    /// path, for when the edit no longer fits in place.</summary>
    public abstract bool RepointSegmentAroundStage(IGraphicsDevice device, int id, int first, int at, int replaced, int count,
        Rect2D scissor, int stageFirst, int stageCount);

    /// <summary>Rewrite ONE retained slot from a staged item - the count-stable repair, where nothing moves and only the
    /// bytes of an instance change.</summary>
    public abstract void UpdateSlotFromStage(IGraphicsDevice device, int slot, int stageIndex);

    /// <summary>Give a staged range a segment of its OWN - a control that drew nothing here until now. Returns the new
    /// segment's index, or -1 when the arena has no room.</summary>
    public abstract int AllocateSegmentFromStage(IGraphicsDevice device, Rect2D scissor, int stageFirst, int stageCount);
}
