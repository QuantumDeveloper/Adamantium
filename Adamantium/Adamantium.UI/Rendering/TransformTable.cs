using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

/// <summary>
/// GPU-resident transform table: one world <see cref="Matrix4x4F"/> per MOTION NODE - an element whose subtree moves
/// independently (a scrolled panel, an animating tile). Batch instances carry local-space geometry plus a SLOT INDEX
/// into this table; the vertex shader fetches the matrix by index (BDA) and transforms. Moving a node then costs ONE
/// 64-byte slot write (a scroll = the panel's slot) - the instance buffer is untouched until actual GEOMETRY changes,
/// and rotated/3D nodes stay batched (the shader applies a full matrix, not an axis-aligned bake).
/// Slots are pooled: at-rest elements share their nearest motion ancestor's slot; an element PROMOTES to its own slot
/// when it starts moving independently and releases it when done (the tilt/flip tiles).
/// </summary>
internal sealed class TransformTable
{
    private const int InitialCapacity = 256;

    private Matrix4x4F[] _cpu = new Matrix4x4F[InitialCapacity];
    private Buffer<Matrix4x4F> _gpu;
    private int _gpuCapacity;
    private int _count;
    private readonly Stack<int> _free = new();
    private readonly Dictionary<Guid, int> _slotByNode = new();   // motion node (component RenderId) -> slot

    /// <summary>Device address of the matrix table for the shader's BDA fetch. Valid after <see cref="EnsureResources"/>.</summary>
    public ulong DeviceAddress => _gpu?.GetDeviceAddress() ?? 0;

    /// <summary>Allocated slot count / GPU capacity (diagnostics).</summary>
    public int SlotCount => _count;
    public int GpuCapacity => _gpuCapacity;

    /// <summary>(Re)creates the GPU buffer when capacity outgrew it. Call at a fence-safe point (BeginFrame); a
    /// (re)allocation re-uploads the whole live table (rare - capacity only doubles).</summary>
    public void EnsureResources(IGraphicsDevice device)
    {
        if (_gpu != null && _gpuCapacity >= _cpu.Length) return;
        _gpu?.Dispose();
        _gpu = Adamantium.Graphics.Buffer.New<Matrix4x4F>(device, (uint)_cpu.Length,
            BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
            MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal);
        _gpuCapacity = _cpu.Length;
        if (_count > 0) _gpu.SetData(_cpu.AsSpan(0, _count), 0);
    }

    /// <summary>The slot for <paramref name="nodeId"/>, allocating one if needed (from the free-list, else grown).</summary>
    public int AcquireSlot(Guid nodeId)
    {
        if (_slotByNode.TryGetValue(nodeId, out var slot)) return slot;
        if (_free.Count > 0) slot = _free.Pop();
        else
        {
            slot = _count++;
            if (_count > _cpu.Length) Array.Resize(ref _cpu, _cpu.Length * 2);   // GPU grows at next EnsureResources
        }
        _slotByNode[nodeId] = slot;
        return slot;
    }

    /// <summary>Releases a promoted node's slot back to the pool (its instances re-point to an ancestor slot first).</summary>
    public void ReleaseSlot(Guid nodeId)
    {
        if (_slotByNode.Remove(nodeId, out var slot)) _free.Push(slot);
    }

    public bool TryGetSlot(Guid nodeId, out int slot) => _slotByNode.TryGetValue(nodeId, out slot);

    /// <summary>Writes one node's world matrix - THE per-move cost (64 bytes). Safe only when the slot's previous value
    /// is no longer read by an in-flight frame or the change is what this frame draws (call from the build phase).
    /// <para>An UNCHANGED matrix writes nothing. Nothing is world-baked into an instance any more, so every drawn element
    /// resolves a slot on every walk and this would otherwise upload 64 bytes per element per frame - the cost the bake
    /// used to avoid. Skipping the identical write puts it back: a still frame moves zero bytes, and only what actually
    /// moved pays.</para></summary>
    public void SetMatrix(IGraphicsDevice device, int slot, in Matrix4x4F world)
    {
        if (SameBytes(_cpu[slot], world)) return;
        _cpu[slot] = world;
        if (_gpu != null && slot < _gpuCapacity) _gpu.SetData(_cpu.AsSpan(slot, 1), (uint)(slot * 64));
    }

    // BYTEWISE, not Matrix4x4F.Equals: that one compares NearEqual, so a sub-pixel scroll step would read as "unchanged"
    // and the write would be skipped - the node would freeze on screen until the step grew past the tolerance.
    private static bool SameBytes(in Matrix4x4F a, in Matrix4x4F b)
        => MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in a), 1))
            .SequenceEqual(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in b), 1)));

    /// <summary>Drops everything (device loss / cache reset).</summary>
    public void Dispose()
    {
        _gpu?.Dispose();
        _gpu = null;
        _gpuCapacity = 0;
        _count = 0;
        _free.Clear();
        _slotByNode.Clear();
    }
}
