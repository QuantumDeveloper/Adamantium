namespace Adamantium.Graphics.Fonts;

/// <summary>An immutable snapshot of a <see cref="TextLayout"/>'s shaped glyphs in LOCAL coordinates, taken on the
/// record/update thread so the render/applier path bakes text WITHOUT reading the live layout (which the owning TextBlock
/// reshapes IN PLACE). It holds a private copy of the glyph items; the <see cref="FontAtlas"/> is shared by reference -
/// its glyph tiles are append-only and never move, so an already-captured run's UVs stay valid even as other text adds
/// glyphs. The glyphs are LOCAL: the text batch packs them into a per-instance GPU buffer and the glyph shader applies the
/// block's world/node transform, so there is no per-glyph CPU world bake (docs/RENDER_THREAD_PLAN.md).</summary>
public sealed class FrozenGlyphRun(FontItem[] glyphs, int count, FontAtlas atlas)
{
    public FontItem[] Glyphs { get; } = glyphs;
    public int Count { get; } = count;
    public FontAtlas Atlas { get; } = atlas;
}
