using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Models;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Effects.Generated;
using Adamantium.UI.Rendering.RenderUnits;
using Adamantium.Vulkan.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.UI.Rendering.Retained;

/// <summary>
/// Walk-integrated collector for GENERAL instanced fills (arbitrary tessellated Path/Polygon fills that share one local
/// mesh). Mirrors the SDF batch collectors (<see cref="BatchCollector{T}"/>): instances are COLLECTED during the render
/// walk and FLUSHED in paint order - at a clip change, when an overlapping non-batched unit must paint on top, or at frame
/// end - so an instanced fill lands in its NATURAL z-layer instead of all-at-once under (draw-first, hidden by opaque
/// backgrounds) or over (draw-last, on top of everything) the scene. Collecting in the walk and flushing in paint order
/// is precisely what gives it z-order (a "draw everything at one point" model cannot layer correctly).
/// </summary>
/// <remarks>
/// Per <see cref="GeometryKey"/> it keeps immutable vtx/idx buffers (built once from the shared local mesh) plus a
/// growable per-frame instance buffer (grown only at BeginFrame - the safe point after the frame fence - then filled
/// append-only, so a mid-frame flush of one clip group never clobbers an earlier group's still-recorded draw; the draw
/// uses a firstInstance byte offset). A flush draws each pending key as ONE instanced call, then draws the DEFERRED
/// per-unit fringe/stroke of the collected units ON TOP - preserving fill-under-fringe order (the fill is batched, the
/// analytic-AA fringe / stroke are per-unit and must sit over it). Clean-frame uploads are skipped (retention benefit).
/// Single-threaded (render thread). While no instanceable fill is collected this is inert.
/// </remarks>
internal sealed class InstancedFillCollector : DeferredDisposableObject
{
    // Master toggle for the general-geometry instancing path (off = solid arbitrary-geometry fills draw per-unit).
    public static bool Enabled = true;

    private const MemoryPropertyFlags Mem = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal;
    private static readonly int VertexStride = Marshal.SizeOf<UIVertex>();
    private static readonly int InstanceStride = Marshal.SizeOf<GeometryInstance>();

    // Per-key GPU + per-frame accumulation state. The mesh buffers are immutable once uploaded; the instance buffer is
    // rewritten each frame (grown only at BeginFrame).
    private sealed class KeySegment
    {
        public ReusableBuffer Vtx, Idx;
        public Buffer VtxBuffer, IdxBuffer;
        public uint VertexCount, IndexCount;
        public bool Indexed;
        public PrimitiveType Topology;
        public bool MeshUploaded;

        public GeometryInstance[] Items = new GeometryInstance[64];
        public int Count;        // instances appended this frame (across all this key's flushes)
        public int Flushed;      // instances already drawn this frame (= firstInstance for the next flush)
        public Buffer Gpu;       // instance SSBO (BDA); grown only at BeginFrame
        public int GpuCapacity;
        public bool Recreated;   // grown this frame -> a clean-skip is unsafe (fresh buffer holds no prior bytes)
        public bool InPending;   // currently listed in _pendingKeys
    }

    private readonly GraphicsDevice _device;
    private readonly GpuBufferManager _bufferManager;
    private readonly BatchEffect _effect;
    private readonly Dictionary<GeometryKey, KeySegment> _keys = new();

    // Pending (not-yet-flushed) clip group: the keys with unflushed instances + the units whose fringe/stroke draw AFTER
    // the fills, plus the shared clip and a logical union for the paint-order overlap test.
    private readonly List<KeySegment> _pendingKeys = new();
    private readonly List<IRenderUnit> _pendingUnits = new();
    private Rect2D _scissor;
    private double _uL, _uT, _uR, _uB;
    private bool _hasUnion;

    /// <summary>Set by the caller each frame: true when the scene is provably unchanged (RenderBuildKind.Clean) so the
    /// per-key instance upload is skipped (the retained buffer already holds these exact bytes).</summary>
    public bool SceneClean { get; set; }

    public InstancedFillCollector(IGraphicsDevice device, GpuBufferManager bufferManager) : base(device)
    {
        _device = (GraphicsDevice)device;
        _bufferManager = bufferManager;
        _effect = new BatchEffect(device);
    }

    /// <summary>A pending (not-yet-flushed) clip group exists.</summary>
    public bool Active => _pendingKeys.Count > 0;

    /// <summary>Reset per-frame accumulation. Grows each key's instance buffer here (the safe point: the render runs after
    /// the frame fence, so last frame's GPU reads are done); a new key allocates lazily on its first add.</summary>
    public void BeginFrame()
    {
        foreach (var seg in _keys.Values)
        {
            if (seg.Gpu != null && seg.GpuCapacity < seg.Items.Length)
            {
                seg.Gpu.Dispose();
                seg.Gpu = null;
            }
            if (seg.Gpu == null && seg.MeshUploaded)
            {
                seg.Gpu = Buffer.New<GeometryInstance>(_device, (uint)seg.Items.Length,
                    BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, Mem);
                seg.GpuCapacity = seg.Items.Length;
                seg.Recreated = true;
            }
            else seg.Recreated = false;
            seg.Count = 0;
            seg.Flushed = 0;
            seg.InPending = false;
        }
        _pendingKeys.Clear();
        _pendingUnits.Clear();
        _hasUnion = false;
    }

    /// <summary>True if this unit's fill can join the instanced batch (solid arbitrary geometry with a drawable mesh).</summary>
    public bool CanBatch(GeometryRenderUnit unit) => unit.TryGetInstancedFill(out _, out _, out _);

    /// <summary>Collect one instanceable fill: append its per-instance world+colour to its key's buffer and register the
    /// unit for a deferred fringe/stroke draw. False only if it can't be batched (no drawable mesh, or the instance buffer
    /// overflowed this frame) - the caller then draws that unit per-unit (fill included).</summary>
    public bool TryAdd(GeometryRenderUnit unit, Matrix4x4F world, Rect2D scissor, Rect logicalBounds)
    {
        if (!unit.TryGetInstancedFill(out var key, out var meshObj, out var color)) return false;
        if (meshObj is not Mesh mesh) return false;
        var seg = GetOrCreate(key, mesh);
        if (seg == null) return false;

        if (seg.Count + 1 > seg.Items.Length) Array.Resize(ref seg.Items, seg.Items.Length * 2);
        if (seg.Gpu == null)
        {
            seg.Gpu = Buffer.New<GeometryInstance>(_device, (uint)seg.Items.Length,
                BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, Mem);
            seg.GpuCapacity = seg.Items.Length;
            seg.Recreated = true;
        }
        if (seg.Count + 1 > seg.GpuCapacity) return false;   // this frame's GPU buffer is full -> per-unit fallback

        seg.Items[seg.Count++] = GeometryInstance.FromWorld(world, color);

        _scissor = scissor;
        if (!seg.InPending) { seg.InPending = true; _pendingKeys.Add(seg); }
        _pendingUnits.Add(unit);
        if (!_hasUnion) { _uL = logicalBounds.X; _uT = logicalBounds.Y; _uR = logicalBounds.Right; _uB = logicalBounds.Bottom; _hasUnion = true; }
        else
        {
            if (logicalBounds.X < _uL) _uL = logicalBounds.X;
            if (logicalBounds.Y < _uT) _uT = logicalBounds.Y;
            if (logicalBounds.Right > _uR) _uR = logicalBounds.Right;
            if (logicalBounds.Bottom > _uB) _uB = logicalBounds.Bottom;
        }
        return true;
    }

    /// <summary>Does a unit's logical bounds overlap the pending group? A later overlapping non-batched unit must draw
    /// AFTER a flush so it paints on top.</summary>
    public bool OverlapsPending(Rect r)
        => _hasUnion && r.X < _uR && _uL < r.Right && r.Y < _uB && _uT < r.Bottom;

    /// <summary>Draw the pending clip group: each key's new instances as one instanced call (fills), then the collected
    /// units' fringe/stroke ON TOP (fill-under-fringe). Restores <paramref name="fullScissor"/> for the caller.</summary>
    public void Flush(Rect2D fullScissor, Matrix4x4F projection)
    {
        if (_pendingKeys.Count == 0 && _pendingUnits.Count == 0) return;

        if (_pendingKeys.Count > 0)
        {
            SetupInstancedState(projection);
            _device.SetScissors(_scissor);
            foreach (var seg in _pendingKeys)
            {
                var count = seg.Count - seg.Flushed;
                if (count > 0)
                {
                    // Incremental upload: skip on a clean frame (retained buffer already holds these bytes), but never
                    // on a buffer (re)allocated this frame. Offset the BDA by firstInstance so SV_InstanceID starts at 0.
                    if (!SceneClean || seg.Recreated)
                        seg.Gpu.SetData(seg.Items.AsSpan(seg.Flushed, count), (uint)(seg.Flushed * InstanceStride));
                    _effect.InstancesAddress.SetValue(seg.Gpu.GetDeviceAddress() + (ulong)(seg.Flushed * InstanceStride));
                    _device.SetVertexBuffer(seg.VtxBuffer);
                    _device.PrimitiveTopology = seg.Topology;
                    _effect.BatchFillPass.Apply();
                    if (seg.Indexed)
                        _device.DrawIndexed(seg.VtxBuffer, seg.IdxBuffer, instanceCount: (uint)count, indexCount: seg.IndexCount);
                    else
                        _device.Draw(seg.VertexCount, (uint)count);
                    seg.Flushed = seg.Count;
                }
                seg.InPending = false;
            }
            _device.SetScissors(fullScissor);
        }

        // Deferred fringe/stroke of the collected units, drawn OVER their now-flushed fills, in the same clip. The unit's
        // Render() skips its fill body (FillInstanced) and draws only the analytic-AA fringe + stroke.
        if (_pendingUnits.Count > 0)
        {
            _device.SetScissors(_scissor);
            foreach (var u in _pendingUnits) u.Render();
            _device.SetScissors(fullScissor);
        }

        _pendingKeys.Clear();
        _pendingUnits.Clear();
        _hasUnion = false;
    }

    // Shared InstancedFill device state (all keys draw the same way; only the mesh topology varies).
    private void SetupInstancedState(Matrix4x4F projection)
    {
        _effect.Projection.SetValue(projection);
        _device.VertexType = typeof(UIVertex);
        _device.PolygonMode = PolygonMode.Fill;
        _device.RasterizerDiscardEnabled = false;   // a prior compute pass (fringe/stroke expander) may have left discard ON
        _device.CullMode = CullModeFlagBits.None;    // 2D fills: never cull (tessellated winding is arbitrary)
        _device.ColorBlendEnabled = true;
        _device.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        _device.DepthTestEnabled = true;
        _device.DepthWriteEnable = true;
        _device.DepthCompareFunction = CompareOp.Always;
    }

    // Build (once) the immutable vtx/idx buffers for a key's shared local mesh. Returns null until it has a drawable mesh.
    private KeySegment GetOrCreate(GeometryKey key, Mesh mesh)
    {
        if (_keys.TryGetValue(key, out var seg) && seg.MeshUploaded) return seg;

        var vertices = mesh.ToUIVertices();
        if (vertices.Length == 0) return null;
        var indices = mesh.Indices;
        var indexed = indices is { Length: > 0 };

        seg ??= new KeySegment();
        seg.Vtx ??= ToDispose(_bufferManager.CreateBuffer(BufferUsageFlags.VertexBuffer, Mem));
        seg.VtxBuffer = seg.Vtx.Acquire((ulong)(vertices.Length * VertexStride), out var writeV);
        if (writeV) seg.VtxBuffer.SetData(vertices, 0, (uint)vertices.Length);
        seg.VertexCount = (uint)vertices.Length;

        seg.Indexed = indexed;
        if (indexed)
        {
            seg.Idx ??= ToDispose(_bufferManager.CreateBuffer(BufferUsageFlags.IndexBuffer, Mem));
            seg.IdxBuffer = seg.Idx.Acquire((ulong)(indices.Length * sizeof(int)), out var writeI);
            if (writeI) seg.IdxBuffer.SetData(indices, 0, (uint)indices.Length);
            seg.IndexCount = (uint)indices.Length;
        }

        seg.Topology = mesh.MeshTopology;
        seg.MeshUploaded = true;
        _keys[key] = seg;
        return seg;
    }
}
