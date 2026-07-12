using System;

namespace Adamantium.Graphics.Fonts;

/// <summary>An immutable snapshot of a <see cref="TextLayout"/>'s shaped glyphs in LOCAL coordinates, taken on the
/// record/update thread so the render/applier path bakes text WITHOUT reading the live layout (which the owning TextBlock
/// reshapes IN PLACE). It holds a private copy of the glyph items; the <see cref="FontAtlas"/> is shared by reference -
/// its glyph tiles are append-only and never move, so an already-captured run's UVs stay valid even as other text adds
/// glyphs. The glyphs are LOCAL: the batch packs them into a per-instance GPU buffer and the glyph shader applies the
/// block's world/node transform; the direct/composite fallback uploads them into the component's own vertex buffer. Either
/// way there is no per-glyph CPU world bake and no live-layout read at draw (docs/RENDER_THREAD_PLAN.md).</summary>
public sealed class FrozenGlyphRun(FontItem[] glyphs, int count, FontAtlas atlas, float fontSize)
{
    public FontItem[] Glyphs { get; } = glyphs;
    public int Count { get; } = count;
    public FontAtlas Atlas { get; } = atlas;
    public float FontSize { get; } = fontSize;

    /// <summary>Screen-px reach of a glyph's effect (outline/glow) beyond its body = the atlas margin scaled to the font
    /// size; the direct/composite text target + composite quad are padded by it so edge-glyph effects aren't clipped.
    /// Mirrors <c>TextLayout.EffectPadding</c>, computed from the FROZEN atlas + size so the applier never reads the live
    /// layout.</summary>
    public int EffectPadding => Atlas == null ? 0 : (int)Math.Ceiling(Atlas.GlyphMargin * FontSize / Atlas.MSDFTextureSize);
}
