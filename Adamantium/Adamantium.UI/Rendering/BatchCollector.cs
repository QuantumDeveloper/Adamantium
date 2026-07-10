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
// docs/TEXT_GLYPH_BATCH_PLAN.md §9). Holds a growable CPU array + one growable GPU vertex buffer, filled APPEND-ONLY
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

    private Buffer<TItem> _gpu;
    private int _gpuCapacity;
    private bool _recreatedThisFrame;  // the GPU buffer was (re)allocated this frame -> its old contents are gone

    // Last frame's baked items (a CPU mirror of what the GPU buffer holds) + how many were valid. The walk is
    // deterministic and _renderUnits is retained across partial frames, so slot i holds the SAME item as last frame while
    // the layout is stable (a hover). Comparing the freshly baked items to this and uploading only the CHANGED span moves
    // a few hundred bytes on a hover instead of re-uploading every instance - the incremental-upload path for NON-clean
    // frames (a clean frame skips it entirely via SceneClean). A scroll/structural change shifts most slots, so most
    // differ and we upload ~everything (correct - no worse than the old full upload).
    private TItem[] _prevItems;
    private int _prevCount;

    // Segments drawn THIS frame (each = a clip + a buffer range), retained for the clean-frame op replay. RenderCache
    // records an ordered op stream during the walk; on a fully-unchanged (Clean) frame it skips the walk entirely and
    // replays each segment via DrawRecordedSegment - no re-bake, no upload (the GPU buffer still holds these exact
    // bytes). Cleared at BeginFrame; a segment's index is stable within the frame that recorded it.
    protected struct Segment { public Rect2D Scissor; public uint Count; public uint First; }
    private readonly List<Segment> _segments = new();

    /// <summary>Set by the caller each frame BEFORE the walk: true when the render scene is provably unchanged since last
    /// frame (RenderBuildKind.Clean). The walk still re-runs (re-bakes identical items, re-records identical draws), but
    /// Flush then SKIPS the GPU upload - last frame's bytes are still in the retained buffer at the same offsets, so
    /// re-uploading them is pure waste. This is the incremental-upload path: on an idle frame, zero bytes move.</summary>
    public bool SceneClean { get; set; }

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
    protected Buffer<TItem> GpuBuffer => _gpu;

    public void BeginFrame(IGraphicsDevice device)
    {
        _prevCount = Count;   // last frame's total (Count still holds it) - the valid length of _prevItems for the diff
        Count = 0;
        _segmentStart = 0;
        _hasUnion = false;
        _recreatedThisFrame = false;
        _segments.Clear();
        if (_gpu == null || _gpuCapacity < Items.Length)
        {
            _gpu?.Dispose();
            _gpu = UsesStorageBuffer
                ? Adamantium.Graphics.Buffer.New<TItem>(device, (uint)Items.Length,
                    BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                    MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal)
                : Adamantium.Graphics.Buffer.Vertex.New<TItem>(device, (uint)Items.Length, BufferMemoryUsage.UploadFromCpuToGpu);
            _gpuCapacity = Items.Length;
            _recreatedThisFrame = true;   // fresh buffer holds no prior data -> a SceneClean skip is unsafe this frame
        }
        OnBeginFrame(device);
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

        // Upload strategy for this segment:
        //  - buffer (re)allocated this frame  -> no prior GPU contents, upload the whole segment;
        //  - SceneClean                        -> the retained buffer already holds these exact bytes, upload nothing;
        //  - otherwise (a partial/full change) -> upload ONLY the [lo,hi] slots whose baked bytes differ from last frame.
        if (_recreatedThisFrame)
        {
            _gpu.SetData(Items.AsSpan(segStart, count), (uint)(segStart * Stride));
        }
        else if (!SceneClean)
        {
            int lo = -1, hi = -1;
            for (var i = segStart; i < Count; i++)
            {
                if (i < _prevCount && SlotUnchanged(i)) continue;
                if (lo < 0) lo = i;
                hi = i;
            }
            if (lo >= 0)
                _gpu.SetData(Items.AsSpan(lo, hi - lo + 1), (uint)(lo * Stride));
        }

        // Mirror this segment into _prevItems for next frame's diff (skip only a clean frame - it is already identical).
        if (!SceneClean || _recreatedThisFrame)
        {
            if (_prevItems == null || _prevItems.Length < Items.Length) Array.Resize(ref _prevItems, Items.Length);
            Items.AsSpan(segStart, count).CopyTo(_prevItems.AsSpan(segStart));
        }

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
        device.SetScissors(s.Scissor);
        BindSegment(index);
        DrawSegment(device, _gpu, s.Count, s.First, projection);
        device.SetScissors(fullScissor);
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
        Items[slot] = item;
        if (_prevItems != null && slot < _prevItems.Length) _prevItems[slot] = item;
        _gpu.SetData(Items.AsSpan(slot, 1), (uint)(slot * Stride));
    }

    /// <summary>Hook: capture per-segment state at record time (the text batch stashes the segment's atlas). Base no-op.</summary>
    protected virtual void OnSegmentRecorded(int index) { }

    /// <summary>Hook: restore the per-segment state captured by <see cref="OnSegmentRecorded"/> before its draw. Base no-op.</summary>
    protected virtual void BindSegment(int index) { }

    // Did slot i bake byte-identical to last frame? A blittable per-item bytewise compare (the items are unmanaged
    // Vector4F structs), SIMD-accelerated by SequenceEqual - cheap relative to the GPU upload it lets us skip.
    private bool SlotUnchanged(int i)
        => MemoryMarshal.AsBytes(Items.AsSpan(i, 1)).SequenceEqual(MemoryMarshal.AsBytes(_prevItems.AsSpan(i, 1)));

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
