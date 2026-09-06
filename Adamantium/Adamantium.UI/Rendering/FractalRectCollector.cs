using System;
using System.Collections.Generic;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Fractal rounded-rect batch: draws MANY rounded-rect fills whose fill is an escape-time fractal (Julia/Mandelbrot) in ONE
// instanced draw (each fill = one per-instance FractalRectItem; the pixel shader reconstructs the rounded rect from an SDF
// AND iterates the fractal per fragment). A sibling of the pattern/gradient SDF collectors - a FractalBrush fill routes
// here. Segment/buffer/overlap/retain machinery comes from SdfBatchCollector; this adds the fractal bake + the Fractal pass.
internal sealed class FractalRectCollector : BrushSdfCollector<FractalRectItem>
{
    public static bool Enabled = true;

    // Only zoom past this uses the perturbation deep path; below it the proven float shader path is unchanged (and a
    // reference orbit isn't even computed). The float wall is ~1e5, so switching over well before keeps a smooth handover.
    private const double DeepZoomThreshold = 100.0;

    // Shared reference-orbit buffer: every deep-zoom Quadratic fractal's Z_n orbit, concatenated (each item's Ref.x/.y is
    // its start index + length). One step is a HI/LO pair - xy the float that holds most of Z_n, zw the residue it could
    // not - so a step is a float4. Rebuilt by the walk (orbits are small - <=401 steps each) and uploaded whole.
    private Vector4F[] _orbitCpu = new Vector4F[4096];
    private int _orbitCount;
    private bool _orbitUploaded;

    // A RING, for the same reason the item buffer has one: a walk rewrites the orbit while earlier frames are still
    // reading it, and overwriting one buffer in place hands a frame in flight a half-written orbit. Zoom is where it
    // shows - every crossing of a reference cell forces a walk, so the rewrites come thick and fast.
    private const int OrbitRingDepth = 3;
    private readonly Buffer<Vector4F>[] _orbitRing = new Buffer<Vector4F>[OrbitRingDepth];
    private int _orbitRingIndex;
    private int _orbitGpuCapacity;

    // What each deep slot's orbit was computed FROM, and where it landed. The buffer outlives the frame that built it -
    // BeginFrame runs only on a walk, and replay/patch frames return before it - so a patch may reuse a slice as long as
    // every input the orbit depends on still matches. That is the whole point of quantizing the reference to a grid:
    // panning inside one cell leaves the orbit alone and moves only the offset in Ref.zw.
    private readonly Dictionary<int, OrbitSlice> _orbitSlices = new();

    private readonly record struct OrbitSlice(int Start, int Length, double RefX, double RefY, double Cx, double Cy, int MaxN);


    // Deep zoom is a KIND of its own, not a flag inside one: it is the only path that reads the reference orbit, and
    // the pass that has it must not carry the plain loop as well.
    private const int DeepKind = 6;

    /// <summary>Ceiling on iterations, mirrored by the same bound in the shader's loops. It is what a DEEP zoom spends
    /// its detail on: past ~1e10 a point needs thousands of iterations before it escapes, and a lower cap paints the
    /// whole neighbourhood as interior - a flat blob, which reads as "the shader stopped resolving the fractal".</summary>
    public const int MaxIterations = 2000;

    // ONE KIND PER SEGMENT, exactly as the pattern batch does it - each formula is its own pass now, so a segment has to
    // be uniform in kind and the caller flushes on a change. The whole point is that no pass carries the other five
    // formulas plus the perturbation block; putting the selector back into the record would undo it.
    private int _kind;
    private readonly List<int> _segKinds = new();

    public FractalRectCollector() : base(256) { }

    protected override IEffectPass DrawPass => _kind switch
    {
        1 => Effect.FractalBurningShipSdfPass,
        2 => Effect.FractalTricornSdfPass,
        3 => Effect.FractalCelticSdfPass,
        4 => Effect.FractalMultibrotSdfPass,
        5 => Effect.FractalNewtonSdfPass,
        DeepKind => Effect.FractalDeepSdfPass,
        _ => Effect.FractalQuadraticSdfPass
    };

    protected override void OnSegmentRecorded(int index)
    {
        while (_segKinds.Count <= index) _segKinds.Add(0);
        _segKinds[index] = _kind;
    }

    protected override void OnSegmentInserted(int index)
    {
        while (_segKinds.Count < index) _segKinds.Add(0);
        _segKinds.Insert(index, index > 0 ? _segKinds[index - 1] : 0);
    }

    protected override void BindSegment(int index) => _kind = _segKinds[index];

    /// <summary>Still the pending segment's kind? A change flushes the batch - the caller asks this before adding,
    /// mirroring PatternRectCollector.SameKind.</summary>
    public bool SameKind(int kind) => !Active || _kind == kind;

    /// <summary>The kind this brush bakes as, for the caller's SameKind check. -1 = not a fractal brush at all.</summary>
    public static int KindOf(Brush brush)
    {
        if (brush is not FractalBrush f) return -1;
        int formula = (int)f.Formula;
        return formula == 0 && f.Zoom > DeepZoomThreshold ? DeepKind : formula;
    }

    protected override void OnBeginFrame(IGraphicsDevice device)
    {
        base.OnBeginFrame(device);
        _segKinds.Clear();
        _orbitCount = 0;
        _orbitSlices.Clear();
        _orbitUploaded = false;
    }

    // Feed the shared morph clock to the shader before drawing (only the fractal pass reads Time): a static fractal ignores
    // it; an Animate one drifts C by it. FractalClock advances only while an animating fractal is live, so this is 0 otherwise.
    protected override void DrawSegment(IGraphicsDevice device, Buffer<FractalRectItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        EnsureEffectForDraw(device);
        Effect.Time.SetValue((float)FractalClock.Time);

        // Publish the reference-orbit buffer (perturbation deep path). Uploaded ONCE per frame on the first segment; the
        // shader only dereferences OrbitAddress when an instance's Ref.z (deep flag) is set, so 0 is safe when none are deep.
        if (_orbitCount > 0)
        {
            if (!_orbitUploaded)
            {
                if (_orbitGpuCapacity < _orbitCount)
                {
                    // Retire the WHOLE ring: a slot too small for this orbit is too small for the next one too. Handed to
                    // the DEFERRED queue, never disposed here - frames in flight are still reading them.
                    for (var i = 0; i < OrbitRingDepth; i++)
                    {
                        if (_orbitRing[i] != null) device.AddToDeferDisposeQueue(_orbitRing[i]);
                        _orbitRing[i] = null;
                    }
                    _orbitGpuCapacity = System.Math.Max(_orbitCount, 4096);
                }

                _orbitRingIndex = (_orbitRingIndex + 1) % OrbitRingDepth;
                _orbitRing[_orbitRingIndex] ??= Adamantium.Graphics.Buffer.New<Vector4F>(device, (uint)_orbitGpuCapacity,
                    BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                    MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal);
                _orbitRing[_orbitRingIndex].SetData(_orbitCpu.AsSpan(0, _orbitCount), 0);
                _orbitUploaded = true;
            }
            Effect.OrbitAddress.SetValue(_orbitRing[_orbitRingIndex].GetDeviceAddress());
        }
        else
        {
            Effect.OrbitAddress.SetValue(0);
        }

        base.DrawSegment(device, buffer, count, firstInstance, projection);
    }

    // Batchable = a FractalBrush fill, a batchable pen (none or a solid stroke the SDF shader draws), and uniform corner
    // radius. Mirrors PatternRectCollector.CanBatch.
    public bool CanBatch(RectanglePayload p)
    {
        if (!Enabled)
        {
            return false;
        }
        if (p.Brush is not FractalBrush)
        {
            return false;
        }
        if (!RectBatchCollector.IsPenBatchable(p.Pen))
        {
            return false;
        }
        return true;
    }

    // Bake one fractal rounded-rect fill. False only if it can't be baked (rotated/sheared world or a GPU-buffer overflow) -
    // the caller draws it per-unit (the demo stays axis-aligned).
    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0, int clipSlot = -1, int fadeSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity)
        {
            return false;
        }
        if (!BakeItem(p, world, opacity, transformSlot, clipSlot, fadeSlot, out var item))
        {
            return false;
        }
        AppendOrbit(p, ref item);
        _kind = KindOf(p.Brush);   // the pass this segment draws with; the caller has already flushed on a change
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // The reference this brush's orbit is built around, or false when the deep path does not apply. Quantized to a
    // power-of-2 grid ~ the view span so it stays FIXED across small pans/zooms: panning then moves the tiny
    // (viewCentre - ref) OFFSET - float has fine ABSOLUTE precision at small magnitudes (~1e-13 at 1e-6) - instead of the
    // O(1) orbit values, whose float ULP (~1e-7) exceeds the deep per-pixel step and made the image snap (the "jitter").
    // The reference plane is c (Mandelbrot) or z0 (Julia); the Julia constant stays fixed.
    private static bool DeepReference(FractalBrush f, out double refX, out double refY, out double offX, out double offY)
    {
        refX = refY = offX = offY = 0.0;
        if ((int)f.Formula != 0 || f.Zoom <= DeepZoomThreshold) return false;

        double span = 1.5 / System.Math.Max(f.Zoom, 1e-4);
        double gridStep = System.Math.Pow(2.0, System.Math.Floor(System.Math.Log2(span)));
        // Which CELL the reference sits in is decided by the WHOLE centre. Rounding here costs nothing - a cell is about
        // a view span wide - but asking only the coarse part does: panning now lives in the fine part, so the coarse one
        // stops moving, and the reference would stay behind at the cell the view started in while the view walked away.
        // The orbit would then be built around a point no longer on screen, and the offset would grow to order 1, where
        // the float it ships in steps by ~6e-8 - which is what turned the picture into blocks.
        refX = System.Math.Round((f.Center.X + f.CenterFine.X) / gridStep) * gridStep;
        refY = System.Math.Round((f.Center.Y + f.CenterFine.Y) / gridStep) * gridStep;
        // The OFFSET is where the precision has to survive: subtract the coarse part first - two nearby doubles subtract
        // EXACTLY - and only then add the fine part, which keeps every digit it has.
        offX = (f.Center.X - refX) + f.CenterFine.X;   // viewCentre - reference; rides to the shader in Ref.zw
        offY = (f.Center.Y - refY) + f.CenterFine.Y;
        return true;
    }

    // Compute this fractal's high-precision (double) REFERENCE ORBIT at the quantized reference and append it to the shared
    // orbit buffer, stamping Ref onto the item. Only Quadratic (z²+c) past DeepZoomThreshold - other formulas / shallow zoom
    // leave Ref length 0 (deep path off) and render on the float path unchanged.
    //
    // Each step ships as a HI/LO PAIR of floats, not one float. Z_n is O(1) and a float carries it to ~1e-7 ABSOLUTE, but
    // at zoom 1e8 the whole view is 1.5e-8 across - the reference missed by more than the picture, which is what put the
    // wall at ~1e8. The lo term is the residue the hi float could not hold, so the pair carries ~1e-14 and the wall moves
    // with it. The deltas stay single floats: they are small, and only their RELATIVE precision matters.
    private void AppendOrbit(RectanglePayload p, ref FractalRectItem item)
    {
        if (p.Brush is not FractalBrush f) return;
        if (!DeepReference(f, out var refX, out var refY, out var offX, out var offY)) return;

        bool mandelbrot = (int)f.Fractal == 1;   // Mandelbrot: z0 = 0, c = centre. Julia: z0 = centre, c = the constant.

        DoubleDouble cx, cy, zx, zy;
        if (mandelbrot) { cx = refX; cy = refY; zx = default; zy = default; }
        else { cx = f.C.X; cy = f.C.Y; zx = refX; zy = refY; }

        int maxN = System.Math.Min((int)f.Iterations, MaxIterations);
        int start = _orbitCount;
        EnsureOrbitCapacity(start + maxN + 1);
        int len = 0;
        for (int n = 0; n <= maxN; n++)
        {
            var hiX = (float)zx.Hi; var hiY = (float)zy.Hi;
            _orbitCpu[start + n] = new Vector4F(hiX, hiY,
                (float)((zx.Hi - hiX) + zx.Lo), (float)((zy.Hi - hiY) + zy.Lo));
            len++;
            var zx2 = zx * zx;
            var zy2 = zy * zy;
            var xy = zx * zy;
            zx = zx2 - zy2 + cx;
            zy = xy + xy + cy;
            if (zx.Hi * zx.Hi + zy.Hi * zy.Hi > 1e12) break;   // diverged - stop before the squared value overflows float
        }
        _orbitCount = start + len;
        item.Ref = new Vector4F(start, len, (float)offX, (float)offY);   // start, length, (viewCentre - ref) offset. Length > 0 arms deep path.
        _orbitSlices[Count] = new OrbitSlice(start, len, refX, refY, cx.Hi, cy.Hi, maxN);   // Count is the slot this item is about to take
    }

    /// <summary>Stamp a PATCHED record's Ref from the orbit this slot already owns, or false when that orbit no longer
    /// describes it and only a walk can rebuild one. Panning inside a reference cell is the case worth keeping cheap:
    /// the orbit is unchanged and only the offset moves, which is exactly what the grid quantisation buys.</summary>
    public bool TryStampOrbit(int slot, RectanglePayload p, ref FractalRectItem item)
    {
        if (p.Brush is not FractalBrush f) return true;
        if (!DeepReference(f, out var refX, out var refY, out var offX, out var offY)) return true;   // shallow: no orbit to stamp
        if (!_orbitSlices.TryGetValue(slot, out var slice)) return false;

        bool mandelbrot = (int)f.Fractal == 1;
        double cx = mandelbrot ? refX : f.C.X;
        double cy = mandelbrot ? refY : f.C.Y;
        if (refX != slice.RefX || refY != slice.RefY || cx != slice.Cx || cy != slice.Cy) return false;
        if (System.Math.Min((int)f.Iterations, MaxIterations) != slice.MaxN) return false;

        item.Ref = new Vector4F(slice.Start, slice.Length, (float)offX, (float)offY);
        return true;
    }

    private void EnsureOrbitCapacity(int n)
    {
        if (_orbitCpu.Length >= n) return;
        int cap = _orbitCpu.Length;
        while (cap < n) cap *= 2;
        System.Array.Resize(ref _orbitCpu, cap);
    }

    // Bake a fractal fill into an instance record. Position -> world; the fractal maps the fragment to the complex plane
    // (centre/zoom are complex-plane values, NOT scaled by the device scale - only the corner radius + stroke are px).
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, int clipSlot, int fadeSlot, out FractalRectItem item)
    {
        item = default;
        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps)
        {
            return false;   // rotation/shear -> per-unit
        }
        if (p.Brush is not FractalBrush f)
        {
            return false;
        }

        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        var alpha = (float)(opacity * f.Opacity);

        var c1 = RectBatchCollector.WithOpacity(f.Color1, alpha);
        var c2 = RectBatchCollector.WithOpacity(f.Color2, alpha);

        RectBatchCollector.BakeStroke(p.Pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1, out var dash);

        var r = p.DestinationRect;
        var radii = RectBatchCollector.BakeRadii(p.CornerRadius, r, sx);
        item = new FractalRectItem
        {
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            Params = new Vector4F(RectBatchCollector.MaxOf(radii), (int)f.Fractal, transformSlot, f.Iterations),
            Radii = radii,
            // The centre the FLOAT path maps fragments through is the WHOLE centre - coarse plus fine. It is the only
            // reader that wants them added: this path lives at shallow zoom, where the sum loses nothing, while the deep
            // path deliberately keeps them apart and sends the small one on its own in Ref.zw.
            Geom = new Vector4F((float)(f.Center.X + f.CenterFine.X), (float)(f.Center.Y + f.CenterFine.Y),
                                (float)f.Zoom, (float)f.Power),   // .w = Multibrot exponent (MorphSpeed lives on FractalClock now)
            Julia = new Vector4F((float)f.C.X, (float)f.C.Y, f.Animate ? 1f : 0f, (int)f.Formula),
            Color1 = c1,
            Color2 = c2,
            StrokeColor = new Color(strokeColor),
            Stroke0 = stroke0,
            Stroke1 = stroke1,
            Dash = dash,
            // .x the rounded ancestor clip, .y the opacity slot its alpha comes from (-1 = none for either). Until the
            // slot was read here a faded ancestor reached a fractal only through a full re-bake, so it lagged behind
            // every neighbour on the Opacity stand until something forced a walk.
            Clip = new Vector4F(clipSlot, fadeSlot, 0, 0)
        };
        return true;
    }
}
