using System.Collections.Generic;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.Vulkan.Core;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering;

// LIVING aura batch: the band whose reach wanders along the outline and drifts over time. The sibling of
// HaloRectCollector and deliberately its own collector rather than a flag on it - the pixel shader evaluates noise, and
// a still band (which is most of what this family draws) must neither pay for that nor ride a heavier shader.
//
// Like the plain one it can draw three kinds of shape: a rounded rect and an ellipse compute their distance, arbitrary
// geometry reads a field bound per SEGMENT.
internal sealed class HaloLivingCollector : ShapeSdfCollector<HaloLivingItem>
{
    public static bool Enabled = true;

    private ITexture _field;
    private readonly List<ITexture> _segState = new();

    public HaloLivingCollector() : base(64) { }

    protected override IEffectPass DrawPass => Effect.BatchHaloLivingPass;

    protected override void OnBeginFrame(IGraphicsDevice device)
    {
        base.OnBeginFrame(device);
        _segState.Clear();
        _field = null;
    }

    protected override void OnSegmentRecorded(int index)
    {
        while (_segState.Count <= index) _segState.Add(null);
        _segState[index] = _field;
    }

    protected override void OnSegmentInserted(int index)
    {
        while (_segState.Count < index) _segState.Add(null);
        _segState.Insert(index, index > 0 ? _segState[index - 1] : null);
    }

    protected override void BindSegment(int index) => _field = _segState[index];

    public bool SameField(ITexture field) => field == null || _field == null || _field == field;

    protected override void DrawSegment(IGraphicsDevice device, Buffer<HaloLivingItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        // The shared flow clock, the same one the animated noise brushes advance on - so a breathing aura and a flowing
        // noise fill drift together rather than each on its own timebase.
        EnsureEffectForDraw(device);
        Effect.Time.SetValue((float)NoiseClock.Time);

        if (_field != null)
        {
            Effect.SourceTexture.SetResource(_field);
            Effect.SourceSampler.SetResource(((GraphicsDevice)device).SamplerStates.LinearClampToEdge);
        }

        base.DrawSegment(device, buffer, count, firstInstance, projection);
    }

    /// <summary>Whether this command wears a living aura. STATIC because it is asked before the collector exists.</summary>
    public static bool WantsBatch(RenderData data) => Enabled && data?.LivingHalo != null;

    /// <summary>How far past the outline it can reach at its widest - the wander adds to the reach, so the overlap test
    /// has to grow the element's box by more than a still band of the same radius would need.</summary>
    public static double MaxReach(LivingBand? band)
        => band is { Inner: false } b ? b.Reach : 0;

    public bool TryAdd(LivingBand band, Rect destinationRect, ProceduralGeometry.CornerRadius corners, HaloShape shape, Matrix4x4F world,
        double opacity, Rect2D scissor, Rect logicalBounds, Vector4F colour, int transformSlot = 0,
        ITexture field = null, double fieldRange = 0, int clipSlot = -1, int fadeSlot = -1)
    {
        const float eps = 1e-4f;
        if (System.Math.Abs(world.M12) > eps || System.Math.Abs(world.M21) > eps)
        {
            return false;   // rotation/shear -> the band would not follow the shape
        }

        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;

        if (!BakeItem(band, destinationRect, corners, shape, world, opacity, colour, transformSlot, fieldRange, clipSlot, fadeSlot, out var baked))
            return false;

        LastSlot = Count;
        Items[Count++] = baked;
        if (field != null) _field = field;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    /// <summary>Which record the last <see cref="TryAdd"/> took, so a patch can re-bake it in place - see
    /// <see cref="HaloRectCollector.BakeInto"/> for why the bake has to be reachable outside the walk.</summary>
    public int LastSlot { get; private set; }

    /// <summary>Bake one living band WITHOUT appending it. False = not bakeable this way (a rotated/sheared world, or a
    /// band that has faded to nothing).</summary>
    public static bool BakeItem(LivingBand band, Rect destinationRect, ProceduralGeometry.CornerRadius corners,
        HaloShape shape, Matrix4x4F world, double opacity, Vector4F colour, int transformSlot, double fieldRange,
        int clipSlot, int fadeSlot, out HaloLivingItem item)
    {
        item = default;
        const float eps = 1e-4f;
        if (System.Math.Abs(world.M12) > eps || System.Math.Abs(world.M21) > eps) return false;

        colour.W *= (float)opacity;
        if (colour.W <= 0f) return false;

        // The bake goes INTO the instance and the slot on top - see HaloRectCollector.TryAdd for why dropping it put a
        // band in the top-left corner. Slot units, not device px: the vertex stage scales bounds and band together.
        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        var iso = System.Math.Min(sx, sy);
        var radii = RectBatchCollector.BakeRadii(corners, destinationRect, iso);
        item = new HaloLivingItem
        {
            Bounds = new Vector4F((float)(destinationRect.X * sx + tx), (float)(destinationRect.Y * sy + ty),
                (float)(destinationRect.Width * sx), (float)(destinationRect.Height * sy)),
            Params = new Vector4F(RectBatchCollector.MaxOf(radii), transformSlot, (float)shape, band.Inner ? 1f : 0f),
            Radii = radii,
            Band = new Vector4F(0, 0, band.Spread * iso, band.Softness * iso),
            Field = new Vector4F((float)fieldRange * iso, band.Turbulence, band.Flow, band.Detail),
            Color = colour,
            // .y = the rounded clip's slot, .z = the opacity slot (-1 = none for either) - Field is full here, so both
            // ride in the ramp's spare components.
            Ramp = new Vector4F(band.StopCount, clipSlot, fadeSlot, 0)
        };

        if (band.Palette is { Length: >= 8 } p && band.Offsets is { Length: >= 8 } o)
        {
            item.Stop0 = p[0]; item.Stop1 = p[1]; item.Stop2 = p[2]; item.Stop3 = p[3];
            item.Stop4 = p[4]; item.Stop5 = p[5]; item.Stop6 = p[6]; item.Stop7 = p[7];
            item.Offsets0 = new Vector4F(o[0], o[1], o[2], o[3]);
            item.Offsets1 = new Vector4F(o[4], o[5], o[6], o[7]);
        }

        return true;
    }
}
