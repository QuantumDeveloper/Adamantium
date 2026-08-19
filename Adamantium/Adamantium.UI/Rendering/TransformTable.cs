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
/// <para>The table is held as ONE COPY PER FRAME IN FLIGHT, laid end to end in a single buffer. The frame's copy is
/// chosen by the device's frame index and only that copy is ever written, so a matrix rewrite can never land in memory
/// a frame still on the GPU is reading - BeginDraw's fence proves only frame N-MaxFramesInFlight is done, and the two
/// frames after it are still executing. Writing one shared copy is what made a fast scroll flicker across the WHOLE
/// window: slot indices come from draw order, so a tab crossing the viewport edge shifts every later element's slot,
/// and the still parts of the window were being rewritten under the frames drawing them. Copies cost 64 bytes per slot
/// per frame in flight (~48 KB), and a copy is caught up lazily - only slots whose content actually changed since that
/// copy last saw them are re-sent, so an idle frame still moves zero bytes.</para>
/// </summary>
internal sealed class TransformTable
{
    private const int InitialCapacity = 256;

    /// <summary>One entry: the node's world matrix and its ALPHA, in ONE record so both travel on ONE device address.
    /// A second table would need a second address, and adding one more <c>uint64_t</c> parameter to the batch effect
    /// stopped shader creation outright (measured: the declaration alone, used by nothing, killed startup 3 of 3 while
    /// the same build without it started 3 of 3 - the parameter block is at its limit). They belong together anyway:
    /// same node, same slot, same lifetime, same catch-up.</summary>
    [StructLayout(LayoutKind.Sequential, Size = SlotStride)]
    internal struct NodeSlot
    {
        public Matrix4x4F World;
        public Vector4F Params;   // X = alpha (1 = opaque); YZW reserved. Pads the record to 16-byte alignment.
    }

    private const int SlotStride = 80;

    private NodeSlot[] _cpu = NewSlots(InitialCapacity);

    private int[] _version = new int[InitialCapacity];   // content version of each slot, bumped on every real change
    private int[][] _uploaded;                           // per copy: the version each slot was last sent with
    // One BUFFER per copy, not one buffer holding all copies at different offsets. Same guarantee either way, but a
    // separate allocation is what makes "is this buffer being rewritten before the pipeline drained?" answerable per
    // copy - with a shared allocation every writer looks like it is rewriting the same buffer every frame.
    private Buffer<NodeSlot>[] _gpu;
    private int _gpuCapacity;                            // slots per copy
    private int _copies;                                 // = MaxFramesInFlight
    private int _current;                                // copy this frame writes and draws from
    private int _count;
    private readonly Stack<int> _free = new();
    private readonly Dictionary<Guid, int> _slotByNode = new();   // motion node (component RenderId) -> slot

    /// <summary>Device address of THIS FRAME's copy for the shader's BDA fetch. Valid after <see cref="EnsureResources"/>;
    /// re-read every frame (the copy moves, and a reallocation moves the whole buffer). The batch collectors push it as a
    /// constant on every draw, so a changing address costs nothing.</summary>
    public ulong DeviceAddress => _gpu == null ? 0 : _gpu[_current].GetDeviceAddress();

    /// <summary>Bumped on every matrix that ACTUALLY changed. A replay records this value with its op stream; if it has
    /// moved by the time the stream is re-issued, that frame is drawing instances baked against older matrices.</summary>
    public ulong MatrixVersion { get; private set; }

    /// <summary>The version of matrix writes that did NOT come from the compositor. A recorded op stream survives a
    /// compositor move - the batches read their slot matrix LIVE and the composited per-unit draws are re-pointed at
    /// replay - but it does NOT survive a LAYOUT move, which is baked into the ops themselves. Comparing the two versions
    /// separately is what lets one spinning loader keep animating without forcing the whole window to re-record.</summary>
    public ulong LayoutMatrixVersion { get; private set; }

    /// <summary>Set while the COMPOSITOR is writing (its own matrices for this frame), so those writes do not count as
    /// layout movement.</summary>
    public bool CompositedWrite { get; set; }

    /// <summary>Allocated slot count / GPU capacity (diagnostics).</summary>
    public int SlotCount => _count;
    public int GpuCapacity => _gpuCapacity;

    /// <summary>Makes room for <paramref name="extraSlots"/> more slots BEFORE <see cref="EnsureResources"/> decides the
    /// buffer size. Growth is otherwise discovered while baking, and a slot past the current GPU capacity is never
    /// uploaded that frame (see <see cref="SetMatrix"/>) - the shader still indexes by it and reads past the buffer.
    /// That is harmless when one node appears and the next frame catches up, and ruinous when a clone run asks for
    /// thousands at once: tiles vanished and jumped about as the set changed, differently every frame.</summary>
    public void Reserve(int extraSlots)
    {
        var needed = _count + extraSlots;
        if (needed <= _cpu.Length) return;

        var length = _cpu.Length;
        while (length < needed) length *= 2;

        var was = _cpu.Length;
        Array.Resize(ref _cpu, length);
        Array.Resize(ref _version, length);
        for (var slot = was; slot < length; slot++) _cpu[slot].Params.X = 1f;   // new slots are opaque
    }

    /// <summary>Picks this frame's copy, (re)creates the buffer when capacity outgrew it and catches that copy up with
    /// the changes it missed. Call at a fence-safe point (BeginFrame), before any slot is written or drawn.</summary>
    public void EnsureResources(IGraphicsDevice device)
    {
        var copies = (int)Math.Max(1, device.MaxFramesInFlight);
        if (_gpu == null || _gpuCapacity < _cpu.Length || _copies != copies)
        {
            // Never Dispose here: frames still in flight are reading this buffer. Hand it to the device's deferred queue,
            // which drains it a full pipeline lap later.
            if (_gpu != null)
            {
                foreach (var buffer in _gpu)
                {
                    if (buffer != null) device.AddToDeferDisposeQueue(buffer);
                }
            }
            _copies = copies;
            _gpu = new Buffer<NodeSlot>[_copies];
            _gpuCapacity = _cpu.Length;
            _uploaded = new int[_copies][];
            for (var i = 0; i < _copies; i++)
            {
                _gpu[i] = Adamantium.Graphics.Buffer.New<NodeSlot>(device, (uint)_gpuCapacity,
                    BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                    MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal);
                _uploaded[i] = new int[_gpuCapacity];
                Array.Fill(_uploaded[i], -1);   // nothing in a fresh buffer matches any version
            }
        }

        _current = (int)(device.CurrentFrame % (uint)_copies);
        CatchUpCurrentCopy();
    }

    // Re-send the slots this copy last saw at an older version. On a still frame nothing differs and nothing is sent;
    // after a scroll each of the other copies re-sends the handful of slots that moved, once.
    private void CatchUpCurrentCopy()
    {
        var uploaded = _uploaded[_current];
        for (var slot = 0; slot < _count; slot++)
        {
            if (uploaded[slot] == _version[slot]) continue;
            Upload(slot);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Upload(int slot)
    {
        // Matrix and alpha travel together - one record, one write, one address.
        _gpu[_current].SetData(_cpu.AsSpan(slot, 1), (uint)(slot * SlotStride));
        _uploaded[_current][slot] = _version[slot];
    }

    /// <summary>Sets the alpha this slot multiplies its instances by. Fading a whole node - or one CLONE of a prototype -
    /// is then ONE float write instead of re-baking every instance's colour.</summary>
    public void SetAlpha(IGraphicsDevice device, int slot, float alpha)
    {
        if (_cpu[slot].Params.X != alpha)
        {
            _cpu[slot].Params.X = alpha;
            _version[slot]++;
            MatrixVersion++;
            if (!CompositedWrite) LayoutMatrixVersion++;
        }

        if (_gpu == null || slot >= _gpuCapacity) return;
        if (_uploaded[_current][slot] != _version[slot]) Upload(slot);
    }

    private static NodeSlot[] NewSlots(int length)
    {
        var slots = new NodeSlot[length];
        for (var i = 0; i < length; i++) slots[i].Params.X = 1f;   // a slot nobody fades is opaque
        return slots;
    }

    /// <summary>The slot for <paramref name="nodeId"/>, allocating one if needed (from the free-list, else grown).</summary>
    public int AcquireSlot(Guid nodeId)
    {
        if (_slotByNode.TryGetValue(nodeId, out var slot)) return slot;
        if (_free.Count > 0) slot = _free.Pop();
        else
        {
            slot = _count++;
            if (_count > _cpu.Length) Grow();
        }
        _slotByNode[nodeId] = slot;
        return slot;
    }


    private int AcquireFreeSlot()
    {
        if (_free.Count > 0) return _free.Pop();

        var slot = _count++;
        if (_count > _cpu.Length) Grow();

        return slot;
    }

    // Doubles the table. Growth lived in TWO places, and only one of them made the new slots OPAQUE - so every slot past
    // the initial capacity came out of the other one with alpha 0 and drew its element fully transparent, while the
    // element still answered the mouse. The fill starts at the OLD length, not at _count: the slot being handed out right
    // now IS the first of the new range, and starting a step later left exactly it invisible.
    private void Grow()
    {
        var was = _cpu.Length;
        Array.Resize(ref _cpu, was * 2);   // the GPU side follows at the next EnsureResources
        Array.Resize(ref _version, _cpu.Length);
        for (var slot = was; slot < _cpu.Length; slot++) _cpu[slot].Params.X = 1f;
    }

    /// <summary>Releases a promoted node's slot back to the pool (its instances re-point to an ancestor slot first).</summary>
    public void ReleaseSlot(Guid nodeId)
    {
        if (!_slotByNode.Remove(nodeId, out var slot)) return;
        _free.Push(slot);
    }

    public bool TryGetSlot(Guid nodeId, out int slot) => _slotByNode.TryGetValue(nodeId, out slot);

    /// <summary>Writes one node's world matrix into THIS FRAME's copy - the per-move cost (64 bytes). No frame in flight
    /// reads that copy, so this cannot disturb what is already being drawn.
    /// <para>An UNCHANGED matrix writes nothing. Nothing is world-baked into an instance any more, so every drawn element
    /// resolves a slot on every walk and this would otherwise upload 64 bytes per element per frame - the cost the bake
    /// used to avoid. Skipping the identical write puts it back: a still frame moves zero bytes, and only what actually
    /// moved pays.</para></summary>
    public void SetMatrix(IGraphicsDevice device, int slot, in Matrix4x4F world)
    {
        if (!SameBytes(_cpu[slot].World, world))
        {
            // Record the value FIRST, always. A slot allocated past the current buffer (the table grew mid-frame; the GPU
            // side follows at the next EnsureResources) still has to keep its matrix and bump its version, or the catch-up
            // that runs after the reallocation sees "already up to date" and the node draws with a stale matrix - which is
            // every node past slot 256 the first time a long tab strip pushes the table over its initial capacity.
            _cpu[slot].World = world;
            _version[slot]++;
            MatrixVersion++;
            if (!CompositedWrite) LayoutMatrixVersion++;
        }

        if (_gpu == null || slot >= _gpuCapacity) return;   // no GPU room yet: EnsureResources sends it on the next frame
        if (_uploaded[_current][slot] != _version[slot]) Upload(slot);
    }

    // BYTEWISE, not Matrix4x4F.Equals: that one compares NearEqual, so a sub-pixel scroll step would read as "unchanged"
    // and the write would be skipped - the node would freeze on screen until the step grew past the tolerance.
    private static bool SameBytes(in Matrix4x4F a, in Matrix4x4F b)
        => MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in a), 1))
            .SequenceEqual(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in b), 1)));

    /// <summary>Drops everything (device loss / cache reset).</summary>
    public void Dispose()
    {
        if (_gpu != null)
        {
            foreach (var buffer in _gpu)
            {
                buffer?.Dispose();
            }
        }
        _gpu = null;
        _gpuCapacity = 0;
        _copies = 0;
        _current = 0;
        _uploaded = null;
        _count = 0;
        _free.Clear();
        _slotByNode.Clear();

    }
}
