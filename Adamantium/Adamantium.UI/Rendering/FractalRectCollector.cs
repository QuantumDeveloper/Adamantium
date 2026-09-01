using System;
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
    // its start index + length). Rebuilt from scratch each frame (orbits are small - <=401 float2 each) and uploaded whole.
    private Vector2F[] _orbitCpu = new Vector2F[4096];
    private int _orbitCount;
    private bool _orbitUploaded;
    private Buffer<Vector2F> _orbitGpu;
    private int _orbitGpuCapacity;

    public FractalRectCollector() : base(256) { }

    protected override IEffectPass DrawPass => Effect.FractalSdfPass;

    protected override void OnBeginFrame(IGraphicsDevice device)
    {
        base.OnBeginFrame(device);
        _orbitCount = 0;
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
            if (_orbitGpu == null || _orbitGpuCapacity < _orbitCount)
            {
                _orbitGpu?.Dispose();
                _orbitGpuCapacity = System.Math.Max(_orbitCount, 4096);
                _orbitGpu = Adamantium.Graphics.Buffer.New<Vector2F>(device, (uint)_orbitGpuCapacity,
                    BufferUsageFlags.StorageBuffer | BufferUsageFlags.ShaderDeviceAddress,
                    MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal);
                _orbitUploaded = false;
            }
            if (!_orbitUploaded)
            {
                _orbitGpu.SetData(_orbitCpu.AsSpan(0, _orbitCount), 0);
                _orbitUploaded = true;
            }
            Effect.OrbitAddress.SetValue(_orbitGpu.GetDeviceAddress());
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
        Items[Count++] = item;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Compute this fractal's high-precision (double) REFERENCE ORBIT at the VIEW CENTRE and append it to the shared orbit
    // buffer, stamping Ref onto the item. Only Quadratic (z²+c) past DeepZoomThreshold - other formulas / shallow zoom leave
    // Ref length 0 (deep path off) and render on the float path unchanged. The reference is simply the view centre: stable
    // across frames (no jitter), and the shader's SEGMENTED REBASING copes with however briefly that orbit survives.
    private void AppendOrbit(RectanglePayload p, ref FractalRectItem item)
    {
        if (p.Brush is not FractalBrush f) return;
        if ((int)f.Formula != 0 || f.Zoom <= DeepZoomThreshold) return;

        bool mandelbrot = (int)f.Fractal == 1;   // Mandelbrot: z0 = 0, c = centre. Julia: z0 = centre, c = the constant.

        // Quantize the reference to a power-of-2 grid ~ the view span so it stays FIXED across small pans/zooms. Panning
        // then moves the tiny (viewCentre - ref) OFFSET - float has fine ABSOLUTE precision at small magnitudes (~1e-13 at
        // 1e-6) - instead of the O(1) orbit values, whose float ULP (~1e-7) exceeds the deep per-pixel step and made the
        // image snap (the "jitter"). The reference plane is c (Mandelbrot) or z0 (Julia); the Julia constant stays fixed.
        double span = 1.5 / System.Math.Max(f.Zoom, 1e-4);
        double gridStep = System.Math.Pow(2.0, System.Math.Floor(System.Math.Log2(span)));
        double refX = System.Math.Round(f.Center.X / gridStep) * gridStep;
        double refY = System.Math.Round(f.Center.Y / gridStep) * gridStep;
        double offX = f.Center.X - refX;   // viewCentre - reference; rides to the shader in Ref.zw, added to each delta
        double offY = f.Center.Y - refY;

        double cx, cy, zx, zy;
        if (mandelbrot) { cx = refX; cy = refY; zx = 0.0; zy = 0.0; }
        else { cx = f.C.X; cy = f.C.Y; zx = refX; zy = refY; }

        int maxN = System.Math.Min((int)f.Iterations, 400);
        int start = _orbitCount;
        EnsureOrbitCapacity(start + maxN + 1);
        int len = 0;
        for (int n = 0; n <= maxN; n++)
        {
            _orbitCpu[start + n] = new Vector2F((float)zx, (float)zy);
            len++;
            double nx = zx * zx - zy * zy + cx;
            double ny = 2.0 * zx * zy + cy;
            zx = nx; zy = ny;
            if (zx * zx + zy * zy > 1e12) break;   // diverged - stop before the squared value overflows float
        }
        _orbitCount = start + len;
        item.Ref = new Vector4F(start, len, (float)offX, (float)offY);   // start, length, (viewCentre - ref) offset. Length > 0 arms deep path.
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

        var c1 = f.Color1.ToVector4();
        c1.W *= alpha;
        var c2 = f.Color2.ToVector4();
        c2.W *= alpha;

        RectBatchCollector.BakeStroke(p.Pen, opacity, (float)sx, out var strokeColor, out var stroke0, out var stroke1, out var dash);

        var r = p.DestinationRect;
        var radii = RectBatchCollector.BakeRadii(p.CornerRadius, r, sx);
        item = new FractalRectItem
        {
            Bounds = new Vector4F((float)(r.X * sx + tx), (float)(r.Y * sy + ty), (float)(r.Width * sx), (float)(r.Height * sy)),
            Params = new Vector4F(RectBatchCollector.MaxOf(radii), (int)f.Fractal, transformSlot, f.Iterations),
            Radii = radii,
            Geom = new Vector4F((float)f.Center.X, (float)f.Center.Y, (float)f.Zoom, (float)f.Power),   // .w = Multibrot exponent (MorphSpeed lives on FractalClock now)
            Julia = new Vector4F((float)f.C.X, (float)f.C.Y, f.Animate ? 1f : 0f, (int)f.Formula),
            Color1 = c1,
            Color2 = c2,
            StrokeColor = strokeColor,
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
