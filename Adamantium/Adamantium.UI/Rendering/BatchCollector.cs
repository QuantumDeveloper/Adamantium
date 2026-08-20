using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Shared machinery for the CPU-baked instanced UI batches (text glyphs, item-background rects; see
// docs/TEXT_GLYPH_BATCH_PLAN.md §9). Holds a growable CPU array + one growable GPU buffer - a BDA STORAGE buffer read
// in the vertex shader by SV_InstanceID (the quad comes from SV_VertexID) - the glyph batch included. Filled APPEND-ONLY
// within a frame and drawn as SEGMENTS - a segment is a run of items sharing one clip (scissor), drawn with a
// firstInstance offset so a mid-frame flush never overwrites an earlier segment's still-recorded draw. The GPU buffer
// only grows at BeginFrame (a safe point: the render runs after the frame fence, so last frame's reads are done). The
// paint-order union bounds let the caller flush before a non-batched unit that overlaps the pending segment.
//
// Derived types add: item baking (their own TryAdd, which writes into Items/Count then calls MarkPending) and the
// per-segment draw (DrawSegment). Grouping (which items share a segment) is decided by the caller (RenderCache).
internal abstract class BatchCollector<TItem> where TItem : struct
{
    // Size of ONE instance. Static readonly, so it is computed once per closed type - a Marshal.SizeOf per DRAW showed up
    // as microseconds a call in the replay breakdown, and a replayed frame issues dozens of them.
    protected static readonly int Stride = Marshal.SizeOf<TItem>();

    protected TItem[] Items;
    protected int Count;               // items written this frame (across all segments, monotonic within a frame)
    private int _segmentStart;         // start of the pending (not-yet-flushed) segment
    private Rect2D _scissor;           // the pending segment's clip
    private double _uL, _uT, _uR, _uB; // logical union of the pending segment (paint-order overlap test)
    private bool _hasUnion;

    // ONE COPY PER FRAME IN FLIGHT. The pipeline is MaxFramesInFlight deep, so BeginDraw's fence proves only frame
    // N-MaxFramesInFlight is done: the frames between it and this one are still reading. Writing a single shared buffer
    // therefore rewrote instances under the frames drawing them - which is what made a fast scroll flicker across the
    // WHOLE window, still parts included (slot indices come from draw order, so an item leaving the viewport shifts every
    // later slot). The copy is chosen by the device's frame index, so the one written is the one whose last reader has
    // already been waited for. Same scheme as ReusableBuffer, which is why per-unit geometry never had this problem.
    private Buffer<TItem>[] _ring;
    private int _current;              // ring slot this frame writes and draws from
    private uint _writeFrame = uint.MaxValue;   // device frame the slot was last chosen for
    private int _gpuCapacity;

    // Per copy: the bytes that copy currently holds, and how many of them are valid. The upload diffs the freshly baked
    // items against THIS copy (not against last frame), so only what that copy is actually missing is sent - a still
    // scene converges to zero bytes within a lap of the ring, and a scroll sends the span that moved.
    private TItem[][] _mirror;
    private int[] _mirrorCount;

    // Segments drawn THIS frame (each = a clip + a buffer range), retained for the clean-frame op replay. RenderCache
    // records an ordered op stream during the walk; on a fully-unchanged (Clean) frame it skips the walk entirely and
    // replays each segment via DrawRecordedSegment - no re-bake, no upload (the GPU buffer still holds these exact
    // bytes). Cleared at BeginFrame; a segment's index is stable within the frame that recorded it.
    // Capacity is the ROOM a range owns, Count what it currently draws - so a layer re-issued one item larger fits where
    // it already is instead of moving, and blocks freed by one edit are the right size for the next.
    // Id is how EVERYONE outside names a segment. The list stays ordered by draw order, so a split has to insert into the
    // middle of it - and an index taken before that insert means a different segment after it. That shift used to be the
    // caller's problem, fixed up in three places at once (the op stream, the pending patches, their resolved layers), and
    // a missed one re-issued the wrong segment: the same picture as a slot-off-by-one, with nothing in the frame to explain
    // it. An id survives the insert, so there is nothing to fix up; ids are never reused, so a reference held across a
    // frame resolves to NOTHING instead of to whatever moved into that index.
    // Bounds are the segment's PAINT-ORDER footprint - the union of what it draws, in logical coordinates, kept from the
    // pending union it was flushed from. The walk needs it live (which is what the pending union is for); a RECORDED segment
    // needs it too, because a newcomer placed into an existing frame can only be told "your order inside this layer does not
    // matter" by asking whether it overlaps what the layer already draws (see §5a: that is the merge rule, and the only
    // reason a cut is ever needed).
    protected struct Segment
    {
        public int Id; public Rect2D Scissor; public uint Count; public uint First; public uint Capacity;
        public double L, T, R, B; public bool HasBounds;
    }
    private readonly List<Segment> _segments = new();
    private readonly Dictionary<int, int> _indexById = new();
    private int _nextSegmentId;

    /// <summary>Where a segment currently sits in draw order, or -1 if this id is not part of the recorded frame.</summary>
    private int IndexOf(int id) => _indexById.TryGetValue(id, out var index) ? index : -1;

    /// <summary>The id of the segment sitting at this position in draw order - the way back from a position (which is
    /// what a slot search answers) to the NAME everything outside uses.</summary>
    public int SegmentIdAt(int index) => index >= 0 && index < _segments.Count ? _segments[index].Id : -1;

    /// <summary>Is this id still part of the recorded frame?</summary>
    public bool HasSegment(int id) => _indexById.ContainsKey(id);

    // Re-point the id map from that index to the end of the list. Called after an insert, which is the only thing that moves a
    // segment's index.
    private void Reindex(int from)
    {
        for (var i = from; i < _segments.Count; i++) _indexById[_segments[i].Id] = i;
    }

    // Ranges vacated by a re-issued layer, handed back so the next re-issue reuses them instead of growing the arena.
    private readonly List<(int First, int Count)> _freeBlocks = new();

    protected BatchCollector(int initialCapacity) => Items = new TItem[initialCapacity];

    /// <summary>A pending (not-yet-flushed) segment exists.</summary>
    public bool Active => Count > _segmentStart;

    /// <summary>How many slots the retained arena currently holds, and what one of them holds. The cache sweeps them to
    /// find any whose owner has stopped drawing - the arena issues segments as RANGES, so such a slot is drawn by its
    /// neighbours' draw call.</summary>
    public int SlotCount => Count;

    public TItem ItemAt(int slot) => Items[slot];

    /// <summary>Absolute slot index of the item written by the LAST successful TryAdd (= its position in the retained
    /// buffer). RenderCache records it per unit during the walk so a partial-replay can address that unit's slot.</summary>
    public int LastSlot => Count - 1;

    /// <summary>GPU-buffer element capacity for THIS frame - derived TryAdd guards against overflowing it.</summary>
    protected int GpuCapacity => _gpuCapacity;

    /// <summary>The batch buffer - a BDA STORAGE buffer whose device address feeds the instanced shader.</summary>
    protected Buffer<TItem> GpuBuffer => _ring?[_current];

    /// <summary>Hand every GPU buffer this collector owns back to the device. Nothing else does: DisposeUnits frees the
    /// render UNITS, and a collector's ring - one host-visible buffer per frame in flight, per collector, and there are a
    /// dozen collectors per cache - stayed alive for the process. A closed window leaked all of it.</summary>
    public void DisposeGpuResources(IGraphicsDevice device)
    {
        if (_ring == null) return;

        foreach (var buffer in _ring)
        {
            if (buffer != null) device.AddToDeferDisposeQueue(buffer);
        }

        _ring = null;
        _mirror = null;
        _mirrorCount = null;
        _gpuCapacity = 0;
        _segments.Clear();
        _freeBlocks.Clear();
        Count = 0;
    }

    public void BeginFrame(IGraphicsDevice device)
    {
        // HEADROOM for patches. Capacity can only change here (the GPU buffer must not be reallocated under frames in
        // flight), so a walk that fits the scene exactly would leave a later patch nowhere to put a layer that GREW by one
        // item, and every such frame would fall back to the walk - which resets capacity to exactly the scene again.
        EnsureCpuCapacity(Count + Math.Max(64, Count / 8));

        Count = 0;
        _segmentStart = 0;
        _hasUnion = false;
        _segments.Clear();
        _indexById.Clear();
        _freeBlocks.Clear();
        EnsureRing(device);
        SelectSlot(device);
        OnBeginFrame(device);
    }

    // (Re)allocates the whole ring when the CPU array outgrew it (or the pipeline depth changed). The outgoing buffers
    // are handed to the device's deferred queue, NEVER disposed here: frames still in flight are reading them, and
    // freeing one under a live frame is the same bug in a louder form.
    // TEMP (flicker hunt): ADAMANTIUM_NO_RING=1 collapses the ring back to ONE copy - the pre-fix behaviour. Used to
    // verify the write probe actually fires on a known violation, so its silence means something.
    private static readonly bool RingDisabled = Environment.GetEnvironmentVariable("ADAMANTIUM_NO_RING") == "1";

    private void EnsureRing(IGraphicsDevice device)
    {
        var copies = RingDisabled ? 1 : (int)Math.Max(1, device.MaxFramesInFlight);
        if (_ring != null && _ring.Length == copies && _gpuCapacity >= Items.Length) return;

        if (_ring != null)
        {
            foreach (var buffer in _ring)
            {
                if (buffer != null) device.AddToDeferDisposeQueue(buffer);
            }
        }

        _ring = new Buffer<TItem>[copies];
        _mirror = new TItem[copies][];
        _mirrorCount = new int[copies];
        for (var i = 0; i < copies; i++)
        {
            _ring[i] = Adamantium.Graphics.Buffer.New<TItem>(device, (uint)Items.Length,
                BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal);
            _mirror[i] = new TItem[Items.Length];
            _mirrorCount[i] = 0;   // a fresh buffer holds nothing -> everything differs -> first write sends it all
        }
        _gpuCapacity = Items.Length;
    }

    // Advance on every WRITE, not by frame index. A copy written by one walk is read by every REPLAY frame that follows
    // it - several, since the render thread draws far more often than the recorder records. Indexing by frame index put
    // the next walk back on the same copy three frames later, overwriting it while those replays were still in flight
    // (BeginDraw's fence only proves frame N-3 is done; the replays are N-1 and N-2). Round-robin per write gives each
    // walk a copy no recent frame is reading, and by the time the ring wraps those frames are long retired.
    private int _writeCursor;

    private void SelectSlot(IGraphicsDevice device)
    {
        _current = _writeCursor % _ring.Length;
        _writeCursor = (_writeCursor + 1) % _ring.Length;
        _writeFrame = device.CurrentFrame;
        if (_mirror[_current].Length < Items.Length) Array.Resize(ref _mirror[_current], Items.Length);
    }

    // A write that does NOT come from a walk (a partial patch replaying last frame's ops) still has to land in THIS
    // frame's copy, and that copy is a lap behind - bring it up to the retained data first, by diff, then patch it.
    protected void PrepareRetainedWrite(IGraphicsDevice device)
    {
        if (_ring == null || device.CurrentFrame == _writeFrame) return;
        SelectSlot(device);
        UploadRange(0, Count);
    }

    // Sends [first, first+count) to the current copy, but only the one contiguous span that copy is actually missing.
    protected void UploadRange(int first, int count)
    {
        if (count <= 0) return;
        var mirror = _mirror[_current];
        var valid = _mirrorCount[_current];
        int lo = -1, hi = -1;
        for (var i = first; i < first + count; i++)
        {
            if (i < valid && SlotUnchanged(i, mirror)) continue;
            if (lo < 0) lo = i;
            hi = i;
        }
        if (lo >= 0)
        {
            _ring[_current].SetData(Items.AsSpan(lo, hi - lo + 1), (uint)(lo * Stride));
            Items.AsSpan(lo, hi - lo + 1).CopyTo(mirror.AsSpan(lo));
        }
        var end = first + count;
        if (end > _mirrorCount[_current]) _mirrorCount[_current] = end;
    }

    /// <summary>Per-frame hook for derived state (e.g. lazily creating the effect). Base does nothing.</summary>
    protected virtual void OnBeginFrame(IGraphicsDevice device) { }

    /// <summary>Whether anything is waiting to be flushed. The recorder needs it to know WHERE the next segment's paint
    /// span begins: a segment glues every control that falls between two flushes, so its span starts with the first one
    /// that put something in it - and the only moment that is knowable is while the segment is still empty.</summary>
    public bool HasPending => _hasUnion;

    /// <summary>Does a unit's logical bounds overlap the pending segment? A later overlapping unit must draw AFTER a
    /// flush (painter's order); spatially disjoint units (a list's stacked items) don't.</summary>
    public bool OverlapsPending(Rect r)
        => _hasUnion && r.X < _uR && _uL < r.Right && r.Y < _uB && _uT < r.Bottom;

    /// <summary>Draw the pending segment (if any) and advance. Uploads ONLY this segment at its byte offset (earlier
    /// segments' GPU data stays intact for their recorded draws), sets its scissor, draws it via DrawSegment with a
    /// firstInstance offset, then restores fullScissor so the caller's per-unit scissor state stays valid.</summary>
    public int Flush(IGraphicsDevice device, Rect2D fullScissor, Matrix4x4F projection)
    {
        if (!Active) return -1;

        var segStart = _segmentStart;
        var count = Count - segStart;

        // Send this segment to THIS frame's copy - only the span that copy is missing. An unchanged scene converges to
        // zero bytes once every copy has seen it, which is what the old single-buffer "skip the upload when the scene is
        // clean" bought, without the assumption that last frame's bytes are still there to be reused.
        UploadRange(segStart, count);

        // Record the segment (clip + buffer range) and draw it. On a Clean frame the walk is skipped and RenderCache
        // replays this exact segment via DrawRecordedSegment (same code path, no upload) - so the immediate draw here
        // and the replayed draw are byte-for-byte the same.
        var index = _segments.Count;
        var id = ++_nextSegmentId;
        _segments.Add(new Segment
        {
            Id = id, Scissor = _scissor, Count = (uint)count, First = (uint)segStart, Capacity = (uint)count,
            L = _uL, T = _uT, R = _uR, B = _uB, HasBounds = _hasUnion
        });
        _indexById[id] = index;
        OnSegmentRecorded(index);

        _segmentStart = Count;
        _hasUnion = false;

        DrawRecordedSegment(device, id, fullScissor, projection);
        return id;
    }

    /// <summary>Draw a segment recorded this frame (by its <see cref="Flush"/> index): set its clip, bind any per-segment
    /// state, issue the instanced draw, restore <paramref name="fullScissor"/>. Called by the immediate draw in Flush AND
    /// by RenderCache's clean-frame op replay - the latter re-issues last frame's segments with zero re-bake/upload.</summary>
    public void DrawRecordedSegment(IGraphicsDevice device, int id, Rect2D fullScissor, Matrix4x4F projection)
    {
        var index = IndexOf(id);
        if (index < 0) return;   // not part of this recorded frame - draw nothing rather than draw somebody else

        var s = _segments[index];
        if (s.Count == 0) return;   // re-issued to nothing - nothing left to draw
        device.SetScissors(s.Scissor);
        BindSegment(index);
        DrawSegment(device, _ring[_current], s.Count, s.First, projection);
        device.SetScissors(fullScissor);
    }

    // --- Spliced-patch surgery (per-control render-cache patching) -------------------------------------------------
    // A control whose batched unit COUNT changed can't patch its retained slots in place (later slots would shift).
    // Instead the caller excises the control's OLD run from whatever segment holds it and APPENDS its re-baked items as
    // a NEW segment at the retained frame's end - no other slot moves; the recorded op stream is spliced accordingly.
    // Abandoned slots stay allocated but unreferenced; the next full walk compacts naturally (BeginFrame resets Count),
    // and AppendPatchSegment's capacity precheck caps how much waste can accumulate between walks (a per-frame chart).

    /// <summary>The recorded segment whose retained range contains <paramref name="slot"/>, or -1. Zero-count (fully
    /// excluded) segments never match.</summary>
    public int FindSegmentContaining(int slot)
    {
        for (var i = 0; i < _segments.Count; i++)
        {
            var s = _segments[i];
            if (s.Count > 0 && slot >= s.First && slot < s.First + s.Count) return s.Id;
        }
        return -1;
    }

    /// <summary>Is every slot of this run blank - written by nobody? Asked before a range is handed back, so a stale run
    /// cannot take a live neighbour with it. The base class cannot read an instance's fields, so the derived collector
    /// answers; a family with nothing to say answers "no" and simply never reclaims.</summary>
    protected virtual bool IsBlank(int first, int count) => false;

    /// <summary>Gives a departed control's run back to the arena, when the run sits at an EDGE of the segment holding
    /// it. A segment is drawn as one range, so a run in its middle cannot be handed to anybody - splitting the segment
    /// to reclaim it would buy a slot at the price of a whole extra draw call, which is the wrong trade. At an edge the
    /// range simply shrinks: nothing moves, every other slot keeps its address, and the instances stop being issued.
    /// <para>At the HEAD the space is free for anyone (it leaves the segment entirely). At the TAIL it stays the
    /// segment's own room - which is what lets the control come back into the same place without moving a neighbour.</para>
    /// </summary>
    /// <returns>Whether the run left the drawn range.</returns>
    public bool ReclaimRun(int first, int count)
    {
        if (count <= 0) return false;

        var index = IndexOf(FindSegmentContaining(first));
        if (index < 0) return false;

        var s = _segments[index];
        var last = first + count;
        if (last > s.First + s.Count) return false;   // the run is not wholly inside what this segment draws

        // ...and it must be EMPTY - every slot in it already blanked. A group's runs can be stale: a walk that did not
        // visit the group reassigns its slots to whoever it recorded there, so the run may now name somebody else's
        // instances (which is why the blanking checks the owner tag before it writes). Shrinking a range by a stale
        // length takes a live neighbour out of the draw with it - measured as a card that vanished from the frame.
        if (!IsBlank(first, count)) return false;

        if (first == (int)s.First)
        {
            s.First += (uint)count;
            s.Count -= (uint)count;
            s.Capacity -= (uint)count;
            _segments[index] = s;
            _freeBlocks.Add((first, count));
            return true;
        }

        if (last == (int)(s.First + s.Count))
        {
            s.Count -= (uint)count;
            _segments[index] = s;   // the room past Count stays this segment's, to grow back into
            return true;
        }

        return false;
    }

    public Rect2D GetSegmentScissor(int id)
    {
        var index = IndexOf(id);
        return index < 0 ? null : _segments[index].Scissor;
    }

    /// <summary>What this recorded segment covers, in logical coordinates - empty when it never had bounds (a stale id, or
    /// a segment re-issued to nothing).</summary>
    public Rect SegmentBounds(int id)
    {
        var index = IndexOf(id);
        if (index < 0) return Rect.Empty;

        var s = _segments[index];
        return s.HasBounds && s.Count > 0 ? new Rect(s.L, s.T, s.R - s.L, s.B - s.T) : Rect.Empty;
    }

    /// <summary>Grow a recorded segment's footprint by what a patch has just put into it - a layer that gained an item now
    /// covers it, and the next placement has to see that.</summary>
    public void GrowSegmentBounds(int id, Rect bounds)
    {
        var index = IndexOf(id);
        if (index < 0) return;

        var s = _segments[index];
        if (!s.HasBounds)
        {
            _segments[index] = s with { L = bounds.X, T = bounds.Y, R = bounds.Right, B = bounds.Bottom, HasBounds = true };
            return;
        }

        _segments[index] = s with
        {
            L = Math.Min(s.L, bounds.X), T = Math.Min(s.T, bounds.Y),
            R = Math.Max(s.R, bounds.Right), B = Math.Max(s.B, bounds.Bottom)
        };
    }

    /// TEMP (flicker hunt): what this recorded segment actually draws, for the walk-vs-replay trace comparison.
    public string DescribeSegment(int id)
    {
        var index = IndexOf(id);
        if (index < 0) return $"seg#{id} MISSING (have {_segments.Count})";
        var s = _segments[index];
        var x = s.Scissor?.Offset?.X ?? -1;
        var y = s.Scissor?.Offset?.Y ?? -1;
        var w = s.Scissor?.Extent?.Width ?? 0;
        var h = s.Scissor?.Extent?.Height ?? 0;
        return $"first={s.First} count={s.Count} clip={x},{y} {w}x{h}";
    }

    /// <summary>Free retained capacity for patch appends this frame (capacity only grows at the next BeginFrame).</summary>
    public int PatchCapacityLeft => _gpuCapacity - Count;

    /// <summary>Retained slot count (the next patch append starts here).</summary>
    public int RetainedCount => Count;

    /// <summary>The retained range [first, first+count) a recorded segment currently draws.</summary>
    public (int First, int Count) SegmentRange(int id)
    {
        var index = IndexOf(id);
        return index < 0 ? (-1, 0) : ((int)_segments[index].First, (int)_segments[index].Count);
    }

    /// <summary>Cut a recorded segment in two at <paramref name="firstOfSecond"/> and return the new segment's index; the
    /// original keeps everything before the cut. Nothing moves - the arena is untouched and both halves keep drawing the
    /// bytes they already held - so this costs one list insert.
    /// <para>What it is for: a segment glues every control between two flushes, so the ONE op that draws it covers a whole
    /// span of paint ranks. A control that starts drawing with a rank INSIDE that span has no correct place in a flat op
    /// stream until the span is split at it (see RenderCache's PlaceNewSegment).</para>
    /// <para>Capacity stays with the FIRST half: it is the tail of the original allocation, and a re-issue that grows must
    /// grow into it, never into the second half's live items.</para></summary>
    public int SplitSegment(int id, int firstOfSecond)
    {
        var index = IndexOf(id);
        if (index < 0) return -1;

        var s = _segments[index];
        var offset = (uint)firstOfSecond - s.First;
        if (offset == 0 || offset >= s.Count) return -1;   // nothing on one side of the cut: not a split

        // The spare room is the TAIL of the original allocation, so it goes to the SECOND half. The first half is boxed in
        // by live items and may not grow at all: letting it keep the capacity would let a re-issue write over the
        // neighbour it just created.
        // Both halves inherit the whole footprint: which items went where is known, but their individual bounds are not
        // kept, and claiming a smaller cover than a half actually draws would let a later placement decide "no overlap"
        // about something it does overlap. Coarse is safe here; wrong is not.
        var second = new Segment
        {
            Id = ++_nextSegmentId,
            Scissor = s.Scissor,
            First = (uint)firstOfSecond,
            Count = s.Count - offset,
            Capacity = s.Capacity - offset,
            L = s.L, T = s.T, R = s.R, B = s.B, HasBounds = s.HasBounds
        };
        _segments[index] = s with { Count = offset, Capacity = offset };
        _segments.Insert(index + 1, second);

        // Per-segment state (a texture, a field) is keyed by index too, and both halves carry the same one.
        OnSegmentInserted(index + 1);

        // The insert moved every later segment one along - which is why nobody outside holds an index. Fixed HERE, once.
        Reindex(index + 1);
        return second.Id;
    }

    /// <summary>Copy retained items out, for a caller re-issuing a segment: the instances of the groups that did NOT
    /// change are carried over as bytes rather than re-baked, so re-issuing a layer costs a copy, not a re-computation.</summary>
    public void CopyRetained(int first, int count, List<TItem> into)
    {
        for (var i = 0; i < count; i++) into.Add(Items[first + i]);
    }

    /// <summary>Re-issue a WHOLE segment over a freshly baked run: the items are appended at the retained frame's end and
    /// the SAME segment index is pointed at them. The recorded op stream is not touched at all - the op that drew this
    /// segment still stands in its place, so paint order relative to everything else (text, per-unit draws, an instanced
    /// flush) is unchanged by construction. That is the point: a control whose unit count changed is repaired by re-baking
    /// the LAYER it belongs to, instead of tearing the layer's segment in two to weave one item into the middle of it.
    /// False when the arena has no room; the caller falls back to a full walk, which compacts it.</summary>
    /// <summary>Replace [at, at+replaced) INSIDE a segment with <paramref name="items"/>, shifting only what follows - the
    /// cheap shape of a layer edit, a hover backdrop being one item among a screenful. The head never moves and the upload
    /// covers the edit plus the tail it pushed.
    /// False when the result no longer fits the room this segment owns; the caller then relocates it whole.</summary>
    public bool ReplaceInSegment(IGraphicsDevice device, int id, int at, int replaced, ReadOnlySpan<TItem> items)
    {
        var index = IndexOf(id);
        if (index < 0) return false;

        var s = _segments[index];
        var newCount = (int)s.Count - replaced + items.Length;
        if (newCount > (int)s.Capacity) return false;

        PrepareRetainedWrite(device);

        var first = (int)s.First;
        var delta = items.Length - replaced;
        var tailAt = first + at + replaced;
        var tailLen = (int)s.Count - at - replaced;
        if (delta != 0 && tailLen > 0) Array.Copy(Items, tailAt, Items, tailAt + delta, tailLen);
        items.CopyTo(Items.AsSpan(first + at));

        var touched = items.Length + (delta != 0 ? tailLen + Math.Max(0, -delta) : 0);
        UploadRange(first + at, touched);
        _segments[index] = s with { Count = (uint)newCount };
        return true;
    }

    /// <summary>Register a NEW segment over freshly written items - for a control that starts drawing where nothing of its
    /// own was recorded. It gets its own range (reused from a vacated one where possible) and its own op, placed by paint
    /// rank; nothing already recorded moves. Returns the segment index, or -1 when the arena has no room.</summary>
    public int AllocateSegment(IGraphicsDevice device, ReadOnlySpan<TItem> items, Rect2D scissor)
    {
        PrepareRetainedWrite(device);

        var want = items.Length + Math.Max(16, items.Length / 8);
        var first = -1;
        for (var i = 0; i < _freeBlocks.Count; i++)
        {
            if (_freeBlocks[i].Count < want) continue;
            first = _freeBlocks[i].First;
            want = _freeBlocks[i].Count;
            _freeBlocks.RemoveAt(i);
            break;
        }

        if (first < 0)
        {
            if (want > PatchCapacityLeft) return -1;
            first = Count;
            EnsureCpuCapacity(first + want);
            Count = first + want;
        }

        items.CopyTo(Items.AsSpan(first));
        UploadRange(first, items.Length);
        var id = ++_nextSegmentId;
        _indexById[id] = _segments.Count;
        _segments.Add(new Segment { Id = id, Scissor = scissor, Count = (uint)items.Length, First = (uint)first, Capacity = (uint)want });
        return id;   // its footprint comes from the caller (GrowSegmentBounds): only it knows the logical bounds it baked
    }

    public bool RepointSegment(IGraphicsDevice device, int id, ReadOnlySpan<TItem> items, Rect2D scissor)
    {
        var index = IndexOf(id);
        if (index < 0) return false;

        PrepareRetainedWrite(device);

        var s = _segments[index];
        var need = items.Length;

        // Inside its own room: the normal case once a layer has been re-issued once, and the only one a hover ever needs.
        if (need <= (int)s.Capacity)
        {
            items.CopyTo(Items.AsSpan((int)s.First));
            UploadRange((int)s.First, need);
            _segments[index] = s with { Scissor = scissor, Count = (uint)need };
            return true;
        }

        // Outgrew it: take a bigger block, WITH room to grow again. Handing out exactly what is asked for means the block
        // freed here (N) never fits the next request (N+1), and the arena fills with near-misses.
        var want = need + Math.Max(16, need / 8);
        var first = -1;
        for (var i = 0; i < _freeBlocks.Count; i++)
        {
            if (_freeBlocks[i].Count < want) continue;
            first = _freeBlocks[i].First;
            want = _freeBlocks[i].Count;   // take the block whole; splitting it leaves shards nothing fits in
            _freeBlocks.RemoveAt(i);
            break;
        }

        if (first < 0)
        {
            if (want > PatchCapacityLeft) return false;
            first = Count;
            EnsureCpuCapacity(first + want);
            Count = first + want;
        }

        if (s.Capacity > 0) _freeBlocks.Add(((int)s.First, (int)s.Capacity));

        items.CopyTo(Items.AsSpan(first));
        UploadRange(first, need);
        _segments[index] = s with { Scissor = scissor, Count = (uint)need, First = (uint)first, Capacity = (uint)want };
        return true;
    }

    /// <summary>
    /// Patch ONE already-flushed slot in place: overwrite its retained CPU + GPU bytes without touching any other slot or
    /// re-baking the frame. Lets a fast-path partial (a hover recolouring one tile) update just the dirty units' instances
    /// and then REPLAY last frame's op stream - the recorded segments still point at the same buffer, now with this slot
    /// updated - instead of re-baking every unit (the O(N) draw-phase cost of a partial). The caller must NOT have begun a
    /// new frame (Items/_gpu still hold the last walk's data) and slot must be within that retained data.
    /// </summary>
    /// <summary>Blanks the slots of a run without touching the segment they sit in. A segment is issued as a RANGE, so a
    /// control that stops drawing cannot simply be forgotten: its instances stay inside somebody else's range and are
    /// re-issued with it on every replayed frame. Reclaiming the range belongs to the next recording walk; until then the
    /// bytes have to draw nothing.</summary>
    public void BlankRun(IGraphicsDevice device, uint first, uint count)
    {
        if (count == 0 || first + count > (uint)Count) return;

        PrepareRetainedWrite(device);
        for (var i = 0u; i < count; i++) Items[first + i] = default;
        UploadRange((int)first, (int)count);
    }

    public void UpdateSlot(IGraphicsDevice device, int slot, TItem item)
    {
        PrepareRetainedWrite(device);
        Items[slot] = item;
        UploadRange(slot, 1);
    }

    /// <summary>Hook: capture per-segment state at record time (the text batch stashes the segment's atlas). Base no-op.</summary>
    protected virtual void OnSegmentRecorded(int index) { }

    /// <summary>Hook: a segment was INSERTED at this index by a split, so index-keyed per-segment state has to make room
    /// for it - carrying a copy of what the segment being split held, since both halves draw the same way. Base no-op.</summary>
    protected virtual void OnSegmentInserted(int index) { }

    /// <summary>Hook: restore the per-segment state captured by <see cref="OnSegmentRecorded"/> before its draw. Base no-op.</summary>
    protected virtual void BindSegment(int index) { }

    // Does slot i already hold, in this copy, exactly what we baked? A blittable per-item bytewise compare (the items are
    // unmanaged Vector4F structs), SIMD-accelerated by SequenceEqual - cheap relative to the GPU upload it lets us skip.
    private bool SlotUnchanged(int i, TItem[] mirror)
        => MemoryMarshal.AsBytes(Items.AsSpan(i, 1)).SequenceEqual(MemoryMarshal.AsBytes(mirror.AsSpan(i, 1)));

    /// <summary>Emit the instanced draw for [firstInstance, firstInstance + count) of <paramref name="buffer"/>. The
    /// segment's scissor is already set; the derived type sets its own blend/depth/effect state + pass.</summary>
    protected abstract void DrawSegment(IGraphicsDevice device, Buffer<TItem> buffer, uint count, uint firstInstance, Matrix4x4F projection);

    /// <summary>Record the pending segment's clip + grow its overlap union. Derived TryAdd calls this AFTER writing its
    /// item(s) into Items/Count. All items of one segment share the clip (the caller enforces it).</summary>
    protected void MarkPending(Rect2D scissor, Rect logicalBounds)
    {
        _scissor = scissor;
        if (!_hasUnion) { _uL = logicalBounds.X; _uT = logicalBounds.Y; _uR = logicalBounds.Right; _uB = logicalBounds.Bottom; _hasUnion = true; return; }
        if (logicalBounds.X < _uL) _uL = logicalBounds.X;
        if (logicalBounds.Y < _uT) _uT = logicalBounds.Y;
        if (logicalBounds.Right > _uR) _uR = logicalBounds.Right;
        if (logicalBounds.Bottom > _uB) _uB = logicalBounds.Bottom;
    }

    /// <summary>Grow the CPU array to fit <paramref name="needed"/> items (also raises next frame's GPU size via
    /// BeginFrame). Derived TryAdd calls this BEFORE writing, then guards Count + n against <see cref="GpuCapacity"/>.</summary>
    protected void EnsureCpuCapacity(int needed)
    {
        if (needed <= Items.Length) return;
        var newLen = Items.Length;
        while (newLen < needed) newLen *= 2;
        Array.Resize(ref Items, newLen);
    }
}
