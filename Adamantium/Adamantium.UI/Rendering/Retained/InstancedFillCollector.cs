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
using Adamantium.UI.Core.Media;
using Adamantium.UI.Effects.Generated;
using Adamantium.UI.Rendering;
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
    private static readonly int RingStride = Marshal.SizeOf<FringeVertex>();
    private static readonly int InstanceStride = Marshal.SizeOf<GeometryInstance>();
    private static readonly int GradInstanceStride = Marshal.SizeOf<GradientGeometryInstance>();
    private static readonly int PatInstanceStride = Marshal.SizeOf<PatternGeometryInstance>();

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

        // The shared analytic-AA ring for this key's mesh (built once with the mesh - see FrozenMesh.Ring). Every
        // instance draws it with the SAME instance buffer as the body, so N elements cost one fringe draw instead of N.
        public ReusableBuffer Ring;
        public Buffer RingBuffer;
        public uint RingVertexCount;

        public GeometryInstance[] Items = new GeometryInstance[64];
        public int Count;        // instances appended this frame (across all this key's flushes)
        public int Flushed;      // instances already drawn this frame (= firstInstance for the next flush)
        public Buffer Gpu;       // instance SSBO (BDA); grown only at BeginFrame
        public int GpuCapacity;
        public bool Recreated;   // grown this frame -> a clean-skip is unsafe (fresh buffer holds no prior bytes)
        public bool InPending;   // currently listed in _pendingKeys

        // Parallel GRADIENT instance state for this key's shared mesh (a gradient fill on the same geometry). Solid and
        // gradient instances of one mesh share the vtx/idx buffers but have separate instance buffers + draw passes.
        public GradientGeometryInstance[] GradItems = new GradientGeometryInstance[16];
        public int GradCount;
        public int GradFlushed;
        public Buffer GradGpu;
        public int GradGpuCapacity;
        public bool GradRecreated;

        // Parallel PATTERN/NOISE instance state for this key's shared mesh (a procedural fill on the same geometry).
        public PatternGeometryInstance[] PatItems = new PatternGeometryInstance[16];
        public int PatCount;
        public int PatFlushed;
        public Buffer PatGpu;
        public int PatGpuCapacity;
        public bool PatRecreated;
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

    // One clip group's draw, retained for the clean-frame op replay (so retained-draw covers instanced fills too, not
    // just the SDF/text batches - otherwise a single vector icon on screen disables replay for the whole window). Each
    // Flush records its key-draws (buffer range per shared mesh) + the deferred fringe/stroke units + the clip; a Clean
    // frame re-issues them via ReplayFlush with NO re-upload (the retained buffers still hold the bytes). Objects are
    // pooled (reset, not reallocated) across frames.
    private sealed class FlushRecord
    {
        public readonly List<(KeySegment Seg, uint First, uint Count)> Keys = new();
        public readonly List<(KeySegment Seg, uint First, uint Count)> GradKeys = new();
        public readonly List<(KeySegment Seg, uint First, uint Count)> PatKeys = new();
        public readonly List<IRenderUnit> Units = new();
        public Rect2D Scissor;
        public void Reset() { Keys.Clear(); GradKeys.Clear(); PatKeys.Clear(); Units.Clear(); }
    }
    private readonly List<FlushRecord> _flushRecords = new();
    private int _flushCount;   // records used this frame (pooled objects reused up to this count)

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

    // --- test observability -------------------------------------------------------------------------------------------
    // The whole point of this collector is that identical geometry COLLAPSES into one shared mesh + one instanced draw,
    // and that a full instance buffer falls back per-unit instead of dropping a shape. Neither is visible from the public
    // surface (both live in the per-key segments), so expose just enough to assert them.
    internal int SegmentCount => _keys.Count;
    internal int PendingKeyCount => _pendingKeys.Count;
    internal int InstanceCountOf(GeometryKey key) => _keys.TryGetValue(key, out var seg) ? seg.Count : 0;
    internal int GpuCapacityOf(GeometryKey key) => _keys.TryGetValue(key, out var seg) ? seg.GpuCapacity : 0;

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

            // Parallel grow/reset for the gradient instance buffer (only when this key has ever held gradient instances).
            if (seg.GradGpu != null && seg.GradGpuCapacity < seg.GradItems.Length)
            {
                seg.GradGpu.Dispose();
                seg.GradGpu = null;
            }
            if (seg.GradGpu == null && seg.MeshUploaded && seg.GradCount > 0)
            {
                seg.GradGpu = Buffer.New<GradientGeometryInstance>(_device, (uint)seg.GradItems.Length,
                    BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, Mem);
                seg.GradGpuCapacity = seg.GradItems.Length;
                seg.GradRecreated = true;
            }
            else seg.GradRecreated = false;

            // Parallel grow/reset for the pattern instance buffer (only when this key has ever held pattern instances).
            if (seg.PatGpu != null && seg.PatGpuCapacity < seg.PatItems.Length)
            {
                seg.PatGpu.Dispose();
                seg.PatGpu = null;
            }
            if (seg.PatGpu == null && seg.MeshUploaded && seg.PatCount > 0)
            {
                seg.PatGpu = Buffer.New<PatternGeometryInstance>(_device, (uint)seg.PatItems.Length,
                    BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, Mem);
                seg.PatGpuCapacity = seg.PatItems.Length;
                seg.PatRecreated = true;
            }
            else seg.PatRecreated = false;

            seg.Count = 0;
            seg.Flushed = 0;
            seg.GradCount = 0;
            seg.GradFlushed = 0;
            seg.PatCount = 0;
            seg.PatFlushed = 0;
            seg.InPending = false;
        }
        _pendingKeys.Clear();
        _pendingUnits.Clear();
        _hasUnion = false;
        _flushCount = 0;   // pooled flush records reused from index 0 this frame
    }

    /// <summary>True if this unit's fill can join the instanced batch (solid arbitrary geometry with a drawable mesh).</summary>
    public bool CanBatch(GeometryRenderUnit unit) => unit.TryGetInstancedFill(out _, out _, out _);

    /// <summary>Collect one instanceable fill: append its per-instance world+colour to its key's buffer and register the
    /// unit for a deferred fringe/stroke draw. False only if it can't be batched (no drawable mesh, or the instance buffer
    /// overflowed this frame) - the caller then draws that unit per-unit (fill included).</summary>
    public bool TryAdd(GeometryRenderUnit unit, Matrix4x4F local, Rect2D scissor, Rect logicalBounds, int transformSlot)
    {
        if (!unit.TryGetInstancedFill(out var key, out var meshObj, out var color)) return false;
        if (meshObj is not FrozenMesh mesh) return false;
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

        seg.Items[seg.Count++] = GeometryInstance.FromLocal(local, color, transformSlot);

        _scissor = scissor;
        if (!seg.InPending) { seg.InPending = true; _pendingKeys.Add(seg); }
        AddPendingUnit(unit);
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

    /// <summary>True if this unit's fill can join the GRADIENT instanced batch (arbitrary geometry with a gradient fill).</summary>
    public bool CanBatchGradient(GeometryRenderUnit unit) => unit.TryGetInstancedGradientFill(out _, out _, out _, out _, out _);

    /// <summary>Collect one instanceable GRADIENT fill: append its per-instance world + gradient to its key's gradient
    /// buffer, and register the unit for a deferred fringe/stroke draw. False if it can't be batched (no drawable mesh or
    /// buffer overflow) - the caller draws it per-unit.</summary>
    public bool TryAddGradient(GeometryRenderUnit unit, Matrix4x4F world, Rect2D scissor, Rect logicalBounds)
    {
        if (!unit.TryGetInstancedGradientFill(out var key, out var meshObj, out var brush, out var localBounds, out var opacity)) return false;
        if (meshObj is not FrozenMesh mesh) return false;
        var seg = GetOrCreate(key, mesh);
        if (seg == null) return false;

        if (seg.GradCount + 1 > seg.GradItems.Length) Array.Resize(ref seg.GradItems, seg.GradItems.Length * 2);
        if (seg.GradGpu == null)
        {
            seg.GradGpu = Buffer.New<GradientGeometryInstance>(_device, (uint)seg.GradItems.Length,
                BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, Mem);
            seg.GradGpuCapacity = seg.GradItems.Length;
            seg.GradRecreated = true;
        }
        if (seg.GradCount + 1 > seg.GradGpuCapacity) return false;

        seg.GradItems[seg.GradCount++] = BuildGradientInstance(brush, world, localBounds, opacity);

        _scissor = scissor;
        if (!seg.InPending) { seg.InPending = true; _pendingKeys.Add(seg); }
        AddPendingUnit(unit);
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

    // Pack a gradient brush + world + local bounds into one gradient instance record (stops/geometry via the shared
    // GradientBake; Params = (type, spread, stopCount, _) to match the gradient-fill vertex shader).
    private static GradientGeometryInstance BuildGradientInstance(GradientBrush g, Matrix4x4F world, Rect localBounds, double opacity)
    {
        var inst = new GradientGeometryInstance { World = world };
        var alpha = (float)(g.Opacity * opacity);
        Span<Vector4F> cols = stackalloc Vector4F[GradientBake.MaxStops];
        Span<float> offs = stackalloc float[GradientBake.MaxStops];
        var count = GradientBake.PackStops(g, alpha, cols, offs);
        inst.Stop0 = cols[0]; inst.Stop1 = cols[1]; inst.Stop2 = cols[2]; inst.Stop3 = cols[3];
        inst.Stop4 = cols[4]; inst.Stop5 = cols[5]; inst.Stop6 = cols[6]; inst.Stop7 = cols[7];
        inst.Offsets0 = new Vector4F(offs[0], offs[1], offs[2], offs[3]);
        inst.Offsets1 = new Vector4F(offs[4], offs[5], offs[6], offs[7]);
        var type = GradientBake.PackGeometry(g, out var geom0, out var geom1);
        inst.Geom0 = geom0;
        inst.Geom1 = geom1;
        inst.LocalBounds = new Vector4F((float)localBounds.X, (float)localBounds.Y, (float)localBounds.Width, (float)localBounds.Height);
        inst.Params = new Vector4F(type, (float)g.SpreadMethod, count, (float)g.ColorInterpolationMode);   // .w = interp mode (0 sRGB/1 OKLab)
        return inst;
    }

    public bool CanBatchPattern(GeometryRenderUnit unit) => unit.TryGetInstancedPatternFill(out _, out _, out _, out _, out _);

    /// <summary>Collect one instanceable PATTERN/NOISE fill: append its per-instance world + pattern to its key's pattern
    /// buffer, and register the unit for a deferred fringe/stroke draw. False if it can't be batched.</summary>
    public bool TryAddPattern(GeometryRenderUnit unit, Matrix4x4F world, Rect2D scissor, Rect logicalBounds)
    {
        if (!unit.TryGetInstancedPatternFill(out var key, out var meshObj, out var brush, out var localBounds, out var opacity)) return false;
        if (meshObj is not FrozenMesh mesh) return false;
        var seg = GetOrCreate(key, mesh);
        if (seg == null) return false;

        if (seg.PatCount + 1 > seg.PatItems.Length) Array.Resize(ref seg.PatItems, seg.PatItems.Length * 2);
        if (seg.PatGpu == null)
        {
            seg.PatGpu = Buffer.New<PatternGeometryInstance>(_device, (uint)seg.PatItems.Length,
                BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress, Mem);
            seg.PatGpuCapacity = seg.PatItems.Length;
            seg.PatRecreated = true;
        }
        if (seg.PatCount + 1 > seg.PatGpuCapacity) return false;

        seg.PatItems[seg.PatCount++] = BuildPatternInstance(brush, world, localBounds, opacity);

        _scissor = scissor;
        if (!seg.InPending) { seg.InPending = true; _pendingKeys.Add(seg); }
        AddPendingUnit(unit);
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

    // Pack a PatternBrush/NoiseBrush + world + local bounds into one pattern instance record. Cell stays in LOCAL units (the
    // geometry PS works in local mesh coords) - no device-scale, unlike the SDF rect bake. Mirrors PatternRectCollector.BakeItem.
    private static PatternGeometryInstance BuildPatternInstance(Brush brush, Matrix4x4F world, Rect localBounds, double opacity)
    {
        var inst = new PatternGeometryInstance { World = world };
        int type;
        Color color1;
        Color color2;
        var midColor = new Color(0, 0, 0, 0);
        double cell;
        double brushOpacity;
        var noise = Vector4F.Zero;

        if (brush is PatternBrush pat)
        {
            type = (int)pat.Pattern;
            color1 = pat.Color1;
            color2 = pat.Color2;
            cell = pat.CellSize;
            brushOpacity = pat.Opacity;
        }
        else
        {
            var n = (NoiseBrush)brush;
            type = n.NoiseType == NoiseType.Simplex ? 4 : 6 + (int)n.NoiseType;
            color1 = n.Color1;
            color2 = n.Color2;
            midColor = n.MidColor;
            cell = n.Scale;
            brushOpacity = n.Opacity;
            var octEnc = n.Animate ? -(float)Math.Max(1, n.Octaves) : n.Octaves;
            noise = new Vector4F(octEnc, (float)n.Seed, (float)n.Lacunarity, (float)n.Gain);
            if (n.NoiseType == NoiseType.CombustibleVoronoi) noise.W = n.UseFirePalette ? 1f : 0f;
        }

        var alpha = (float)(opacity * brushOpacity);
        var c1 = color1.ToVector4(); c1.W *= alpha;
        var c2 = color2.ToVector4(); c2.W *= alpha;
        var c3 = midColor.ToVector4(); c3.W *= alpha;

        inst.Params = new Vector4F(0, type, (float)cell, 0);
        inst.LocalBounds = new Vector4F((float)localBounds.X, (float)localBounds.Y, (float)localBounds.Width, (float)localBounds.Height);
        inst.Color1 = c1;
        inst.Color2 = c2;
        inst.Color3 = c3;
        inst.Noise = noise;
        return inst;
    }

    // Register a collected unit for the deferred per-unit draw - but ONLY if it still has something to draw there. A
    // solid fill whose fringe is instanced too has nothing left (its body is skipped via FillInstanced), and that empty
    // Render() per element is exactly the cost this path exists to remove.
    private void AddPendingUnit(GeometryRenderUnit unit)
    {
        if (!unit.HasPerUnitOverlay) return;
        _pendingUnits.Add(unit);
    }

    /// <summary>Does a unit's logical bounds overlap the pending group? A later overlapping non-batched unit must draw
    /// AFTER a flush so it paints on top.</summary>
    public bool OverlapsPending(Rect r)
        => _hasUnion && r.X < _uR && _uL < r.Right && r.Y < _uB && _uT < r.Bottom;

    /// <summary>Draw the pending clip group: each key's new instances as one instanced call (fills), then the collected
    /// units' fringe/stroke ON TOP (fill-under-fringe). Records the group into a pooled <see cref="FlushRecord"/> and
    /// draws THROUGH it (so the immediate draw and a later clean-frame replay share one path). Returns the record index
    /// for the op stream, or -1 if nothing was pending. Restores <paramref name="fullScissor"/> for the caller.</summary>
    public int Flush(Rect2D fullScissor, Matrix4x4F projection)
    {
        if (_pendingKeys.Count == 0 && _pendingUnits.Count == 0) return -1;

        // Record this group's draws + upload each key's new instances NOW (recording frame). The record captures the
        // buffer range (first,count) per key so replay re-issues the exact same draw with no upload.
        var rec = _flushCount < _flushRecords.Count ? _flushRecords[_flushCount] : AddFlushRecord();
        _flushCount++;
        rec.Reset();
        rec.Scissor = _scissor;

        foreach (var seg in _pendingKeys)
        {
            var count = seg.Count - seg.Flushed;
            if (count > 0)
            {
                // Incremental upload: skip on a clean frame (retained buffer already holds these bytes), but never on a
                // buffer (re)allocated this frame. Offset the BDA by firstInstance so SV_InstanceID starts at 0.
                if (!SceneClean || seg.Recreated)
                    seg.Gpu.SetData(seg.Items.AsSpan(seg.Flushed, count), (uint)(seg.Flushed * InstanceStride));
                rec.Keys.Add((seg, (uint)seg.Flushed, (uint)count));
                seg.Flushed = seg.Count;
            }
            var gcount = seg.GradCount - seg.GradFlushed;
            if (gcount > 0)
            {
                if (!SceneClean || seg.GradRecreated)
                    seg.GradGpu.SetData(seg.GradItems.AsSpan(seg.GradFlushed, gcount), (uint)(seg.GradFlushed * GradInstanceStride));
                rec.GradKeys.Add((seg, (uint)seg.GradFlushed, (uint)gcount));
                seg.GradFlushed = seg.GradCount;
            }
            var pcount = seg.PatCount - seg.PatFlushed;
            if (pcount > 0)
            {
                if (!SceneClean || seg.PatRecreated)
                    seg.PatGpu.SetData(seg.PatItems.AsSpan(seg.PatFlushed, pcount), (uint)(seg.PatFlushed * PatInstanceStride));
                rec.PatKeys.Add((seg, (uint)seg.PatFlushed, (uint)pcount));
                seg.PatFlushed = seg.PatCount;
            }
            seg.InPending = false;
        }
        foreach (var u in _pendingUnits) rec.Units.Add(u);

        _pendingKeys.Clear();
        _pendingUnits.Clear();
        _hasUnion = false;

        DrawFlushRecord(rec, fullScissor, projection);
        return _flushCount - 1;
    }

    /// <summary>Re-issue a flush recorded earlier this cycle (a clean-frame replay): same key draws + deferred unit
    /// fringe/stroke, NO re-upload (the retained instance buffers still hold the bytes).</summary>
    public void ReplayFlush(int index, Rect2D fullScissor, Matrix4x4F projection)
        => DrawFlushRecord(_flushRecords[index], fullScissor, projection);

    private FlushRecord AddFlushRecord() { var r = new FlushRecord(); _flushRecords.Add(r); return r; }

    private void DrawFlushRecord(FlushRecord rec, Rect2D fullScissor, Matrix4x4F projection)
    {
        if (rec.Keys.Count > 0)
        {
            SetupInstancedState(projection);
            _device.SetScissors(rec.Scissor);
            foreach (var (seg, first, count) in rec.Keys)
            {
                _effect.InstancesAddress.SetValue(seg.Gpu.GetDeviceAddress() + (ulong)(first * InstanceStride));
                _device.SetVertexBuffer(seg.VtxBuffer);
                _device.PrimitiveTopology = seg.Topology;
                _effect.BatchFillPass.Apply();
                if (seg.Indexed)
                    _device.DrawIndexed(seg.VtxBuffer, seg.IdxBuffer, instanceCount: count, indexCount: seg.IndexCount);
                else
                    _device.Draw(seg.VertexCount, count);
            }
            _device.SetScissors(fullScissor);
        }

        // The analytic-AA fringe of those same instances: the shared ring per key, drawn with the SAME instance buffer
        // range as the body above, one draw per key. This is what the deferred per-unit fringe loop used to do one
        // element at a time (a pipeline switch + a uniform matrix each), which measured ~90% of the draw phase.
        if (rec.Keys.Count > 0 && AnalyticAa.Enabled)
        {
            SetupFringeState(projection);
            _device.SetScissors(rec.Scissor);
            foreach (var (seg, first, count) in rec.Keys)
            {
                if (seg.RingBuffer == null) continue;   // a mesh with no closed boundary has no fringe
                _effect.InstancesAddress.SetValue(seg.Gpu.GetDeviceAddress() + (ulong)(first * InstanceStride));
                _device.SetVertexBuffer(seg.RingBuffer);
                _effect.BatchFringePass.Apply();
                _device.Draw(seg.RingVertexCount, count);
            }
            _device.SetScissors(fullScissor);
        }

        // Gradient instanced fills for this group (same shared meshes, gradient pass + per-instance gradient buffer),
        // drawn after the solid fills in the same clip. State is the same as the solid fill; only the pass differs.
        if (rec.GradKeys.Count > 0)
        {
            SetupInstancedState(projection);
            _device.SetScissors(rec.Scissor);
            foreach (var (seg, first, count) in rec.GradKeys)
            {
                _effect.InstancesAddress.SetValue(seg.GradGpu.GetDeviceAddress() + (ulong)(first * GradInstanceStride));
                _device.SetVertexBuffer(seg.VtxBuffer);
                _device.PrimitiveTopology = seg.Topology;
                _effect.BatchGradientFillPass.Apply();
                if (seg.Indexed)
                    _device.DrawIndexed(seg.VtxBuffer, seg.IdxBuffer, instanceCount: count, indexCount: seg.IndexCount);
                else
                    _device.Draw(seg.VertexCount, count);
            }
            _device.SetScissors(fullScissor);
        }

        // Pattern/noise instanced fills for this group (same shared meshes, pattern-fill pass + per-instance pattern buffer).
        if (rec.PatKeys.Count > 0)
        {
            SetupInstancedState(projection);
            _effect.Time.SetValue((float)NoiseClock.Time);   // animated noise reads the shared flow clock
            _device.SetScissors(rec.Scissor);
            foreach (var (seg, first, count) in rec.PatKeys)
            {
                _effect.InstancesAddress.SetValue(seg.PatGpu.GetDeviceAddress() + (ulong)(first * PatInstanceStride));
                _device.SetVertexBuffer(seg.VtxBuffer);
                _device.PrimitiveTopology = seg.Topology;
                _effect.BatchPatternFillPass.Apply();
                if (seg.Indexed)
                    _device.DrawIndexed(seg.VtxBuffer, seg.IdxBuffer, instanceCount: count, indexCount: seg.IndexCount);
                else
                    _device.Draw(seg.VertexCount, count);
            }
            _device.SetScissors(fullScissor);
        }

        // Deferred fringe/stroke of the collected units, drawn OVER their now-flushed fills, in the same clip. The unit's
        // Render() skips its fill body (FillInstanced) and draws only the analytic-AA fringe + stroke.
        if (rec.Units.Count > 0)
        {
            _device.SetScissors(rec.Scissor);
            foreach (var u in rec.Units) u.Render();
            _device.SetScissors(fullScissor);
        }
    }

    // Shared InstancedFill device state (all keys draw the same way; only the mesh topology varies).
    /// <summary>Device address of the shared transform table - the vertex shader fetches each instance's slot matrix from
    /// it. Set by the caller each frame (the table may have been reallocated), same as for the SDF batches.</summary>
    public ulong TransformsAddress { get; set; }

    private void SetupInstancedState(Matrix4x4F projection)
    {
        _effect.Projection.SetValue(projection);
        _effect.TransformsAddress.SetValue(TransformsAddress);
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

    // Fringe device state: as the fill, but the ring's own vertex layout and the pixel basis its VS offsets in.
    private void SetupFringeState(Matrix4x4F projection)
    {
        SetupInstancedState(projection);
        _device.VertexType = typeof(FringeVertex);
        _device.PrimitiveTopology = PrimitiveTopology.TriangleList;
        var vp = _device.CurrentViewports;
        if (vp is { Length: > 0 }) _effect.ViewportSize.SetValue(new Vector2F(vp[0].Width, vp[0].Height));
        _effect.FringePixels.SetValue(DeviceFringePx);
    }

    // Fringe width in DEVICE pixels - the same constant the per-unit fringe uses (GpuFillRenderComponent).
    private const float DeviceFringePx = 1.0f;

    // Build (once) the immutable vtx/idx buffers for a key's shared local mesh. Returns null until it has a drawable mesh.
    private KeySegment GetOrCreate(GeometryKey key, FrozenMesh mesh)
    {
        if (_keys.TryGetValue(key, out var seg) && seg.MeshUploaded) return seg;

        var vertices = mesh.Vertices;
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

        // The fringe ring: immutable like the mesh (both are keyed on the same tessellation).
        if (mesh.Ring is { Length: > 0 } ring)
        {
            seg.Ring ??= ToDispose(_bufferManager.CreateBuffer(BufferUsageFlags.VertexBuffer, Mem));
            seg.RingBuffer = seg.Ring.Acquire((ulong)(ring.Length * RingStride), out var writeRing);
            if (writeRing) seg.RingBuffer.SetData(ring, 0, (uint)ring.Length);
            seg.RingVertexCount = (uint)ring.Length;
        }

        seg.Topology = mesh.Topology;
        seg.MeshUploaded = true;
        _keys[key] = seg;
        return seg;
    }
}
