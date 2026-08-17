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

// Halo batch: the soft bands drawn UNDER shapes - an aura, a shadow, or both - in ONE instanced draw. The sibling of the
// fill collectors, and deliberately the same shape as them: the band is the shape's own signed distance read further
// out, so it costs no offscreen target, no blur pass, and it batches with everything else.
//
// It knows nothing about "an aura and a shadow": it draws N bands. That is what leaves room for an elevation preset -
// one number expanding into the several bands a real penumbra needs - without the public API growing a list.
internal sealed class HaloRectCollector : SdfBatchCollector<HaloRectItem>
{
    public static bool Enabled = true;

    public HaloRectCollector() : base(256) { }

    protected override IEffectPass DrawPass => Effect.BatchHaloPass;

    // Arbitrary geometry reads its distance from a baked field, and a field binds per DRAW - so one is bound per SEGMENT,
    // the way the textured batch binds its source. Analytic shapes need none, which is why a segment may have no field
    // at all and still draw.
    private ITexture _field;
    private readonly List<ITexture> _segState = new();

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

    /// <summary>Still the pending segment's field? A change of field flushes, exactly as a change of texture does for the
    /// textured batch. Analytic bands carry none and never split a run.</summary>
    public bool SameField(ITexture field) => field == null || _field == null || _field == field;

    protected override void DrawSegment(IGraphicsDevice device, Buffer<HaloRectItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        // A segment with no field holds only analytic bands, and those never sample - so unlike the textured batch there
        // is nothing to refuse here. When there IS one, bind it; the shader reaches it only for shape 2.
        if (_field != null)
        {
            Effect.SourceTexture.SetResource(_field);
            Effect.SourceSampler.SetResource(((GraphicsDevice)device).SamplerStates.LinearClampToEdge);
        }

        base.DrawSegment(device, buffer, count, firstInstance, projection);
    }

    /// <summary>Whether this command wears any band at all. STATIC because it is asked before the collector exists: one
    /// is built on the first halo a cache meets, and most caches never meet one.</summary>
    public static bool WantsBatch(RenderData data) => Enabled && data?.Halo is { Length: > 0 };

    /// <summary>Does this element wear any band on the given side? An OUTER band is drawn under every fill, an INNER one
    /// over them - a band inside the shape that went under would simply be covered by the shape's own fill.</summary>
    public static bool HasSide(HaloBand[] bands, bool inner)
    {
        if (bands == null) return false;

        foreach (var band in bands)
        {
            if (!band.IsEmpty && band.Inner == inner) return true;
        }
        return false;
    }

    /// <summary>How far past the outline the widest band reaches - what the element's box has to be grown by before
    /// asking whether anything already drawn overlaps it.</summary>
    public static double MaxReach(HaloBand[] bands)
    {
        if (bands == null) return 0;

        var reach = 0.0;
        foreach (var band in bands)
        {
            if (band.Inner || band.IsEmpty) continue;
            reach = System.Math.Max(reach, band.Reach);
        }
        return reach;
    }

    /// <summary>Bake one command's bands into the pending segment - one instance each, drawn in the order they were
    /// baked (aura first, then shadow). False on a rotated/sheared world, where the axis-aligned instance cannot hold
    /// the shape and the caller falls back to drawing nothing rather than something wrong.</summary>
    public bool TryAdd(HaloBand[] bands, bool inner, Rect destinationRect, ProceduralGeometry.CornerRadius corners, HaloShape shape,
        Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot = 0,
        ITexture field = null, double fieldRange = 0)
    {
        if (bands == null || bands.Length == 0) return false;

        const float eps = 1e-4f;
        if (System.Math.Abs(world.M12) > eps || System.Math.Abs(world.M21) > eps)
        {
            return false;   // rotation/shear -> the band would not follow the shape
        }

        EnsureCpuCapacity(Count + bands.Length);
        if (Count + bands.Length > GpuCapacity) return false;

        // Slot units here, not device px - the band fields are in slot units too, and the vertex stage scales them all together.
        var radii = RectBatchCollector.BakeRadii(corners, destinationRect, 1.0);
        var added = false;
        foreach (var band in bands)
        {
            if (band.IsEmpty || band.Inner != inner) continue;

            var color = band.Color;
            color.W *= (float)opacity;
            if (color.W <= 0f) continue;

            Items[Count++] = new HaloRectItem
            {
                Bounds = new Vector4F((float)destinationRect.X, (float)destinationRect.Y,
                    (float)destinationRect.Width, (float)destinationRect.Height),
                Params = new Vector4F(RectBatchCollector.MaxOf(radii), transformSlot, (float)shape, band.Inner ? 1f : 0f),
                Radii = radii,
                Band = new Vector4F(band.Offset.X, band.Offset.Y, band.Spread, band.Softness),
                Color = color,
                Field = new Vector4F((float)fieldRange, 0, 0, 0)
            };
            added = true;
        }

        if (!added) return false;
        if (field != null) _field = field;
        MarkPending(scissor, logicalBounds);
        return true;
    }
}
