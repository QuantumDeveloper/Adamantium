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
// in the vertex shader by SV_InstanceID (the quad comes from SV_VertexID), except for the glyph/text batch, which still
// binds its instances as a per-instance VERTEX buffer (see UsesStorageBuffer). Filled APPEND-ONLY
// within a frame and drawn as SEGMENTS - a segment is a run of items sharing one clip (scissor), drawn with a
// firstInstance offset so a mid-frame flush never overwrites an earlier segment's still-recorded draw. The GPU buffer
// only grows at BeginFrame (a safe point: the render runs after the frame fence, so last frame's reads are done). The
// paint-order union bounds let the caller flush before a non-batched unit that overlaps the pending segment.
//
// Derived types add: item baking (their own TryAdd, which writes into Items/Count then calls MarkPending) and the
// per-segment draw (DrawSegment). Grouping (which items share a segment) is decided by the caller (RenderCache).
internal abstract class BatchCollector<TItem> where TItem : struct
{
    private static readonly int Stride = Marshal.SizeOf<TItem>();

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
    protected struct Segment { public Rect2D Scissor; public uint Count; public uint First; }
    private readonly List<Segment> _segments = new();

    protected BatchCollector(int initialCapacity) => Items = new TItem[initialCapacity];

    /// <summary>A pending (not-yet-flushed) segment exists.</summary>
    public bool Active => Count > _segmentStart;

    /// <summary>Absolute slot index of the item written by the LAST successful TryAdd (= its position in the retained
    /// buffer). RenderCache records it per unit during the walk so a partial-replay can address that unit's slot.</summary>
    public int LastSlot => Count - 1;

    /// <summary>GPU-buffer element capacity for THIS frame - derived TryAdd guards against overflowing it.</summary>
    protected int GpuCapacity => _gpuCapacity;

    /// <summary>The batch buffer is a BDA STORAGE buffer by default (per-instance data read in the vertex shader by
    /// SV_InstanceID, quad from SV_VertexID - no per-instance vertex buffer). Override to <c>false</c> only for a batch
    /// that still binds its instances as a per-instance VERTEX buffer (the glyph/text batch).</summary>
    protected virtual bool UsesStorageBuffer => true;

    /// <summary>The batch buffer (its device address feeds the instanced shader when <see cref="UsesStorageBuffer"/>).</summary>
    protected Buffer<TItem> GpuBuffer => _ring?[_current];

    public void BeginFrame(IGraphicsDevice device)
    {
        Count = 0;
        _segmentStart = 0;
        _hasUnion = false;
        _segments.Clear();
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
            _ring[i] = UsesStorageBuffer
                ? Adamantium.Graphics.Buffer.New<TItem>(device, (uint)Items.Length,
                    BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                    MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal)
                : Adamantium.Graphics.Buffer.Vertex.New<TItem>(device, (uint)Items.Length, BufferMemoryUsage.UploadFromCpuToGpu);
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
    private void PrepareRetainedWrite(IGraphicsDevice device)
    {
        if (_ring == null || device.CurrentFrame == _writeFrame) return;
        SelectSlot(device);
        UploadRange(0, Count);
    }

    // Sends [first, first+count) to the current copy, but only the one contiguous span that copy is actually missing.
    private void UploadRange(int first, int count)
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
        _segments.Add(new Segment { Scissor = _scissor, Count = (uint)count, First = (uint)segStart });
        OnSegmentRecorded(index);

        _segmentStart = Count;
        _hasUnion = false;

        DrawRecordedSegment(device, index, fullScissor, projection);
        return index;
    }

    /// <summary>Draw a segment recorded this frame (by its <see cref="Flush"/> index): set its clip, bind any per-segment
    /// state, issue the instanced draw, restore <paramref name="fullScissor"/>. Called by the immediate draw in Flush AND
    /// by RenderCache's clean-frame op replay - the latter re-issues last frame's segments with zero re-bake/upload.</summary>
    public void DrawRecordedSegment(IGraphicsDevice device, int index, Rect2D fullScissor, Matrix4x4F projection)
    {
        var s = _segments[index];
        if (s.Count == 0) return;   // fully excluded by a spliced patch (see ExcludeRun) - nothing left to draw
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
            if (s.Count > 0 && slot >= s.First && slot < s.First + s.Count) return i;
        }
        return -1;
    }

    public Rect2D GetSegmentScissor(int index) => _segments[index].Scissor;

    /// TEMP (flicker hunt): what this recorded segment actually draws, for the walk-vs-replay trace comparison.
    public string DescribeSegment(int index)
    {
        if (index < 0 || index >= _segments.Count) return $"seg[{index}] MISSING (have {_segments.Count})";
        var s = _segments[index];
        var x = s.Scissor?.Offset?.X ?? -1;
        var y = s.Scissor?.Offset?.Y ?? -1;
        var w = s.Scissor?.Extent?.Width ?? 0;
        var h = s.Scissor?.Extent?.Height ?? 0;
        return $"first={s.First} count={s.Count} clip={x},{y} {w}x{h}";
    }

    /// <summary>Shrinks segment <paramref name="segmentIndex"/> to end BEFORE <paramref name="first"/> and registers the
    /// remainder AFTER [first, first+count) as a NEW segment (same scissor), returning its index (-1 when nothing
    /// remains after). With count = 0 this is a pure SPLIT at <paramref name="first"/> (an op-order insertion point).
    /// NOTE: only for collectors with no per-segment state (the SDF family) - the split does not re-run
    /// OnSegmentRecorded, so a stashing collector (the text batch's atlas) must not be patched this way.</summary>
    public int ExcludeRun(int segmentIndex, int first, int count)
    {
        var s = _segments[segmentIndex];
        var before = first - (int)s.First;
        var afterCount = (int)s.Count - before - count;
        _segments[segmentIndex] = new Segment { Scissor = s.Scissor, Count = (uint)Math.Max(0, before), First = s.First };
        if (afterCount <= 0) return -1;
        var idx = _segments.Count;
        _segments.Add(new Segment { Scissor = s.Scissor, Count = (uint)afterCount, First = (uint)(first + count) });
        return idx;
    }

    /// <summary>Free retained capacity for patch appends this frame (capacity only grows at the next BeginFrame).</summary>
    public int PatchCapacityLeft => _gpuCapacity - Count;

    /// <summary>Retained slot count (the next patch append starts here).</summary>
    public int RetainedCount => Count;

    /// <summary>Appends a spliced control's re-baked items into the RETAINED frame data (no BeginFrame ran): writes them
    /// after the last used slot, uploads exactly those bytes, mirrors them for the next incremental-upload diff, and
    /// registers a new segment over the range. The caller pre-checks <see cref="PatchCapacityLeft"/>. Returns the new
    /// segment's index.</summary>
    public int AppendPatchSegment(IGraphicsDevice device, ReadOnlySpan<TItem> items, Rect2D scissor)
    {
        PrepareRetainedWrite(device);
        var first = Count;
        EnsureCpuCapacity(first + items.Length);
        items.CopyTo(Items.AsSpan(first));
        Count = first + items.Length;
        UploadRange(first, items.Length);
        var idx = _segments.Count;
        _segments.Add(new Segment { Scissor = scissor, Count = (uint)items.Length, First = (uint)first });
        return idx;
    }

    /// <summary>
    /// Patch ONE already-flushed slot in place: overwrite its retained CPU + GPU bytes without touching any other slot or
    /// re-baking the frame. Lets a fast-path partial (a hover recolouring one tile) update just the dirty units' instances
    /// and then REPLAY last frame's op stream - the recorded segments still point at the same buffer, now with this slot
    /// updated - instead of re-baking every unit (the O(N) draw-phase cost of a partial). The caller must NOT have begun a
    /// new frame (Items/_gpu still hold the last walk's data) and slot must be within that retained data.
    /// </summary>
    public void UpdateSlot(IGraphicsDevice device, int slot, TItem item)
    {
        PrepareRetainedWrite(device);
        Items[slot] = item;
        UploadRange(slot, 1);
    }

    /// <summary>Hook: capture per-segment state at record time (the text batch stashes the segment's atlas). Base no-op.</summary>
    protected virtual void OnSegmentRecorded(int index) { }

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
