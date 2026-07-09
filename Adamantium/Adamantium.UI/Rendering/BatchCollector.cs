using System;
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

    /// <summary>Set by the caller each frame BEFORE the walk: true when the render scene is provably unchanged since last
    /// frame (RenderBuildKind.Clean). The walk still re-runs (re-bakes identical items, re-records identical draws), but
    /// Flush then SKIPS the GPU upload - last frame's bytes are still in the retained buffer at the same offsets, so
    /// re-uploading them is pure waste. This is the incremental-upload path: on an idle frame, zero bytes move.</summary>
    public bool SceneClean { get; set; }

    protected BatchCollector(int initialCapacity) => Items = new TItem[initialCapacity];

    /// <summary>A pending (not-yet-flushed) segment exists.</summary>
    public bool Active => Count > _segmentStart;

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
        Count = 0;
        _segmentStart = 0;
        _hasUnion = false;
        _recreatedThisFrame = false;
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
    public void Flush(IGraphicsDevice device, Rect2D fullScissor, Matrix4x4F projection)
    {
        if (!Active) return;

        var count = Count - _segmentStart;
        // Incremental upload: on a provably-clean frame the retained buffer already holds these exact bytes at this exact
        // offset (the walk is deterministic, so identical items landed at identical slots last frame) - so skip the copy.
        // A buffer (re)allocated this frame has no prior contents, so it must still upload.
        if (!SceneClean || _recreatedThisFrame)
            _gpu.SetData(Items.AsSpan(_segmentStart, count), (uint)(_segmentStart * Stride));
        device.SetScissors(_scissor);
        DrawSegment(device, _gpu, (uint)count, (uint)_segmentStart, projection);
        device.SetScissors(fullScissor);

        _segmentStart = Count;
        _hasUnion = false;
    }

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
