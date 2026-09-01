using System.Collections.Generic;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// TEXTURED rounded-rect batch: draws many rounded-rect fills whose colour is SAMPLED from an image in ONE instanced draw
// (each fill = one per-instance TextureItem; the pixel shader reconstructs the rounded rect from an SDF and samples).
// The sibling of the solid/gradient/pattern SDF collectors - an ImageBrush or NineSliceBrush fill routes here.
//
// WHICH texture is not in the record: ONE texture is bound per SEGMENT, the way TextBatchCollector binds one atlas per
// segment. Bindless would let a segment mix textures, but the engine has no bindless path (textures bind as effect
// parameters) and this driver is documented to fall over on richer texture use - see docs/NINE_SLICE_PLAN.md. Cost: a
// texture change breaks the batch, which for UI is a handful of times per frame.
internal sealed class TextureBatchCollector : BrushSdfCollector<TextureItem>
{
    public static bool Enabled = true;

    private ITexture _texture;                          // the pending segment's texture (one bind per draw)
    private readonly List<ITexture> _segState = new();  // parallel to the base segment list, for the clean-frame replay

    public TextureBatchCollector() : base(256) { }

    protected override IEffectPass DrawPass => Effect.TextureSdfPass;

    protected override void OnBeginFrame(IGraphicsDevice device)
    {
        base.OnBeginFrame(device);
        _segState.Clear();
    }

    protected override void OnSegmentRecorded(int index)
    {
        while (_segState.Count <= index) _segState.Add(null);
        _segState[index] = _texture;
    }

    protected override void OnSegmentInserted(int index)
    {
        while (_segState.Count < index) _segState.Add(null);
        _segState.Insert(index, index > 0 ? _segState[index - 1] : null);
    }

    protected override void BindSegment(int index) => _texture = _segState[index];

    /// <summary>Still the pending segment's texture? One draw binds one texture, so a change flushes the batch - the
    /// caller asks this before adding (mirrors TextBatchCollector.SameAtlas).</summary>
    public bool SameTexture(ITexture texture) => !Active || _texture == null || _texture == texture;

    protected override void DrawSegment(IGraphicsDevice device, Buffer<TextureItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        // NO texture, NO draw. The heap path passes a texture as an INDEX into the device-wide descriptor heap, written
        // into push data by whoever bound it last; drawing this pass without binding one leaves a stale index in place and
        // the shader samples whatever descriptor sits there - in practice the glyph atlas, smeared across the frame. A
        // segment with nothing to sample has nothing to draw either.
        if (_texture == null)
        {
            return;
        }

        EnsureEffectForDraw(device);
        Effect.SourceTexture.SetResource(_texture);
        Effect.SourceSampler.SetResource(((GraphicsDevice)device).SamplerStates.LinearClampToEdge);
        base.DrawSegment(device, buffer, count, firstInstance, projection);
    }

    /// <summary>The GPU texture a brush samples, or null - either it is not a textured brush, or its source is still
    /// decoding (in which case the next re-render picks it up, the way ImageRenderUnit does). ONE statement of "which
    /// brushes carry a texture", asked by every render unit that can route here rather than restated per shape.</summary>
    internal static ITexture BrushTexture(Brush brush, IResourceFactory factory, Size size = default, IUIComponent owner = null)
    {
        var source = brush switch
        {
            TileBrush tile => tile.ContentSource,
            NineSliceBrush nine => nine.Source,
            // A material with a picture of its own. It is not a textured fill and never goes through this batch - it
            // only wants the SAME answer to "what texture does this brush name", which is the question this method is.
            //
            // MICA ONLY, and stated HERE so it is stated once: acrylic and liquid glass ARE "what is directly beneath
            // this element", and a picture from elsewhere would not make them a variant of themselves - it would make
            // them something else wearing their name.
            MaterialBrush material => MaterialRectCollector.IsWallpaper(material.Material) ? material.Source : null,
            _ => null
        };

        // A LIVE source has to be DRAWN before it can be sampled. Asked every frame and cheap: a picture that is still
        // current does nothing (see VisualBrushRaster).
        if (brush is VisualBrush live)
        {
            VisualBrushRaster.Ensure(live, owner);
            source = live.ContentSource;
        }

        if (source is BitmapSource bitmap) return bitmap.GetOrCreateTexture(factory);

        // A VECTOR source has no pixels to sample, so this is where the raster fallback earns its keep: hand over the
        // bake if there is one, and otherwise queue it and draw nothing this frame - the same "not ready yet" answer a
        // picture still being decoded gives. Baked at the rect it is DRAWN in, not at the fill box, so it carries the
        // aspect Stretch asked for (see ImageTiling.BakeSize). A nine-slice always fills its box.
        if (source is DrawingImage vector)
        {
            var bakeSize = brush is TileBrush tileBrush ? ImageTiling.BakeSize(tileBrush, size) : size;
            var baked = DrawingImageRaster.Get(vector, bakeSize, owner);
            if (baked != null)
            {
                return baked.GetOrCreateTexture(factory);
            }

            DrawingImageRaster.Request(vector, bakeSize, owner);
        }

        return null;
    }

    /// <summary>Whether this fill belongs to the textured batch at all. STATIC because it is asked before the collector
    /// exists: one is built on the first textured fill a cache meets, and most caches never meet one.</summary>
    public static bool WantsBatch(RectanglePayload p) => Enabled && p.Brush is TileBrush or NineSliceBrush;

    public bool CanBatch(RectanglePayload p) => WantsBatch(p);

    /// <summary>Bake one textured fill into the pending segment. An <see cref="ImageBrush"/> is one instance; a
    /// <see cref="NineSliceBrush"/> is NINE - the corners at their own size, the edges and centre stretched or tiled -
    /// which is the whole trick: one batch, one texture, nine records.</summary>
    public bool TryAdd(RectanglePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds,
        ITexture texture, int transformSlot = 0, int fadeSlot = -1, int clipSlot = -1)
    {
        var slices = NineSlice.Count(p.Brush);
        EnsureCpuCapacity(Count + slices);
        if (Count + slices > GpuCapacity)
        {
            return false;
        }
        if (!Bake(p.Brush, p.DestinationRect, p.CornerRadius, BrushShape.Rect, world, opacity, transformSlot, fadeSlot, clipSlot, out var baked))
        {
            return false;
        }

        _texture = texture;
        LastFirst = Count;
        LastCount = baked.Length;
        foreach (var item in baked)
        {
            Items[Count++] = item;
        }
        MarkPending(scissor, logicalBounds);
        return true;
    }

    /// <summary>Bake ONE textured fill into a record without appending it - what the MOVE path needs to rewrite the
    /// record a unit already occupies. Refuses a brush that bakes more than one record (a nine-slice): a run of nine
    /// cannot be rewritten through a single slot, and the caller falls back to the walk.</summary>
    public static bool BakeRun(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot,
        int clipSlot, out TextureItem[] items)
        => Bake(p.Brush, p.DestinationRect, p.CornerRadius, BrushShape.Rect, world, opacity, transformSlot, fadeSlot,
            clipSlot, out items);

    /// <summary>How many records this brush bakes: ONE for a picture, NINE for a nine-slice. The move path asks before
    /// patching - a run whose length changed is not something a rewrite in place can express, and that one takes the
    /// walk.</summary>
    public static int RecordCount(Brush brush) => NineSlice.Count(brush);

    /// <inheritdoc cref="BakeSingle"/>
    public static bool BakeSingleEllipse(EllipsePayload p, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot,
        int clipSlot, out TextureItem item)
        => BakeOne(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, BrushShape.Ellipse, world, opacity, transformSlot, fadeSlot, clipSlot, out item);

    /// <inheritdoc cref="BakeSingle"/>
    public static bool BakeSinglePolygon(RegularPolygonPayload p, Matrix4x4F world, double opacity, int transformSlot, int fadeSlot,
        int clipSlot, out TextureItem item)
        => BakeOne(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, BrushShape.Polygon(p, (float)world.M11), world, opacity, transformSlot, fadeSlot, clipSlot, out item);

    private static bool BakeOne(Brush brush, Rect destination, ProceduralGeometry.CornerRadius corners, BrushShape shape,
        Matrix4x4F world, double opacity, int transformSlot, int fadeSlot, int clipSlot, out TextureItem item)
    {
        item = default;
        if (!Bake(brush, destination, corners, shape, world, opacity, transformSlot, fadeSlot, clipSlot, out var baked)
            || baked.Length != 1)
        {
            return false;
        }
        item = baked[0];
        return true;
    }

    /// <summary>Where the LAST accepted fill landed and how many records it took - one for a plain picture, nine for a
    /// nine-slice. The move path needs both: patching a unit in place means rewriting ITS run, and a run of nine cannot
    /// be addressed by one slot number.</summary>
    public int LastFirst { get; private set; }

    /// <inheritdoc cref="LastFirst"/>
    public int LastCount { get; private set; }

    /// <summary>Ellipse variant: a full ellipse with a textured fill batches into the SAME textured pass, the shape told
    /// apart by a NEGATIVE baked corner radius (TexRectPS branches SdEllipse for it) - the trick PatternRectCollector
    /// uses, so no second pass and no second batch lifecycle. A <see cref="NineSliceBrush"/> does NOT come here: nine
    /// quads cut on four straight lines have no meaning on a curve, which is why CSS border-image is rect-only too.</summary>
    public static bool WantsBatchEllipse(EllipsePayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not TileBrush) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        return p.StartAngle <= 0.0 && p.SweepAngle >= 360.0;
    }

    public bool CanBatchEllipse(EllipsePayload p) => WantsBatchEllipse(p);

    public bool TryAddEllipse(EllipsePayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds,
        ITexture texture, int transformSlot = 0, int fadeSlot = -1, int clipSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity)
        {
            return false;
        }
        if (!Bake(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, BrushShape.Ellipse, world, opacity, transformSlot, fadeSlot, clipSlot, out var baked))
        {
            return false;
        }

        _texture = texture;
        // Where THIS fill landed. Set on every accepting path, not just the rect one: the move path reads it to learn
        // which record belongs to this unit, and a stale value from the previous fill points it at somebody else's -
        // dragging a textured polygon by one pixel then rewrote the textured RECTANGLE's record and the rectangle vanished.
        LastFirst = Count;
        LastCount = 1;
        Items[Count++] = baked[0];
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // POLYGON variant: a regular polygon with a textured fill (a picture, a drawing, a live element) batches into the
    // SAME pass. The shape stays a field - one instanced draw, crisp at any zoom - and only the source of the colour
    // differs. A NineSliceBrush does not come here for the same reason it does not come to the ellipse: nine quads cut on
    // four straight lines mean nothing on a shape that is not a rect.
    /// <summary>THE one statement for the polygon form - the render unit asks THIS, never its own copy.</summary>
    public static bool WantsBatchPolygon(RegularPolygonPayload p)
    {
        if (!Enabled) return false;
        if (p.Brush is not TileBrush || p.Brush is NineSliceBrush) return false;
        if (!RectBatchCollector.IsPenBatchable(p.Pen)) return false;
        return !RegularPolygonCollector.NeedsArcLength(p.Pen);
    }

    public bool CanBatchPolygon(RegularPolygonPayload p) => WantsBatchPolygon(p);

    public bool TryAddPolygon(RegularPolygonPayload p, Matrix4x4F world, double opacity, Rect2D scissor, Rect logicalBounds,
        ITexture texture, int transformSlot = 0, int fadeSlot = -1, int clipSlot = -1)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity) return false;
        if (!Bake(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, BrushShape.Polygon(p, (float)world.M11),
                world, opacity, transformSlot, fadeSlot, clipSlot, out var baked))
        {
            return false;
        }

        _texture = texture;
        // Where THIS fill landed. Set on every accepting path, not just the rect one: the move path reads it to learn
        // which record belongs to this unit, and a stale value from the previous fill points it at somebody else's -
        // dragging a textured polygon by one pixel then rewrote the textured RECTANGLE's record and the rectangle vanished.
        LastFirst = Count;
        LastCount = 1;
        Items[Count++] = baked[0];
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Bake a textured fill into 1 or 9 instance records. Position -> world; false on a rotated/sheared world (the
    // axis-aligned instance cannot hold it) so the caller falls back to the per-unit path. The four corner radii ride in
    // Radii, scaled with the world; Params.x carries the LARGEST of them, or -1 as the ELLIPSE shape flag.
    private static bool Bake(Brush brush, Rect destinationRect, ProceduralGeometry.CornerRadius corners, BrushShape shape, Matrix4x4F world, double opacity,
        int transformSlot, int fadeSlot, int clipSlot, out TextureItem[] items)
    {
        items = null;
        const float eps = 1e-4f;
        if (System.Math.Abs(world.M12) > eps || System.Math.Abs(world.M21) > eps)
        {
            return false;   // rotation/shear -> per-unit
        }

        var sx = world.M11;
        var sy = world.M22;
        var tx = world.M41;
        var ty = world.M42;
        var r = destinationRect;
        var bounds = new Rect(r.X * sx + tx, r.Y * sy + ty, r.Width * sx, r.Height * sy);
        var rectRadii = RectBatchCollector.BakeRadii(corners, r, sx);
        var radii = shape.RadiiFor(rectRadii);
        var radius = shape.RadiusFlag(rectRadii);

        items = brush switch
        {
            NineSliceBrush nine => NineSlice.Bake(nine, bounds, opacity, transformSlot, fadeSlot, sx, sy),
            TileBrush tile => [Single(tile, bounds, radius, radii, opacity, transformSlot, fadeSlot, sx, sy)],
            _ => null
        };
        if (items == null) return false;

        // Stamped HERE rather than inside each producer: a nine-slice makes nine records and a tile one, and every one
        // of them is cut - and faded - by the same ancestor. -1 = none, for either.
        // The FADE slot reached this collector as a parameter and was dropped on the floor for a long time, while the
        // bake had already taken the opacity CHAIN out of the tint (RenderCache calls FadeBySlot for this family): a
        // faded ancestor left the picture nearly at full strength. Measured on the Opacity stand at 0.86 of its
        // reference where every well-behaved family sat at 0.58.
        for (var i = 0; i < items.Length; i++) items[i].Clip = new Vector4F(clipSlot, fadeSlot, 0, 0);
        return true;
    }

    // One record for the plain textured fill. WHERE it is drawn and WHAT it samples come from the brush's tiling and
    // stretch (see ImageTiling) - stretched across the shape, fitted inside it, or repeated.
    private static TextureItem Single(TileBrush brush, Rect bounds, float radius, Vector4F radii, double opacity, int transformSlot, int fadeSlot,
        double scaleX, double scaleY)
    {
        var tint = brush.Tint.ToVector4();
        tint.W *= (float)(opacity * brush.Opacity);

        var layout = ImageTiling.Layout(brush, bounds, scaleX, scaleY);

        // The SHAPE stays the shape; only the content inside each tile is fitted. Handing the fitted rect over as the
        // bounds shrank the SDF itself, so a Uniform fill turned a circle into an oval.
        return new TextureItem
        {
            Bounds = new Vector4F((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height),
            Params = new Vector4F(radius, transformSlot, layout.Repeats ? 1f : 0f, layout.Mirror),
            Radii = radii,
            Tile = layout.Tile,
            Rotation = layout.Rotation,
            Drawn = layout.Drawn,
            UvRect = layout.UvRect,
            Tint = tint
        };
    }
}
