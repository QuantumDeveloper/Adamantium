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
// (each fill = one per-instance TexRectItem; the pixel shader reconstructs the rounded rect from an SDF and samples).
// The sibling of the solid/gradient/pattern SDF collectors - an ImageBrush or NineSliceBrush fill routes here.
//
// WHICH texture is not in the record: ONE texture is bound per SEGMENT, the way TextBatchCollector binds one atlas per
// segment. Bindless would let a segment mix textures, but the engine has no bindless path (textures bind as effect
// parameters) and this driver is documented to fall over on richer texture use - see docs/NINE_SLICE_PLAN.md. Cost: a
// texture change breaks the batch, which for UI is a handful of times per frame.
internal sealed class TexRectCollector : SdfBatchCollector<TexRectItem>
{
    public static bool Enabled = true;

    private ITexture _texture;                          // the pending segment's texture (one bind per draw)
    private readonly List<ITexture> _segState = new();  // parallel to the base segment list, for the clean-frame replay

    public TexRectCollector() : base(256) { }

    protected override IEffectPass DrawPass => Effect.BatchTexRectPass;

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

    protected override void DrawSegment(IGraphicsDevice device, Buffer<TexRectItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        // NO texture, NO draw. The heap path passes a texture as an INDEX into the device-wide descriptor heap, written
        // into push data by whoever bound it last; drawing this pass without binding one leaves a stale index in place and
        // the shader samples whatever descriptor sits there - in practice the glyph atlas, smeared across the frame. A
        // segment with nothing to sample has nothing to draw either.
        if (_texture == null)
        {
            return;
        }

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
        ITexture texture, int transformSlot = 0)
    {
        var slices = NineSlice.Count(p.Brush);
        EnsureCpuCapacity(Count + slices);
        if (Count + slices > GpuCapacity)
        {
            return false;
        }
        if (!Bake(p.Brush, p.DestinationRect, p.CornerRadius, false, world, opacity, transformSlot, out var baked))
        {
            return false;
        }

        _texture = texture;
        foreach (var item in baked)
        {
            Items[Count++] = item;
        }
        MarkPending(scissor, logicalBounds);
        return true;
    }

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
        ITexture texture, int transformSlot = 0)
    {
        EnsureCpuCapacity(Count + 1);
        if (Count + 1 > GpuCapacity)
        {
            return false;
        }
        if (!Bake(p.Brush, p.DestinationRect, ProceduralGeometry.CornerRadius.Empty, true, world, opacity, transformSlot, out var baked))
        {
            return false;
        }

        _texture = texture;
        Items[Count++] = baked[0];
        MarkPending(scissor, logicalBounds);
        return true;
    }

    // Bake a textured fill into 1 or 9 instance records. Position -> world; false on a rotated/sheared world (the
    // axis-aligned instance cannot hold it) so the caller falls back to the per-unit path. The four corner radii ride in
    // Radii, scaled with the world; Params.x carries the LARGEST of them, or -1 as the ELLIPSE shape flag.
    private static bool Bake(Brush brush, Rect destinationRect, ProceduralGeometry.CornerRadius corners, bool ellipse, Matrix4x4F world, double opacity,
        int transformSlot, out TexRectItem[] items)
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
        var radii = ellipse ? Vector4F.Zero : RectBatchCollector.BakeRadii(corners, r, sx);
        var radius = ellipse ? -1f : RectBatchCollector.MaxOf(radii);

        items = brush switch
        {
            NineSliceBrush nine => NineSlice.Bake(nine, bounds, opacity, transformSlot, sx, sy),
            TileBrush tile => [Single(tile, bounds, radius, radii, opacity, transformSlot, sx, sy)],
            _ => null
        };
        return items != null;
    }

    // One record for the plain textured fill. WHERE it is drawn and WHAT it samples come from the brush's tiling and
    // stretch (see ImageTiling) - stretched across the shape, fitted inside it, or repeated.
    private static TexRectItem Single(TileBrush brush, Rect bounds, float radius, Vector4F radii, double opacity, int transformSlot,
        double scaleX, double scaleY)
    {
        var tint = brush.Tint.ToVector4();
        tint.W *= (float)(opacity * brush.Opacity);

        var layout = ImageTiling.Layout(brush, bounds, scaleX, scaleY);

        // The SHAPE stays the shape; only the content inside each tile is fitted. Handing the fitted rect over as the
        // bounds shrank the SDF itself, so a Uniform fill turned a circle into an oval.
        return new TexRectItem
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
