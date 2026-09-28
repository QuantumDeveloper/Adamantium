using System;

namespace Adamantium.Graphics.Fonts;

/// <summary>An immutable snapshot of a <see cref="TextLayout"/>'s shaped glyphs in local coordinates, taken on the record
/// thread so rendering never reads the live layout. The shared <see cref="FontAtlas"/> tiles never move.</summary>
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
