using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering.RenderUnits;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Text glyph batch (docs/TEXT_GLYPH_BATCH_PLAN.md §9 Stage 2): collects same-clip + same-atlas visible text blocks -
// their glyphs baked to WORLD space on the CPU - into ONE instanced draw per segment (FontRenderer.DrawBatch reads the
// foreground from each glyph's per-instance colour, so blocks of different colours share the draw). Segment/buffer/
// overlap machinery is in BatchCollector; this adds glyph baking + the atlas-bound draw. Rendered ABOVE the rect batch.
internal sealed class TextBatchCollector : BatchCollector<FontItem>
{
    private FontAtlas _atlas;            // the pending segment's atlas (one bind per draw)
    private FontRenderer _fontRenderer;

    public TextBatchCollector() : base(8192) { }

    // Whether this block can batch at all (and its atlas). Canonical MSDF only - the batch pixel shader is the MSDF
    // variant; outline / gradient-AA / empty / non-solid-foreground text (and UseTextBatch=off) fall back to the
    // per-block direct draw. The clip-group check lives in RenderCache; the atlas check is SameAtlas below.
    public bool CanBatch(TextRenderComponent tc, out FontAtlas atlas)
    {
        atlas = null;
        if (!FontRenderer.UseTextBatch) return false;   // off -> every block falls back to the per-block direct draw
        var layout = tc.TextLayout;
        if (layout == null || layout.ElementsCount == 0 || layout.FontAtlas == null) return false;
        var fr = tc.FontRenderer;
        if (fr == null || !fr.UseCanonicalMsdf || fr.UseOutline) return false;
        if (tc.Foreground is not SolidColorBrush) return false;
        atlas = layout.FontAtlas;
        return true;
    }

    // Still the pending segment's atlas? (One draw binds one atlas; a change flushes both batches - see RenderCache.)
    public bool SameAtlas(FontAtlas atlas) => !Active || _atlas == atlas;

    // Bake one block's glyphs (positions -> world, foreground -> per-instance colour) into the pending segment. False
    // only if it can't be baked - a rotated/sheared world (the axis-aligned FontItem rect can't hold it) or a
    // GPU-buffer overflow this frame - and the caller then renders that block via the per-block direct draw.
    public bool TryAdd(TextRenderComponent tc, Matrix4x4F world, Rect2D scissor, FontAtlas atlas, Rect logicalBounds)
    {
        var layout = tc.TextLayout;
        var n = (int)layout.ElementsCount;

        EnsureCpuCapacity(Count + n);
        if (Count + n > GpuCapacity) return false;   // won't fit this frame's GPU buffer -> direct

        var area = tc.RenderingParameters.TextArea;
        var color = ((SolidColorBrush)tc.Foreground).Color.ToVector4();
        color.W *= (float)tc.RenderData.Opacity;      // fold the element's opacity into the glyph alpha

        // Writes n glyphs into Items and advances Count; returns false (no write) for a rotated/sheared world.
        if (!layout.TryBakeWorldGlyphs(Items, ref Count, world, new Vector2F((float)area.X, (float)area.Y), color))
            return false;

        _atlas = atlas;
        _fontRenderer = tc.FontRenderer;
        MarkPending(scissor, logicalBounds);
        return true;
    }

    protected override void DrawSegment(IGraphicsDevice device, Buffer<FontItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
        => _fontRenderer.DrawBatch(device.SamplerStates.LinearFont, _atlas, buffer, count, projection, firstInstance);
}
