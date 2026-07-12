using System;
using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

/// <summary>An immutable snapshot of a <see cref="TextLayout"/>'s shaped glyphs, taken on the record/update thread so the
/// render/applier path can bake text WITHOUT reading the live layout (which the owning TextBlock reshapes IN PLACE). It
/// holds a private copy of the glyph items; the <see cref="FontAtlas"/> is shared by reference - its glyph tiles are
/// append-only and never move, so an already-captured run's baked UVs stay valid even as other text adds glyphs. This is
/// the text half of the render-side payload freeze (docs/RENDER_THREAD_PLAN.md).</summary>
public sealed class FrozenGlyphRun(FontItem[] glyphs, int count, FontAtlas atlas)
{
    public FontItem[] Glyphs { get; } = glyphs;
    public int Count { get; } = count;
    public FontAtlas Atlas { get; } = atlas;

    /// <summary>Bake the snapshot's glyphs (local quad -> world, foreground -> per-instance colour) into the batch buffer.
    /// Same math as <see cref="TextLayout.TryBakeWorldGlyphs"/>, but reads the frozen copy - no live layout touched.
    /// False (no write) for a rotated/sheared world (the axis-aligned rect can't hold it) or a dest overflow, so the
    /// caller falls back to the direct draw / flushes and retries.</summary>
    public bool BakeWorld(FontItem[] dest, ref int count, Matrix4x4F world, Vector2F textAreaOffset, Vector4F color)
    {
        if (Count == 0) return true;

        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;
        if (count + Count > dest.Length) return false;

        var sx = world.M11;
        var sy = world.M22;
        var tx = world.M41;
        var ty = world.M42;

        for (var i = 0; i < Count; i++)
        {
            var item = Glyphs[i];
            var d = item.ArrangeRect;   // local x, y, w, h
            item.ArrangeRect = new Vector4F(
                (d.X + textAreaOffset.X) * sx + tx,
                (d.Y + textAreaOffset.Y) * sy + ty,
                d.Z * sx,
                d.W * sy);
            item.Color = color;
            dest[count++] = item;
        }
        return true;
    }
}
