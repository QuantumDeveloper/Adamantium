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
internal sealed class HaloLivingCollector : SdfBatchCollector<HaloLivingItem>
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

    protected override void BindSegment(int index) => _field = _segState[index];

    public bool SameField(ITexture field) => field == null || _field == null || _field == field;

    protected override void DrawSegment(IGraphicsDevice device, Buffer<HaloLivingItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        // The shared flow clock, the same one the animated noise brushes advance on - so a breathing aura and a flowing
        // noise fill drift together rather than each on its own timebase.
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

    public bool TryAdd(LivingBand band, Rect destinationRect, double cornerRadius, HaloShape shape, Matrix4x4F world,
        double opacity, Rect2D scissor, Rect logicalBounds, Vector4F colour, int transformSlot = 0,
        ITexture field = null, double fieldRange = 0)
    {
        const float eps = 1e-4f;
        if (System.Math.Abs(world.M12) > eps || System.Math.Abs(world.M21) > eps)
        {
            return false;   // rotation/shear -> the band would not follow the shape
        }

        colour.W *= (float)opacity;
        if (colour.W <= 0f) return false;

        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;

        var item = new HaloLivingItem
        {
            Bounds = new Vector4F((float)destinationRect.X, (float)destinationRect.Y,
                (float)destinationRect.Width, (float)destinationRect.Height),
            Params = new Vector4F((float)cornerRadius, transformSlot, (float)shape, band.Inner ? 1f : 0f),
            Band = new Vector4F(0, 0, band.Spread, band.Softness),
            Field = new Vector4F((float)fieldRange, band.Turbulence, band.Flow, band.Detail),
            Color = colour,
            Ramp = new Vector4F(band.StopCount, 0, 0, 0)
        };

        if (band.Palette is { Length: >= 8 } p && band.Offsets is { Length: >= 8 } o)
        {
            item.Stop0 = p[0]; item.Stop1 = p[1]; item.Stop2 = p[2]; item.Stop3 = p[3];
            item.Stop4 = p[4]; item.Stop5 = p[5]; item.Stop6 = p[6]; item.Stop7 = p[7];
            item.Offsets0 = new Vector4F(o[0], o[1], o[2], o[3]);
            item.Offsets1 = new Vector4F(o[4], o[5], o[6], o[7]);
        }

        Items[Count++] = item;
        if (field != null) _field = field;
        MarkPending(scissor, logicalBounds);
        return true;
    }
}
