using System;
using System.Threading;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.UI.Rendering;

// A reusable GPU allocation with a capacity that grows with headroom and NEVER shrinks (high-water-mark). It starts as a
// SINGLE buffer - enough for static geometry, which is written once and only read afterwards - and lazily promotes to a
// ring of N (frames-in-flight) buffers the first time its geometry changes after being drawn, i.e. when it starts
// animating and the one buffer could be read by an in-flight frame while a new frame rewrites it. The owner calls
// Acquire each frame for the current slot plus a "stale, must (re)write" flag, so a static payload settles to zero work
// and an animated one rewrites only the current frame's slot - no per-frame allocation. See GPU_BUFFER_REUSE_PLAN §1-3.
public sealed class ReusableBuffer : IDisposable
{
    // Round capacity up to a power-of-two bucket (min 256 B). Small geometry wobble (a few vertices, a CornerRadius
    // arc-segment change) then reuses the same allocation instead of regrowing, and grows stay rare.
    private const ulong MinCapacityBytes = 256;

    private readonly GpuBufferManager _manager;
    private readonly BufferUsageFlags _usage;
    private readonly MemoryPropertyFlags _memory;
    // RENDER-THREAD state. Everything below is written only inside Acquire, which is the draw path - so the buffers are
    // allocated and read by one thread. The record thread used to allocate here too (Reserve/Invalidate->Promote), and
    // since none of it is ordered against the reader, the render thread could see `_promoted` already true while the ring
    // slots it then indexes were still null: Acquire handed back nothing and the address fetch faulted the process. It
    // took a fast render loop to catch that window at all, which is why it hid for as long as it did.
    private readonly Buffer[] _ring;       // [0] only while single; all slots once promoted to a ring
    private readonly int[] _slotVersion;   // data version last written to each slot; -1 = stale (must rewrite)
    private ulong _capacity;               // bytes; high-water-mark, only grows
    private bool _promoted;                // false = single buffer (static), true = per-frame ring (animating)
    private bool _drawnOnce;               // a frame has read slot 0 -> a later change must promote, not rewrite in place
    private int _seenVersion;              // the data version this side has already acted on

    // The RECORD thread's half: two plain numbers, published atomically and read by the draw path. It says WHAT is
    // wanted; the draw path decides when to allocate it.
    private int _version;                  // current data version; bumped by Invalidate
    private long _requestedBytes;          // largest size asked for by Reserve (high-water)

    public ReusableBuffer(GpuBufferManager manager, BufferUsageFlags usage, MemoryPropertyFlags memory)
    {
        _manager = manager;
        _usage = usage;
        _memory = memory;
        _ring = new Buffer[manager.RingSize];
        _slotVersion = new int[manager.RingSize];
        Array.Fill(_slotVersion, -1);
    }

    /// <summary>RECORD thread: geometry of this size is coming. Only remembered - the allocation itself belongs to the
    /// draw path, which is the only thread allowed to touch the buffers.</summary>
    public void Reserve(ulong requiredBytes)
    {
        var wanted = (long)RoundUpToBucket(Math.Max(requiredBytes, MinCapacityBytes));
        long seen;
        do
        {
            seen = Interlocked.Read(ref _requestedBytes);
            if (wanted <= seen)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _requestedBytes, wanted, seen) != seen);
    }

    /// <summary>RECORD thread: the data changed, so every slot must rewrite lazily. A bumped number and nothing else -
    /// whether that also means promoting to a ring is the draw path's call, because promoting ALLOCATES.</summary>
    public void Invalidate() => Interlocked.Increment(ref _version);

    // Ensure the current slot holds at least requiredBytes (grow, never shrink) and return it. needsWrite is true when
    // that slot doesn't yet hold the current data version (freshly grown/promoted, or invalidated) - the caller then
    // uploads/re-dispatches into the returned buffer; otherwise it's already current (zero work).
    public Buffer Acquire(ulong requiredBytes, out bool needsWrite)
    {
        var version = Volatile.Read(ref _version);

        // A change AFTER the single buffer has been drawn is what promotes it: that buffer may be read by an in-flight
        // frame, so the rewrite has to go to a slot of its own rather than over it.
        if (_drawnOnce && !_promoted && version != _seenVersion)
        {
            Promote();
        }

        _seenVersion = version;
        EnsureCapacity(Math.Max(requiredBytes, (ulong)Interlocked.Read(ref _requestedBytes)));

        var slot = _promoted ? _manager.CurrentFrame : 0u;
        needsWrite = _slotVersion[slot] != version;
        if (needsWrite)
        {
            _slotVersion[slot] = version;
        }

        _drawnOnce = true;
        return _ring[slot];
    }

    // Every ACTIVE slot ends up allocated and at least this big - slot 0 while single, the whole ring once promoted.
    // Checking only slot 0 was half the crash: after promotion Acquire indexes by frame, so a slot the check never
    // looked at could still be empty. An existing buffer may be read by an in-flight frame, so it is handed to the
    // deferred queue rather than freed. Growth is rare (high-water-mark) - about once, at the peak size.
    private void EnsureCapacity(ulong requiredBytes)
    {
        var needed = RoundUpToBucket(Math.Max(requiredBytes, MinCapacityBytes));
        var size = Math.Max(needed, _capacity);
        var slots = _promoted ? _ring.Length : 1;
        for (var i = 0; i < slots; i++)
        {
            if (_ring[i] != null && needed <= _capacity)
            {
                continue;
            }

            if (_ring[i] != null)
            {
                _manager.Device.AddToDeferDisposeQueue(_ring[i]);
            }

            _ring[i] = Buffer.New(_manager.Device, size, _usage, _memory);
            _slotVersion[i] = -1;
        }

        _capacity = size;
    }

    // Hands the single buffer over and leaves the ring EMPTY: EnsureCapacity, which runs right after, allocates every
    // slot at the current capacity. One place that allocates, rather than two that must agree.
    private void Promote()
    {
        _promoted = true;
        if (_ring[0] != null)
        {
            _manager.Device.AddToDeferDisposeQueue(_ring[0]);
            _ring[0] = null;
        }

        Array.Fill(_slotVersion, -1);
    }

    private static ulong RoundUpToBucket(ulong bytes)
    {
        var bucket = MinCapacityBytes;
        while (bucket < bytes) bucket <<= 1;
        return bucket;
    }

    public void Dispose()
    {
        for (var i = 0; i < _ring.Length; i++)
        {
            _ring[i]?.Dispose();
            _ring[i] = null;
        }
    }
}
