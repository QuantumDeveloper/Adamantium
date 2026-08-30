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

    // The OTHER source. Acrylic and glass read the frame under the element; mica reads the desktop wallpaper behind the
    // WINDOW, which is a file rather than a capture - see WallpaperBackdrop for why that makes it the cheap one.
    private readonly WallpaperBackdrop _wallpaper = new();



    // Its OWN effect, not the brushes'. Putting these shaders in BrushEffect made vkCreateShadersEXT die with an access
    // violation - on the gradient pass, which had worked for months: this driver's compiler has a ceiling per effect,
    // and the brushes were already at it. See the note at the top of MaterialEffect.fx.
    private Adamantium.UI.Effects.Generated.MaterialEffect Effect;

    // How a frame pixel maps into the bound image. A parameter rather than an instance field because it belongs to the
    // SEGMENT, and because it must be recomputed at draw time - see the note on SourceUv in MaterialEffect.fx.
    private EffectParameter SourceUvParam;

    /// <summary>A rectangle in frame pixels as the SCALE and SHIFT the shader wants: multiply-add instead of
    /// subtract-and-divide per fragment. The guard against a zero-sized rectangle lives here too, once.</summary>
    private static Vector4F ToUv(Vector4F rect)
    {
        var sx = 1f / Math.Max(rect.Z, 1f);
        var sy = 1f / Math.Max(rect.W, 1f);
        return new Vector4F(sx, sy, -rect.X * sx, -rect.Y * sy);
    }

    public MaterialRectCollector() : base(64) { }

    protected override void EnsureEffect(IGraphicsDevice device)
    {
        if (Effect != null) return;

        Effect = new Adamantium.UI.Effects.Generated.MaterialEffect(device);
        SourceUvParam = Effect.SourceUv;
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
    private readonly System.Collections.Generic.List<bool> _segWallpaper = new();

    // TWO flags, not one, and the difference is the difference between recording and drawing.
    //
    // _pendingWallpaper describes the segment being FILLED - it decides whether the next material may join it. It is
    // cleared when that segment is flushed, because the next one starts undecided.
    //
    // _boundWallpaper describes the segment being DRAWN - restored by BindSegment, since a replayed frame draws
    // segments recorded long before. Sharing one field made the draw's value survive into the next frame's recording:
    // after a mica pane the flag stayed set, so the acrylic pane that opened the next frame was taken for a wallpaper
    // one, joined its segment, and vanished.
    private bool _pendingWallpaper;
    private bool _boundWallpaper;

    /// <summary>Whether the segment currently being drawn reads the WALLPAPER rather than a capture of the frame. One
    /// segment, one source - the same rule the textured batch has for its texture, and for the same reason: a draw
    /// binds one image.</summary>
    public bool WallpaperSegment => _boundWallpaper;

    /// <summary>The window on the DESKTOP, in physical pixels. A wallpaper-backed material needs it: the picture is
    /// placed on the desktop, and a fragment has to be mapped from the frame into that placement.</summary>
    public Rect WindowBounds { get; set; }

    /// <summary>The point that picks the MONITOR - the window's centre, so a window mostly on the second screen reads
    /// that screen's wallpaper rather than the one its top-left corner still touches.</summary>
    private PixelPoint WindowCentre()
        => new((int)(WindowBounds.X + WindowBounds.Width / 2), (int)(WindowBounds.Y + WindowBounds.Height / 2));

    protected override void OnSegmentRecorded(int index)
    {
        while (_segRegion.Count <= index) _segRegion.Add(default);
        while (_segWallpaper.Count <= index) _segWallpaper.Add(false);
        _segRegion[index] = CaptureRegion;
        _segWallpaper[index] = _pendingWallpaper;
        _pendingWallpaper = false;   // the next segment starts undecided, as its instances decide it
    }

    protected override void OnSegmentInserted(int index)
    {
        while (_segRegion.Count < index) _segRegion.Add(default);
        while (_segWallpaper.Count < index) _segWallpaper.Add(false);
        _segRegion.Insert(index, index > 0 ? _segRegion[index - 1] : CaptureRegion);
        _segWallpaper.Insert(index, index > 0 && _segWallpaper[index - 1]);
    }

    protected override void BindSegment(int index)
    {
        if ((uint)index < (uint)_segRegion.Count) CaptureRegion = _segRegion[index];
        if ((uint)index < (uint)_segWallpaper.Count) _boundWallpaper = _segWallpaper[index];
    }

    /// <summary>Does this material read the same source as what is already pending? Asked before adding - a change of
    /// source flushes the batch, exactly as a change of texture does in the textured batch.</summary>
    public bool SameSource(RectanglePayload p)
        => !HasPending || p.Brush is not MaterialBrush m || IsWallpaper(m.Material) == _pendingWallpaper;

    private static bool IsWallpaper(MaterialType material) => material == MaterialType.Mica;

    protected override void DrawSegment(IGraphicsDevice device, Buffer<MaterialRectItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        EnsureEffectForDraw(device);

        var samplers = ((GraphicsDevice)device).SamplerStates;

        // ONE source per segment, and NOTHING drawn without one: a material with no backdrop would sample whatever
        // descriptor happens to be bound - in practice the glyph atlas - which is the failure the textured batch already
        // learned to refuse rather than to paint.
        if (WallpaperSegment)
        {
            // No capture at all: the picture is prepared once and only re-read when the desktop says it changed. The
            // texture is asked for HERE rather than at bake time so a wallpaper that changed between recording and
            // replaying is picked up by the replay too.
            var texture = _wallpaper.Texture(device, WindowCentre());
            if (texture == null) return;

            // Computed NOW, every draw: the window may have been dragged since this segment was recorded, and it is the
            // window moving under a still picture that makes mica read as a window onto the desktop.
            SourceUvParam.SetValue(ToUv(WallpaperRect()));
            Effect.SourceTexture.SetResource(texture);
            // Repeat only for a TILED desktop - every other layout places one copy, and repeating it would wrap the
            // picture's far edge into a pane sitting near the screen's border.
            Effect.SourceSampler.SetResource(_wallpaper.Tiles ? samplers.LinearRepeat : samplers.LinearClampToEdge);
        }
        else
        {
            // Capture FIRST, then draw - see BackdropCapture: the copy is only correct once everything meant to be
            // BEHIND the material is already in the frame.
            if (!_capture.Capture(device, CaptureRegion) || _capture.Image == null) return;

            SourceUvParam.SetValue(ToUv(new Vector4F(CaptureRegion.Offset.X, CaptureRegion.Offset.Y,
                CaptureRegion.Extent.Width, CaptureRegion.Extent.Height)));
            Effect.SourceTexture.SetResource(_capture.Image);
            Effect.SourceSampler.SetResource(samplers.LinearClampToEdge);
        }

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
            Knobs = new Vector4F((float)material.BlurAmount, (float)material.NoiseAmount, (float)material.Refraction, 0f)
        };

        if (IsWallpaper(material.Material)) _pendingWallpaper = true;

        MarkPending(scissor, logicalBounds);
        return true;
    }

    /// <summary>Where the capture will be taken from - the segment's device-pixel footprint, which the cache knows only
    /// at flush. Nothing is written into the instances: the rectangle reaches the shader as a parameter set at draw
    /// time, so a replayed frame gets this segment's own value back through <see cref="BindSegment"/>.</summary>
    public void SetCaptureRect(Rect2D region) => CaptureRegion = region;

    /// <summary>Where the wallpaper lands, in the FRAME's device pixels: the desktop rectangle the picture was placed
    /// into, moved into the window and scaled. Recomputed per instance because the window moves - and that movement is
    /// exactly what makes mica look like a window onto the desktop rather than a painted panel.</summary>
    private Vector4F WallpaperRect()
    {
        // The desktop has to have been read at least once before there is a rectangle to compute. Cheap to ask - a path
        // and a timestamp, compared as one record, and only re-read when that comparison differs.
        _wallpaper.Ensure(WindowCentre());

        var placement = _wallpaper.Placement(PlatformSettings.VirtualScreen);
        if (placement.Width <= 0 || placement.Height <= 0) return Vector4F.Zero;


        // ALREADY PHYSICAL, both of them: the desktop states where it put the picture in physical pixels, and the
        // window's corner comes from the OS in the same units. Their difference is therefore physical too, which is what
        // the shader wants - it works in the frame's device pixels. Scaling it again by the render scale was a mistake
        // invisible at 100% and a picture off by half its width at 150%.
        return new Vector4F(
            (float)(placement.X - WindowBounds.X),
            (float)(placement.Y - WindowBounds.Y),
            (float)placement.Width,
            (float)placement.Height);
    }
}
