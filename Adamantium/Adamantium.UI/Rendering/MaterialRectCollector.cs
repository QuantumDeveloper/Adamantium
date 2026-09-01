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

    /// <summary>The pass the segment being drawn needs. A material is a SOURCE plus a TREATMENT, and both are decided
    /// per segment: acrylic is capture+frosted, mica is wallpaper+frosted, liquid glass is capture+glass.</summary>
    protected override IEffectPass DrawPass => _boundGlass ? Effect.MaterialGlassSdfPass : Effect.MaterialFrostedSdfPass;

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

    // The same pair again, for the TREATMENT rather than the source: which pass this segment is drawn with. Kept beside
    // the source flags because a segment is defined by BOTH - two materials may share a segment only if they agree
    // about the image bound to it and about the shader that reads it.
    private readonly System.Collections.Generic.List<bool> _segGlass = new();
    private bool _pendingGlass;
    private bool _boundGlass;

    // And once more for the author's own picture: WHICH one (a draw binds one) and WHAT IT IS PINNED TO (the anchor
    // decides the mapping). Both inert without a picture.
    private readonly System.Collections.Generic.List<ITexture> _segSource = new();
    private readonly System.Collections.Generic.List<MaterialAnchor> _segAnchor = new();
    private ITexture _pendingSource;
    private ITexture _boundSource;
    private MaterialAnchor _pendingAnchor;
    private MaterialAnchor _boundAnchor;

    /// <summary>Whether the segment currently being drawn reads the WALLPAPER rather than a capture of the frame. One
    /// segment, one source - the same rule the textured batch has for its texture, and for the same reason: a draw
    /// binds one image.</summary>
    public bool WallpaperSegment => _boundWallpaper;

    /// <summary>The window on the DESKTOP in physical pixels, as THIS FRAME understands it: a wallpaper-backed material
    /// maps a fragment from the frame into the picture's placement there. Latched, not asked per draw - the value is
    /// written by the message thread, and two panes in one frame must not be placed against two positions.</summary>
    public Rect WindowBounds => _frameWindow;

    private Rect _frameWindow;

    /// <summary>Take this frame's origin. Called per FRAME by the cache, not from this batch's BeginFrame: that only
    /// runs on frames that walk the tree, and a drag re-records nothing - the latch would stop moving for the whole
    /// drag.</summary>
    public void LatchWindow() => _frameWindow = WindowBoundsProvider?.Invoke() ?? _lastWindowBounds;

    /// <summary>Where the window is, asked as late as possible. Supplied by the cache, which owns the visual root.</summary>
    public Func<Rect> WindowBoundsProvider { get; set; }

    private Rect _lastWindowBounds;

    /// <summary>The point that picks the MONITOR - the window's centre, so a window mostly on the second screen reads
    /// that screen's wallpaper rather than the one its top-left corner still touches.</summary>
    private static PixelPoint CentreOf(Rect bounds)
        => new((int)(bounds.X + bounds.Width / 2), (int)(bounds.Y + bounds.Height / 2));

    protected override void OnSegmentRecorded(int index)
    {
        while (_segRegion.Count <= index) _segRegion.Add(default);
        while (_segWallpaper.Count <= index) _segWallpaper.Add(false);
        while (_segGlass.Count <= index) _segGlass.Add(false);
        while (_segSource.Count <= index) _segSource.Add(null);
        while (_segAnchor.Count <= index) _segAnchor.Add(MaterialAnchor.Element);
        _segRegion[index] = CaptureRegion;
        _segWallpaper[index] = _pendingWallpaper;
        _segGlass[index] = _pendingGlass;
        _segSource[index] = _pendingSource;
        _segAnchor[index] = _pendingAnchor;

        // The next segment starts undecided about all of them, as its own instances decide them.
        _pendingWallpaper = false;
        _pendingGlass = false;
        _pendingSource = null;
        _pendingAnchor = MaterialAnchor.Element;
    }

    protected override void OnSegmentInserted(int index)
    {
        while (_segRegion.Count < index) _segRegion.Add(default);
        while (_segWallpaper.Count < index) _segWallpaper.Add(false);
        while (_segGlass.Count < index) _segGlass.Add(false);
        while (_segSource.Count < index) _segSource.Add(null);
        while (_segAnchor.Count < index) _segAnchor.Add(MaterialAnchor.Element);
        _segRegion.Insert(index, index > 0 ? _segRegion[index - 1] : CaptureRegion);
        _segWallpaper.Insert(index, index > 0 && _segWallpaper[index - 1]);
        _segGlass.Insert(index, index > 0 && _segGlass[index - 1]);
        _segSource.Insert(index, index > 0 ? _segSource[index - 1] : null);
        _segAnchor.Insert(index, index > 0 ? _segAnchor[index - 1] : MaterialAnchor.Element);
    }

    protected override void BindSegment(int index)
    {
        if ((uint)index < (uint)_segRegion.Count) CaptureRegion = _segRegion[index];
        if ((uint)index < (uint)_segWallpaper.Count) _boundWallpaper = _segWallpaper[index];
        if ((uint)index < (uint)_segGlass.Count) _boundGlass = _segGlass[index];
        if ((uint)index < (uint)_segSource.Count) _boundSource = _segSource[index];
        if ((uint)index < (uint)_segAnchor.Count) _boundAnchor = _segAnchor[index];
    }

    /// <summary>Can this material join the segment being filled? Only if it agrees about everything a segment IS - the
    /// image bound to it, how that image is mapped, and the pass that reads it. A change of any of them flushes the
    /// batch, exactly as a change of texture does in the textured batch.</summary>
    public bool SameKind(Brush brush, ITexture source)
        => !HasPending
           || brush is not MaterialBrush m
           || (IsWallpaper(m.Material) == _pendingWallpaper && IsGlass(m.Material) == _pendingGlass
               && ReferenceEquals(source, _pendingSource) && (source == null || m.Anchor == _pendingAnchor));

    // The two halves of a segment's identity, asked by the mesh carrier too - so the answer is stated once here rather
    // than decided again over there.
    public static bool IsWallpaper(MaterialType material) => material == MaterialType.Mica;

    public static bool IsGlass(MaterialType material) => material == MaterialType.LiquidGlass;

    protected override void DrawSegment(IGraphicsDevice device, Buffer<MaterialRectItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        EnsureEffectForDraw(device);

        if (!BindSource(device, WallpaperSegment, CaptureRegion, _boundSource, _boundAnchor,
                Effect.SourceTexture, Effect.SourceSampler, SourceUvParam)) return;

        base.DrawSegment(device, buffer, count, firstInstance, projection);
    }

    /// <summary>Point an effect at the backdrop a material is about to read; false when there is none, and then the
    /// caller must draw NOTHING (an unbound sampler paints whatever descriptor was left there).
    ///
    /// <para>Parameterised because BOTH carriers use it - the analytic shapes here and the meshes in
    /// <see cref="Retained.InstancedFillCollector"/>. A capture is a copy of the frame taken between two draws, so two
    /// owners would mean two copies of one region taken at different moments.</para></summary>
    public bool BindSource(IGraphicsDevice device, bool wallpaper, Rect2D region, ITexture own, MaterialAnchor anchor,
        EffectParameter texture, EffectParameter sampler, EffectParameter uv)
    {
        var samplers = ((GraphicsDevice)device).SamplerStates;

        // The author's own picture replaces the built-in source: no capture, no desktop. The anchors differ only in the
        // rectangle it is laid over.
        if (own != null)
        {
            uv.SetValue(ToUv(OwnSourceRect(device, anchor)));
            texture.SetResource(own);
            sampler.SetResource(samplers.LinearClampToEdge);
            return true;
        }

        if (wallpaper)
        {
            // No capture at all: the picture is prepared once and only re-read when the desktop says it changed. The
            // texture is asked for HERE rather than at bake time so a wallpaper that changed between recording and
            // replaying is picked up by the replay too.
            var picture = _wallpaper.Texture(device, CentreOf(WindowBounds));
            if (picture == null) return false;

            // Computed NOW, every draw: the window may have been dragged since this segment was recorded, and it is the
            // window moving under a still picture that makes mica read as a window onto the desktop.
            uv.SetValue(ToUv(WallpaperRect()));
            texture.SetResource(picture);
            // Repeat only for a TILED desktop - every other layout places one copy, and repeating it would wrap the
            // picture's far edge into a pane sitting near the screen's border.
            sampler.SetResource(_wallpaper.Tiles ? samplers.LinearRepeat : samplers.LinearClampToEdge);
            return true;
        }

        // Capture FIRST, then draw - see BackdropCapture: the copy is only correct once everything meant to be
        // BEHIND the material is already in the frame.
        if (!_capture.Capture(device, region) || _capture.Image == null) return false;

        uv.SetValue(ToUv(new Vector4F(region.Offset.X, region.Offset.Y, region.Extent.Width, region.Extent.Height)));
        texture.SetResource(_capture.Image);
        sampler.SetResource(samplers.LinearClampToEdge);
        return true;
    }

    /// <summary>THE one statement of what this batch draws - a render unit asks THIS, never its own copy. Shape does
    /// not come into it: every figure goes through the same pass, distinguished by a flag in its record.</summary>
    public static bool WantsBatch(Brush brush, Pen pen)
    {
        if (!Enabled) return false;
        if (brush is not MaterialBrush) return false;
        if (pen == null) return true;

        // A SOLID, WHOLE pen only. The stroke is composited by the shared CompositeFillStroke, exactly as in the other
        // SDF batches, but dashes and trims need the arc-length machinery those passes carry and this one does not - so
        // they go to the per-unit path rather than being drawn as a solid ring nobody asked for.
        if (!RectBatchCollector.IsPenBatchable(pen)) return false;
        if (pen.DashStrokeArray is { Count: > 0 }) return false;
        return pen.TrimStart <= 0 && pen.TrimEnd >= 1;
    }

    public static bool WantsBatch(RectanglePayload p) => WantsBatch(p.Brush, p.Pen);

    public static bool WantsBatchEllipse(EllipsePayload p) => WantsBatch(p.Brush, p.Pen);

    public static bool WantsBatchPolygon(RegularPolygonPayload p) => WantsBatch(p.Brush, p.Pen);

    public bool CanBatch(RectanglePayload p) => WantsBatch(p.Brush, p.Pen);

    public bool CanBatchEllipse(EllipsePayload p) => WantsBatch(p.Brush, p.Pen);

    public bool CanBatchPolygon(RegularPolygonPayload p) => WantsBatch(p.Brush, p.Pen);

    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds,
        int transformSlot = 0, int fadeSlot = -1, ITexture source = null, int clipSlot = -1)
        => Add(p.Brush, p.DestinationRect, p.CornerRadius, p.Pen, ShapeRect, opacity, scissor, logicalBounds,
            transformSlot, fadeSlot, source, clipSlot: clipSlot);

    /// <summary>An ELLIPSE filled with a material. Same pass, same record - the shader branches on the shape flag baked
    /// into Params.x, exactly as the pattern batch does it, so no separate collector or pass is needed.</summary>
    public bool TryAddEllipse(EllipsePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds,
        int transformSlot = 0, int fadeSlot = -1, ITexture source = null, int clipSlot = -1)
        => Add(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, p.Pen, ShapeEllipse, opacity, scissor,
            logicalBounds, transformSlot, fadeSlot, source, clipSlot: clipSlot);

    /// <summary>A regular POLYGON filled with a material. Its corner count and start angle ride in the radii, which is
    /// what the shape function reads for this flag.</summary>
    public bool TryAddPolygon(RegularPolygonPayload p, Matrix4x4F world, double opacity, Rect2D scissor,
        Rect logicalBounds, int transformSlot = 0, int fadeSlot = -1, ITexture source = null, int clipSlot = -1)
        => Add(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, p.Pen, ShapePolygon, opacity, scissor,
            logicalBounds, transformSlot, fadeSlot, source, p.Corners, (float)p.StartAngle, clipSlot);

    // The shape flags, as the shader reads them out of Params.x: a real radius for a rounded rect, and negative values
    // standing for the other figures. The same encoding the pattern batch uses - one convention across the procedural
    // fills, rather than one per collector.
    private const float ShapeRect = 0f;
    private const float ShapeEllipse = -1f;
    private const float ShapePolygon = -2f;

    // ONE bake for every figure. What changes between them is the shape flag and, for a polygon, the two numbers that
    // describe it; everything else - bounds, tint, knobs, the slots - is the same record.
    /// <summary>Bake one material fill into an instance record WITHOUT appending it - what the MOVE path needs to
    /// rewrite the record a unit already occupies, exactly as the sibling batches expose. The walk goes through
    /// <see cref="Add"/>, which calls this and then keeps the segment's own state (wallpaper/glass/source).</summary>
    public static bool BakeItem(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot,
        ITexture source, int clipSlot, out MaterialRectItem item)
        => BakeCore(p.Brush, p.DestinationRect, p.CornerRadius, p.Pen, ShapeRect, opacity, transformSlot, fadeSlot,
            source, 0, 0f, clipSlot, out item);

    /// <inheritdoc cref="BakeItem"/>
    public static bool BakeEllipseItem(EllipsePayload p, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot,
        ITexture source, int clipSlot, out MaterialRectItem item)
        => BakeCore(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, p.Pen, ShapeEllipse, opacity,
            transformSlot, fadeSlot, source, 0, 0f, clipSlot, out item);

    /// <inheritdoc cref="BakeItem"/>
    public static bool BakePolygonItem(RegularPolygonPayload p, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot,
        ITexture source, int clipSlot, out MaterialRectItem item)
        => BakeCore(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, p.Pen, ShapePolygon, opacity,
            transformSlot, fadeSlot, source, p.Corners, (float)p.StartAngle, clipSlot, out item);

    private static bool BakeCore(Brush brush, Rect destination, ProceduralGeometry.CornerRadius corners, Pen pen, float shape,
        double opacity, int transformSlot, int fadeSlot, ITexture source,
        int polygonCorners, float polygonStart, int clipSlot, out MaterialRectItem item)
    {
        item = default;
        if (brush is not MaterialBrush material) return false;

        var tint = material.TintColor;
        // Baked in LOGICAL units (scale 1), like the bounds and the radii beside them: this batch has no world matrix on
        // the CPU at all - the vertex shader takes the device scale from the transform slot and hands it to the pixel
        // shader, which is where the width becomes pixels.
        RectBatchCollector.BakeStroke(pen, opacity * material.Opacity, 1f, out var strokeColor, out var stroke0, out var stroke1);
        var radii = shape == ShapePolygon
            ? new Vector4F(polygonCorners, polygonStart, 0f, 0f)
            : new Vector4F((float)corners.TopLeft, (float)corners.TopRight,
                (float)corners.BottomRight, (float)corners.BottomLeft);

        item = new MaterialRectItem
        {
            Bounds = new Vector4F((float)destination.X, (float)destination.Y,
                (float)destination.Width, (float)destination.Height),
            Params = new Vector4F(shape == ShapeRect ? (float)corners.TopLeft : shape,
                transformSlot, (float)Math.Clamp(opacity * material.Opacity, 0.0, 1.0), fadeSlot),
            Radii = radii,
            // TintOpacity alone: how much the tint covers the capture. The element's own opacity is the fill's ALPHA
            // (Params.z), not a weakening of the tint - folding it in here left a half-transparent material fully
            // opaque, just less tinted.
            Tint = new Vector4F(tint.R / 255f, tint.G / 255f, tint.B / 255f,
                (float)Math.Clamp(material.TintOpacity, 0.0, 1.0)),
            // .w says the picture is pinned to the ELEMENT, and the shader then takes its coordinates from the fragment's
            // place in the shape instead of from its place in the frame. It cannot be a rectangle in SourceUv like the
            // other anchors: each instance in the segment has its own, and a rotated shape has none at all.
            Knobs = new Vector4F((float)material.BlurAmount, (float)material.NoiseAmount, (float)material.Refraction,
                source != null && material.Anchor == MaterialAnchor.Element ? 1f : 0f),
            StrokeColor = strokeColor,
            Stroke0 = stroke0,
            Stroke1 = stroke1,
            Clip = new Vector4F(clipSlot, 0, 0, 0)   // the rounded ancestor clip, -1 = none
        };
        return true;
    }

    private bool Add(Brush brush, Rect destination, ProceduralGeometry.CornerRadius corners, Pen pen, float shape,
        double opacity, Rect2D scissor, Rect logicalBounds, int transformSlot, int fadeSlot, ITexture source,
        int polygonCorners = 0, float polygonStart = 0f, int clipSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!BakeCore(brush, destination, corners, pen, shape, opacity, transformSlot, fadeSlot, source,
                polygonCorners, polygonStart, clipSlot, out var item)) return false;

        Items[Count++] = item;

        var material = (MaterialBrush)brush;
        if (IsWallpaper(material.Material)) _pendingWallpaper = true;
        if (IsGlass(material.Material)) _pendingGlass = true;
        if (source != null)
        {
            _pendingSource = source;
            _pendingAnchor = material.Anchor;
        }

        MarkPending(scissor, logicalBounds);
        return true;
    }

    /// <summary>Where the capture will be taken from - the segment's device-pixel footprint, which the cache knows only
    /// at flush. Nothing is written into the instances: the rectangle reaches the shader as a parameter set at draw
    /// time, so a replayed frame gets this segment's own value back through <see cref="BindSegment"/>.</summary>
    public void SetCaptureRect(Rect2D region) => CaptureRegion = region;

    /// <summary>Where an author's OWN picture lands, in the frame's device pixels. Element has no entry here on purpose:
    /// it cannot be a rectangle in the frame (every instance has its own, and a rotated shape has none), so the shader
    /// answers it from the fragment's place in the geometry and ignores what this returns.</summary>
    private Vector4F OwnSourceRect(IGraphicsDevice device, MaterialAnchor anchor)
    {
        var viewports = ((GraphicsDevice)device).CurrentViewports;
        var frame = viewports is { Length: > 0 }
            ? new Vector4F(0f, 0f, viewports[0].Width, viewports[0].Height)
            : new Vector4F(0f, 0f, 1f, 1f);

        if (anchor != MaterialAnchor.Desktop) return frame;

        // The virtual screen, moved into the window - the same subtraction the wallpaper does, and physical on both
        // sides for the same reason (see WallpaperRect). No desktop is needed for the picture itself, only for the
        // extent it is stretched over.
        var screen = PlatformSettings.VirtualScreen;
        if (screen.Width <= 0 || screen.Height <= 0) return frame;   // no desktop geometry: behave as Window

        var window = WindowBounds;
        _lastWindowBounds = window;
        return new Vector4F(
            (float)(screen.X - window.X),
            (float)(screen.Y - window.Y),
            (float)screen.Width,
            (float)screen.Height);
    }

    /// <summary>Where the wallpaper lands, in the FRAME's device pixels: the desktop rectangle the picture was placed
    /// into, moved into the window and scaled. Recomputed per instance because the window moves - and that movement is
    /// exactly what makes mica look like a window onto the desktop rather than a painted panel.</summary>
    private Vector4F WallpaperRect()
    {
        // ONE reading of the position for the whole computation, not two. Asking twice - once to pick the monitor and
        // again to subtract - lets the window move in between, so the picture could be placed on the monitor the window
        // is leaving and then offset by where it already is.
        var window = WindowBounds;
        _lastWindowBounds = window;

        // The desktop has to have been read at least once before there is a rectangle to compute. Cheap to ask - a path
        // and a timestamp, compared as one record, and only re-read when that comparison differs.
        _wallpaper.Ensure(CentreOf(window));

        var placement = _wallpaper.Placement(PlatformSettings.VirtualScreen);
        if (placement.Width <= 0 || placement.Height <= 0) return Vector4F.Zero;

        // ALREADY PHYSICAL, both of them: the desktop states where it put the picture in physical pixels, and the
        // window's corner comes from the OS in the same units. Their difference is therefore physical too, which is what
        // the shader wants - it works in the frame's device pixels. Scaling it again by the render scale was a mistake
        // invisible at 100% and a picture off by half its width at 150%.
        //
        // Measured, when the drag wobble was being hunted: every number here comes out exact - the wallpaper's origin,
        // its width, and a step of precisely 1.000000 per pixel of window movement. There is no rounding drift in this
        // mapping, which is what rules it out as the cause.
        return new Vector4F(
            (float)(placement.X - window.X),
            (float)(placement.Y - window.Y),
            (float)placement.Width,
            (float)placement.Height);
    }
}
