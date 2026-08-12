using System.Collections.Generic;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
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

    /// <summary>Whether this fill belongs to the textured batch at all. STATIC because it is asked before the collector
    /// exists: one is built on the first textured fill a cache meets, and most caches never meet one.</summary>
    public static bool WantsBatch(RectanglePayload p) => Enabled && p.Brush is ImageBrush or NineSliceBrush;

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
        if (!Bake(p, world, opacity, transformSlot, out var baked))
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

    // Bake a textured fill into 1 or 9 instance records. Position -> world; false on a rotated/sheared world (the
    // axis-aligned instance cannot hold it) so the caller falls back to the per-unit path.
    private static bool Bake(RectanglePayload p, Matrix4x4F world, double opacity, int transformSlot, out TexRectItem[] items)
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
        var r = p.DestinationRect;
        var bounds = new Rect(r.X * sx + tx, r.Y * sy + ty, r.Width * sx, r.Height * sy);
        var radius = (float)(p.CornerRadius.TopLeft * sx);

        items = p.Brush switch
        {
            NineSliceBrush nine => NineSlice.Bake(nine, bounds, opacity, transformSlot, sx, sy),
            ImageBrush image => [Single(image, bounds, radius, opacity, transformSlot, sx, sy)],
            _ => null
        };
        return items != null;
    }

    // One record for the plain textured fill. WHERE it is drawn and WHAT it samples come from the brush's tiling and
    // stretch (see ImageTiling) - stretched across the shape, fitted inside it, or repeated.
    private static TexRectItem Single(ImageBrush brush, Rect bounds, float radius, double opacity, int transformSlot,
        double scaleX, double scaleY)
    {
        var tint = brush.Tint.ToVector4();
        tint.W *= (float)(opacity * brush.Opacity);

        var (drawn, uvRect, uvRepeat) = ImageTiling.Layout(brush, bounds, scaleX, scaleY);

        return new TexRectItem
        {
            Bounds = new Vector4F((float)drawn.X, (float)drawn.Y, (float)drawn.Width, (float)drawn.Height),
            Params = new Vector4F(radius, transformSlot, 0, 0),
            UvRect = uvRect,
            UvRepeat = uvRepeat,
            Tint = tint
        };
    }
}
