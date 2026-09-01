using System;
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
internal sealed class HaloRectCollector : ShapeSdfCollector<HaloRectItem>
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
        EnsureEffectForDraw(device);

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
        ITexture field = null, double fieldRange = 0, int clipSlot = -1, int fadeSlot = -1)
    {
        if (bands == null || bands.Length == 0) return false;

        const float eps = 1e-4f;
        if (System.Math.Abs(world.M12) > eps || System.Math.Abs(world.M21) > eps)
        {
            return false;   // rotation/shear -> the band would not follow the shape
        }

        EnsureCpuCapacity(Count + bands.Length);
        if (Count + bands.Length > GpuCapacity) return false;

        LastFirst = Count;
        var added = BakeInto(Items.AsSpan(Count, bands.Length), bands, inner, destinationRect, corners, shape, world,
            opacity, transformSlot, fieldRange, clipSlot, fadeSlot);
        if (added == 0) return false;

        Count += added;
        LastCount = added;
        if (field != null) _field = field;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    /// <summary>Where the last <see cref="TryAdd"/> put its bands, so a patch can re-bake them in place instead of
    /// waiting for the next walk to write the arena again.</summary>
    public int LastFirst { get; private set; }
    public int LastCount { get; private set; }

    /// <summary>Bake one command's bands into <paramref name="dst"/> and return how many were written. WHERE they land is
    /// the caller's business - the retained arena while walking, the records they already occupy while patching - and the
    /// bake is the same either way. That symmetry is the point: without it the band was written by the walk alone, so a
    /// colour change repainted the shape at once and left its aura on the old colour until some unrelated frame walked.
    /// </summary>
    public static int BakeInto(Span<HaloRectItem> dst, HaloBand[] bands, bool inner, Rect destinationRect,
        ProceduralGeometry.CornerRadius corners, HaloShape shape, Matrix4x4F world, double opacity,
        int transformSlot, double fieldRange, int clipSlot = -1, int fadeSlot = -1)
    {
        // The bake goes INTO the instance and the slot is applied on top - the same two-part address every fill family
        // uses. It used to be dropped here, and the band's ONLY address was its slot: correct while that slot held the
        // full world, wrong the moment it held a motion NODE's, because the shape's own place inside the node went
        // nowhere. That is the aura landing in the top-left corner during a slide.
        // Slot units, not device px: the band fields are in slot units too and the vertex stage scales them together.
        var sx = world.M11; var sy = world.M22; var tx = world.M41; var ty = world.M42;
        var iso = System.Math.Min(sx, sy);   // the shader reads radii and band isotropically - bake them the same way
        var radii = RectBatchCollector.BakeRadii(corners, destinationRect, iso);
        var written = 0;
        foreach (var band in bands)
        {
            if (band.IsEmpty || band.Inner != inner) continue;

            var color = band.Color;
            color.W *= (float)opacity;
            if (color.W <= 0f) continue;
            if (written >= dst.Length) break;

            dst[written++] = new HaloRectItem
            {
                Bounds = new Vector4F((float)(destinationRect.X * sx + tx), (float)(destinationRect.Y * sy + ty),
                    (float)(destinationRect.Width * sx), (float)(destinationRect.Height * sy)),
                Params = new Vector4F(RectBatchCollector.MaxOf(radii), transformSlot, (float)shape, band.Inner ? 1f : 0f),
                Radii = radii,
                Band = new Vector4F(band.Offset.X * iso, band.Offset.Y * iso, band.Spread * iso, band.Softness * iso),
                Color = color,
                // .y = the rounded clip's slot, .z = the opacity slot (-1 = none for either): a band is cut - and
                // faded - by an ancestor exactly like the fill it sits under.
                Field = new Vector4F((float)fieldRange * iso, clipSlot, fadeSlot, 0)
            };
        }

        return written;
    }
}
