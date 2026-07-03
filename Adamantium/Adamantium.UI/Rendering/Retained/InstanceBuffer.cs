using System;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// Retained instance store for the render-cache redesign (docs/RENDER_CACHE_REDESIGN.md §4h): a sparse-set / packed
/// array. Live instances are kept DENSE and contiguous in <c>[0, Count)</c> (cache-friendly to iterate + upload to the
/// GPU in one range), while each owner holds a stable <b>handle</b> that maps to the instance's current dense slot.
/// So Add / Patch / Remove are all O(1), and Remove keeps the array packed (swap-remove) without invalidating any
/// OTHER handle. The data-oriented layout is an internal detail behind this handle API (§4k "not at the expense of
/// architecture").
/// </summary>
/// <remarks>
/// Single-threaded (the UI/render thread owns it). Handles are recycled via a free-list, so the handle space stays
/// bounded by the peak live count. A handle is a plain <see cref="int"/>; the owner is trusted not to use a freed one
/// (that is the same contract as a slot index) - <see cref="IsAlive"/> is available for asserts/diagnostics.
/// Touched slots are tracked as a single contiguous dirty range for the GPU upload; <see cref="ClearDirty"/> after the
/// upload. A scattered set of patches over-uploads the spanning range - acceptable and simple; can be refined to a
/// dirty set later if it ever matters.
/// </remarks>
public sealed class InstanceBuffer<T> where T : struct
{
    private const int Free = -1;

    private T[] _dense;              // packed instance data; iterate/upload [0, _count)
    private int[] _denseToHandle;    // dense slot -> owning handle (needed to fix the swapped instance on Remove)
    private int[] _handleToDense;    // handle -> dense slot, or Free
    private int _count;

    private int[] _freeHandles;      // recycled handle ids
    private int _freeCount;
    private int _handleWatermark;    // number of handle ids ever handed out (grows only when the free-list is empty)

    private int _dirtyMin = int.MaxValue;
    private int _dirtyMax = -1;

    public InstanceBuffer(int capacity = 16)
    {
        if (capacity < 1) capacity = 1;
        _dense = new T[capacity];
        _denseToHandle = new int[capacity];
        _handleToDense = new int[capacity];
        _freeHandles = new int[capacity];
    }

    /// <summary>Number of live instances (the dense length).</summary>
    public int Count => _count;

    /// <summary>The packed live instances, in slot order. This is what a draw iterates / uploads.</summary>
    public ReadOnlySpan<T> Span => _dense.AsSpan(0, _count);

    /// <summary>Adds an instance and returns its stable handle.</summary>
    public int Add(in T item)
    {
        EnsureDenseCapacity(_count + 1);
        var handle = AllocHandle();
        var slot = _count++;
        _dense[slot] = item;
        _denseToHandle[slot] = handle;
        _handleToDense[handle] = slot;
        Touch(slot);
        return handle;
    }

    /// <summary>Overwrites the instance behind <paramref name="handle"/> (the O(1) dirty patch).</summary>
    public void Patch(int handle, in T item)
    {
        var slot = _handleToDense[handle];
        _dense[slot] = item;
        Touch(slot);
    }

    /// <summary>The current value of the instance behind <paramref name="handle"/>.</summary>
    public ref readonly T Get(int handle) => ref _dense[_handleToDense[handle]];

    /// <summary>True if <paramref name="handle"/> refers to a live instance (asserts/diagnostics).</summary>
    public bool IsAlive(int handle)
        => handle >= 0 && handle < _handleWatermark && _handleToDense[handle] != Free;

    /// <summary>Removes the instance behind <paramref name="handle"/>, keeping the array packed (swap-remove). The
    /// LAST live instance moves into the freed slot, so ITS handle is silently re-pointed - every other handle,
    /// including that moved one, stays valid.</summary>
    public void Remove(int handle)
    {
        var slot = _handleToDense[handle];
        var last = --_count;
        if (slot != last)
        {
            _dense[slot] = _dense[last];
            var movedHandle = _denseToHandle[last];
            _denseToHandle[slot] = movedHandle;
            _handleToDense[movedHandle] = slot;
            Touch(slot);   // the moved instance now lives at `slot` on the GPU too
        }
        _handleToDense[handle] = Free;
        FreeHandle(handle);
    }

    // ---- GPU-upload dirty range (contiguous span of touched slots since the last ClearDirty) -----------------------

    /// <summary>Any slot was added/patched/moved since the last <see cref="ClearDirty"/>.</summary>
    public bool HasDirty => _dirtyMax >= _dirtyMin;

    /// <summary>First dirty slot (valid only when <see cref="HasDirty"/>).</summary>
    public int DirtyStart => _dirtyMin;

    /// <summary>Number of slots in the dirty range (0 when nothing is dirty). Clamped to the live count.</summary>
    public int DirtyCount => HasDirty ? Math.Min(_dirtyMax, _count - 1) - _dirtyMin + 1 : 0;

    /// <summary>Call after uploading the dirty range to the GPU.</summary>
    public void ClearDirty()
    {
        _dirtyMin = int.MaxValue;
        _dirtyMax = -1;
    }

    private void Touch(int slot)
    {
        if (slot < _dirtyMin) _dirtyMin = slot;
        if (slot > _dirtyMax) _dirtyMax = slot;
    }

    // ---- capacity / handle recycling -------------------------------------------------------------------------------

    private void EnsureDenseCapacity(int needed)
    {
        if (needed <= _dense.Length) return;
        var len = _dense.Length;
        while (len < needed) len *= 2;
        Array.Resize(ref _dense, len);
        Array.Resize(ref _denseToHandle, len);
    }

    private int AllocHandle()
    {
        if (_freeCount > 0) return _freeHandles[--_freeCount];
        var handle = _handleWatermark++;
        if (handle >= _handleToDense.Length)
            Array.Resize(ref _handleToDense, _handleToDense.Length * 2);
        return handle;
    }

    private void FreeHandle(int handle)
    {
        if (_freeCount >= _freeHandles.Length)
            Array.Resize(ref _freeHandles, _freeHandles.Length * 2);
        _freeHandles[_freeCount++] = handle;
    }
}
