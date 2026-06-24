using System;
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
    private readonly Buffer[] _ring;       // [0] only while single; all slots once promoted to a ring
    private readonly int[] _slotVersion;   // data version last written to each slot; -1 = stale (must rewrite)
    private ulong _capacity;               // bytes; high-water-mark, only grows
    private int _version;                  // current data version; bumped by Invalidate
    private bool _promoted;                // false = single buffer (static), true = per-frame ring (animating)
    private bool _drawnOnce;               // a frame has read slot 0 -> a later change must promote, not rewrite in place

    public ReusableBuffer(GpuBufferManager manager, BufferUsageFlags usage, MemoryPropertyFlags memory)
    {
        _manager = manager;
        _usage = usage;
        _memory = memory;
        _ring = new Buffer[manager.RingSize];
        _slotVersion = new int[manager.RingSize];
        Array.Fill(_slotVersion, -1);
    }

    public ulong Capacity => _capacity;

    // Pre-allocate the backing buffer for geometry of this size (call when the geometry is set, so the allocation
    // happens up front rather than on the first draw). Grows, never shrinks.
    public void Reserve(ulong requiredBytes) => EnsureCapacity(requiredBytes);

    // The data changed: bump the version so every slot rewrites lazily. Once the buffer has been drawn, a change also
    // promotes it to a ring - the single buffer may be in flight, so it can't be safely rewritten in place.
    public void Invalidate()
    {
        _version++;
        if (_drawnOnce && !_promoted) Promote();
    }

    // Ensure the current slot holds at least requiredBytes (grow, never shrink) and return it. needsWrite is true when
    // that slot doesn't yet hold the current data version (freshly grown/promoted, or invalidated) - the caller then
    // uploads/re-dispatches into the returned buffer; otherwise it's already current (zero work).
    public Buffer Acquire(ulong requiredBytes, out bool needsWrite)
    {
        EnsureCapacity(requiredBytes);
        var slot = _promoted ? _manager.CurrentFrame : 0u;
        needsWrite = _slotVersion[slot] != _version;
        if (needsWrite) _slotVersion[slot] = _version;
        _drawnOnce = true;
        return _ring[slot];
    }

    private void EnsureCapacity(ulong requiredBytes)
    {
        var needed = RoundUpToBucket(Math.Max(requiredBytes, MinCapacityBytes));
        if (needed <= _capacity && _ring[0] != null) return;   // already allocated and big enough

        // Allocate (or grow) the active slots: just slot 0 while single, the whole ring once promoted. An existing
        // buffer may still be read by an in-flight frame, so defer its disposal to after its fence. Growth is rare
        // (high-water-mark) - about once, at the peak size.
        var slots = _promoted ? _ring.Length : 1;
        for (var i = 0; i < slots; i++)
        {
            if (_ring[i] != null) _manager.Device.AddToDeferDisposeQueue(_ring[i]);
            _ring[i] = Buffer.New(_manager.Device, needed, _usage, _memory);
        }
        _capacity = Math.Max(_capacity, needed);
        Array.Fill(_slotVersion, -1, 0, slots);
    }

    private void Promote()
    {
        _promoted = true;
        // Give every ring slot a FRESH buffer so the per-frame rewrites that follow never touch a buffer a previous
        // frame still reads; defer the old single buffer to after its fence.
        for (var i = 0; i < _ring.Length; i++)
        {
            if (_ring[i] != null) _manager.Device.AddToDeferDisposeQueue(_ring[i]);
            _ring[i] = _capacity > 0 ? Buffer.New(_manager.Device, _capacity, _usage, _memory) : null;
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
