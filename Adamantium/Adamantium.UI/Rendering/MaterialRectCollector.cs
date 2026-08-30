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

// Backdrop materials: shapes whose fill is made from what is ALREADY DRAWN behind them - acrylic, mica, liquid glass.
//
// The one thing that makes this collector unlike every other SDF batch: its source does not exist until the moment it
// draws. A texture is an asset, bound and forgotten; a capture is a copy of the frame taken between two draws, and it is
// only correct if everything meant to be BEHIND the material has already been drawn. So the capture happens here, in
// DrawSegment, immediately before the instances that read it - not at bake time, when the frame is still being built.
internal sealed class MaterialRectCollector : SdfBatchCollector<MaterialRectItem>
{
    public static bool Enabled = true;

    private readonly BackdropCapture _capture = new();

    // Its OWN effect, not the brushes'. Putting these shaders in BrushEffect made vkCreateShadersEXT die with an access
    // violation - on the gradient pass, which had worked for months: this driver's compiler has a ceiling per effect,
    // and the brushes were already at it. See the note at the top of MaterialEffect.fx.
    private Adamantium.UI.Effects.Generated.MaterialEffect Effect;

    public MaterialRectCollector() : base(64) { }

    protected override void EnsureEffect(IGraphicsDevice device)
    {
        if (Effect != null) return;

        Effect = new Adamantium.UI.Effects.Generated.MaterialEffect(device);
        ProjectionParam = Effect.Projection;
        ViewportSizeParam = Effect.ViewportSize;
        InstancesAddressParam = Effect.InstancesAddress;
        TransformsAddressParam = Effect.TransformsAddress;
    }

    protected override IEffectPass DrawPass => Effect.MaterialFrostedSdfPass;

    /// <summary>The logical region this segment's instances cover, in DEVICE pixels, grown by the blur margin. Set by
    /// the caller when it flushes; the capture is taken from exactly this.</summary>
    public Rect2D CaptureRegion { get; set; }

    // PER SEGMENT, exactly as the textured batch keeps its texture. The region is not a property of the collector but of
    // the segment: a replayed frame re-draws a segment recorded earlier, while CaptureRegion still holds whatever the
    // LAST flush put there. The two then disagree - the copy is taken from one rectangle and the shader maps fragments
    // back through the CaptureRect baked into the instances, which is the other - and the material jumps between the two
    // every time a frame switches between walking and replaying. That is the flicker seen while scrolling.
    private readonly System.Collections.Generic.List<Rect2D> _segRegion = new();

    protected override void OnSegmentRecorded(int index)
    {
        while (_segRegion.Count <= index) _segRegion.Add(default);
        _segRegion[index] = CaptureRegion;
    }

    protected override void OnSegmentInserted(int index)
    {
        while (_segRegion.Count < index) _segRegion.Add(default);
        _segRegion.Insert(index, index > 0 ? _segRegion[index - 1] : CaptureRegion);
    }

    protected override void BindSegment(int index)
    {
        if ((uint)index < (uint)_segRegion.Count) CaptureRegion = _segRegion[index];
    }

    protected override void DrawSegment(IGraphicsDevice device, Buffer<MaterialRectItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        EnsureEffectForDraw(device);

        // Capture FIRST, then draw. Nothing is drawn without one: a material with no backdrop would sample whatever
        // descriptor happens to be bound - in practice the glyph atlas - which is the failure the textured batch already
        // learned to refuse rather than to paint.
        if (!_capture.Capture(device, CaptureRegion) || _capture.Image == null) return;


        Effect.SourceTexture.SetResource(_capture.Image);
        Effect.SourceSampler.SetResource(((GraphicsDevice)device).SamplerStates.LinearClampToEdge);
        base.DrawSegment(device, buffer, count, firstInstance, projection);
    }

    /// <summary>THE one statement of what this batch draws - a render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatch(RectanglePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not MaterialBrush) return false;
        // A pen would have to be composited over a fill that is itself a capture; not supported yet, so a framed
        // material falls to the per-unit path rather than silently losing its border.
        return p.Pen == null;
    }

    public bool CanBatch(RectanglePayload p) => WantsBatch(p);

    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds,
        int transformSlot = 0, int fadeSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (p.Brush is not MaterialBrush material) return false;

        var radius = (float)p.CornerRadius.TopLeft;
        var tint = material.TintColor;
        Items[Count++] = new MaterialRectItem
        {
            Bounds = new Vector4F((float)p.DestinationRect.X, (float)p.DestinationRect.Y,
                (float)p.DestinationRect.Width, (float)p.DestinationRect.Height),
            Params = new Vector4F(radius, transformSlot, (float)material.Material, fadeSlot),
            Radii = new Vector4F((float)p.CornerRadius.TopLeft, (float)p.CornerRadius.TopRight,
                (float)p.CornerRadius.BottomRight, (float)p.CornerRadius.BottomLeft),
            // The tint's ALPHA carries TintOpacity - how much the tint covers the capture - while the element's own
            // opacity rides the coverage, as it does for every other fill.
            Tint = new Vector4F(tint.R / 255f, tint.G / 255f, tint.B / 255f,
                (float)Math.Clamp(material.TintOpacity * opacity * material.Opacity, 0.0, 1.0)),
            Knobs = new Vector4F((float)material.BlurAmount, (float)material.NoiseAmount, (float)material.Refraction, 0f),
            CaptureRect = Vector4F.Zero   // filled in at flush, when the region is known in device pixels
        };

        MarkPending(scissor, logicalBounds);
        return true;
    }

    /// <summary>Tell every pending instance where the capture will come from. Called by the cache once, when it knows
    /// the segment's device-pixel footprint - the instances are baked before that footprint exists.</summary>
    public void SetCaptureRect(Rect2D region)
    {
        CaptureRegion = region;
        var rect = new Vector4F(region.Offset.X, region.Offset.Y, region.Extent.Width, region.Extent.Height);
        for (var i = 0; i < Count; i++) Items[i].CaptureRect = rect;
    }
}
