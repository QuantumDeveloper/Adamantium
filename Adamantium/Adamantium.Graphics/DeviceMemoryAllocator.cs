using System;
using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics;

/// <summary>
/// Persistent GPU memory sub-allocator (VMA-style). Vulkan caps the number of live <c>vkAllocateMemory</c> allocations
/// (<c>maxMemoryAllocationCount</c>, guaranteed as low as 4096); the old model gave every <see cref="Buffer"/> its own
/// dedicated allocation, so a few thousand small per-fill buffers (the analytic-AA fringe rents two BDA buffers per
/// contour) exhausted that limit and threw <c>ErrorOutOfDeviceMemory</c> even though the bytes were tiny. This allocator
/// carves buffer memory out of a handful of large shared blocks instead: N small buffers collapse to a few allocations.
///
/// Blocks are grouped by (memory-type index, needs-device-address) - a block's <c>DeviceAddressBit</c> alloc flag is set
/// only for the BDA group. A host-visible block is mapped ONCE for its whole life and every sub-allocation writes through
/// <c>MappedBase + offset</c>, which both sidesteps Vulkan's one-map-per-<c>VkDeviceMemory</c> rule and skips a map/unmap
/// syscall per upload. Buffer-device-address stays correct because <c>vkGetBufferDeviceAddress</c> reports the address for
/// the buffer wherever it is bound (its bind offset included). Images are NOT routed here (buffer-image granularity, and
/// they are few + large); this is buffers only.
/// </summary>
public sealed class DeviceMemoryAllocator : IDisposable
{
    // 64 MB blocks in a big VRAM heap, but never more than heapSize/8 so the tiny (~214 MB) host-visible BAR window still
    // fits several blocks. A single allocation larger than the block size gets its own exact-size dedicated block.
    private const ulong DefaultBlockSize = 64UL * 1024 * 1024;
    private const ulong MinBlockSize = 4UL * 1024 * 1024;

    private readonly GraphicsDevice _device;
    private readonly object _lock = new();
    private readonly Dictionary<int, List<Block>> _groups = new();   // key = GroupKey(memoryTypeIndex, deviceAddress)
    private readonly ulong[] _heapSizeByType;   // heap byte size behind each memory-type index (block-size cap)

    public DeviceMemoryAllocator(GraphicsDevice device)
    {
        _device = device;
        var memProps = device.Adapter.Adapter.GetPhysicalDeviceMemoryProperties();
        _heapSizeByType = new ulong[memProps.MemoryTypeCount];
        for (var t = 0; t < memProps.MemoryTypeCount; t++)
        {
            var heapIndex = memProps.MemoryTypes.Span[t].HeapIndex;
            _heapSizeByType[t] = memProps.MemoryHeaps.Span[(int)heapIndex].Size;
        }
    }

    /// <summary>Reserves <paramref name="size"/> bytes (aligned to <paramref name="alignment"/>) inside a shared block of
    /// the given memory type, allocating a new block only when no existing one has room. <paramref name="hostVisible"/>
    /// blocks are persistently mapped; <paramref name="deviceAddress"/> selects a block allocated with the BDA flag.</summary>
    public MemoryAllocation Allocate(ulong size, ulong alignment, uint memoryTypeIndex, bool hostVisible, bool deviceAddress)
    {
        if (size == 0) size = 1;
        if (alignment == 0) alignment = 1;
        size = AlignUp(size, alignment);

        lock (_lock)
        {
            var key = GroupKey(memoryTypeIndex, deviceAddress);
            if (!_groups.TryGetValue(key, out var blocks))
            {
                blocks = [];
                _groups[key] = blocks;
            }

            foreach (var block in blocks)
                if (TryCarve(block, size, alignment, out var offset))
                    return MakeAllocation(block, offset, size);

            // No block in this group has room -> allocate a new one (at least big enough for this request).
            var heapCap = Math.Max(MinBlockSize, _heapSizeByType[memoryTypeIndex] / 8);
            var blockSize = Math.Max(Math.Min(DefaultBlockSize, heapCap), size);
            var fresh = CreateBlock(blockSize, memoryTypeIndex, hostVisible, deviceAddress);
            blocks.Add(fresh);
            if (!TryCarve(fresh, size, alignment, out var freshOffset))
                throw new GraphicsEngineException("Fresh GPU memory block could not satisfy its own allocation");
            return MakeAllocation(fresh, freshOffset, size);
        }
    }

    /// <summary>Returns a sub-allocation's range to its block's free list (coalescing neighbours). The block itself is
    /// retained for reuse - blocks are freed only on <see cref="Dispose"/>.</summary>
    public void Free(MemoryAllocation allocation)
    {
        if (allocation?.Block == null) return;
        lock (_lock)
        {
            InsertFree(allocation.Block, allocation.Offset, allocation.Size);
            allocation.Block = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var blocks in _groups.Values)
                foreach (var block in blocks)
                {
                    if (block.MappedBase != 0) _device.UnmapMemory(block.Memory);
                    _device.Destroy(block.Memory);
                }
            _groups.Clear();
        }
    }

    private MemoryAllocation MakeAllocation(Block block, ulong offset, ulong size) => new()
    {
        Block = block,
        Memory = block.Memory,
        Offset = offset,
        Size = size,
        MappedBase = block.MappedBase == 0 ? 0 : block.MappedBase + (nuint)offset,
    };

    private Block CreateBlock(ulong size, uint memoryTypeIndex, bool hostVisible, bool deviceAddress)
    {
        var allocInfo = new MemoryAllocateInfo { AllocationSize = size, MemoryTypeIndex = memoryTypeIndex };
        if (deviceAddress)
            allocInfo.PNext = new MemoryAllocateFlagsInfo { Flags = MemoryAllocateFlagBits.DeviceAddressBit };

        var memory = _device.LogicalDevice.AllocateMemory(allocInfo);
        // Host-visible blocks are mapped once, for life: sub-allocations write via MappedBase + offset (a VkDeviceMemory
        // can only be mapped once at a time, and the used host-visible types are HOST_COHERENT - the write path assumes
        // coherence, matching the pre-existing Buffer upload code, so no flush is needed).
        nuint mappedBase = hostVisible ? _device.MapMemory(memory, 0, size, 0) : 0;

        return new Block { Memory = memory, Size = size, MappedBase = mappedBase, Free = [new FreeRange(0, size)] };
    }

    // Find the first free range that fits (respecting alignment); carve [alignedOffset, alignedOffset+size) out of it,
    // leaving the alignment pad and the tail as (smaller) free ranges. First-fit is fine for the modest UI churn.
    private static bool TryCarve(Block block, ulong size, ulong alignment, out ulong dataOffset)
    {
        var free = block.Free;
        for (var i = 0; i < free.Count; i++)
        {
            var r = free[i];
            var aligned = AlignUp(r.Offset, alignment);
            var pad = aligned - r.Offset;
            if (pad + size > r.Size) continue;   // doesn't fit here (including alignment padding)

            dataOffset = aligned;
            var tailOffset = aligned + size;
            var tailSize = r.Offset + r.Size - tailOffset;
            free.RemoveAt(i);
            if (tailSize > 0) free.Insert(i, new FreeRange(tailOffset, tailSize));
            if (pad > 0) free.Insert(i, new FreeRange(r.Offset, pad));
            return true;
        }
        dataOffset = 0;
        return false;
    }

    // Insert [offset, offset+size) back into the sorted free list, merging with an adjacent range on either side.
    private static void InsertFree(Block block, ulong offset, ulong size)
    {
        var free = block.Free;
        var i = 0;
        while (i < free.Count && free[i].Offset < offset) i++;
        free.Insert(i, new FreeRange(offset, size));

        if (i > 0 && free[i - 1].Offset + free[i - 1].Size == free[i].Offset)   // merge with previous
        {
            free[i - 1] = new FreeRange(free[i - 1].Offset, free[i - 1].Size + free[i].Size);
            free.RemoveAt(i);
            i--;
        }
        if (i + 1 < free.Count && free[i].Offset + free[i].Size == free[i + 1].Offset)   // merge with next
        {
            free[i] = new FreeRange(free[i].Offset, free[i].Size + free[i + 1].Size);
            free.RemoveAt(i + 1);
        }
    }

    private static ulong AlignUp(ulong value, ulong alignment) => (value + alignment - 1) & ~(alignment - 1);

    private static int GroupKey(uint memoryTypeIndex, bool deviceAddress) => (int)memoryTypeIndex * 2 + (deviceAddress ? 1 : 0);

    internal sealed class Block
    {
        public DeviceMemory Memory;
        public ulong Size;
        public nuint MappedBase;   // persistent map base, or 0 for a device-local (non-host-visible) block
        public List<FreeRange> Free;
    }

    internal readonly record struct FreeRange(ulong Offset, ulong Size);

    /// <summary>Handle to one sub-allocation: the shared block memory, the byte offset the buffer is bound at, and (for a
    /// host-visible block) the mapped CPU pointer to that offset. Returned by <see cref="Allocate"/>, passed to <see cref="Free"/>.</summary>
    public sealed class MemoryAllocation
    {
        internal Block Block;
        public DeviceMemory Memory;
        public ulong Offset;
        public ulong Size;
        public nuint MappedBase;   // Block.MappedBase + Offset, or 0 if the block is not host-visible
    }
}
