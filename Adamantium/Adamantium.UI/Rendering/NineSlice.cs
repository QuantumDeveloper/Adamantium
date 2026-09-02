using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Rendering;

/// <summary>Cuts a <see cref="NineSliceBrush"/> into the nine instance records the textured batch draws. Kept apart from
/// the collector because it is pure arithmetic on rectangles - the only part of a nine-slice worth testing on its own,
/// and the part that is easy to get subtly wrong.</summary>
internal static class NineSlice
{
    /// <summary>How many instance records a brush bakes into - nine for a nine-slice (eight without its centre), one for
    /// anything else. The collector asks BEFORE baking, to check the batch has room.</summary>
    public static int Count(Brush brush) => brush switch
    {
        NineSliceBrush nine => nine.DrawCenter ? 9 : 8,
        _ => 1
    };

    // A vector source cannot be baked bigger than this on either axis. The rule below asks what each BAND needs, and a
    // thin ornament magnified by a thick Border asks for a lot: a corner drawn at 40px from 2% of the source wants a
    // 2000px picture, of which only the four corners are ever seen at that density. Past this the corner softens instead
    // of the frame costing 16MB of render target, which is the trade a raster brush is entitled to make.
    private const double MaxBakeLength = 2048;

    /// <summary>The size a VECTOR source has to be baked at to dress this shape - in LOGICAL units, the device scale
    /// being the raster cache's business. The sibling of <see cref="ImageTiling.BakeSize"/>, and needed for the same
    /// reason: the picture a fill samples is made to order, so somebody has to say how big to make it.
    /// <para>What makes a FRAME different from a fill is that its pieces are not drawn at the shape's scale. A corner is
    /// drawn at <see cref="NineSliceBrush.Border"/> however large the panel is, so baking at the shape's size - which is
    /// what a fill wants, and what this used to do - spends the resolution on the middle and leaves the corners as
    /// whatever is left over. So each band is asked what it needs and the largest answer wins: a band drawn N units long
    /// out of a fraction F of the source needs N/F of source to be 1:1.</para></summary>
    public static Size BakeSize(NineSliceBrush brush, Size shape)
    {
        var source = brush?.Source;
        if (source == null) return shape;

        var (sourceWidth, sourceHeight) = PixelSize(source);
        if (sourceWidth <= 0 || sourceHeight <= 0) return shape;

        var slice = Clamp(brush.Slice);
        var border = brush.Border;

        // A REPEATED edge draws its motif at the motif's own size however long the strip is, so it never asks for more
        // than the source already has; only a STRETCHED one grows with the shape.
        var repeat = brush.EdgeMode != NineSliceEdgeMode.Stretch;

        return new Size(
            Axis(border.Left > 0 ? border.Left : slice.Left * sourceWidth,
                border.Right > 0 ? border.Right : slice.Right * sourceWidth,
                slice.Left, slice.Right, shape.Width, sourceWidth, repeat),
            Axis(border.Top > 0 ? border.Top : slice.Top * sourceHeight,
                border.Bottom > 0 ? border.Bottom : slice.Bottom * sourceHeight,
                slice.Top, slice.Bottom, shape.Height, sourceHeight, repeat));
    }

    // The most demanding band on one axis, in source units. A band drawn `length` long out of a `fraction` of the source
    // is 1:1 when the source is length/fraction across.
    private static double Axis(double near, double far, double nearFraction, double farFraction, double shapeLength,
        double sourceLength, bool repeat)
    {
        var middleFraction = Math.Max(0, 1 - nearFraction - farFraction);
        var middle = repeat ? middleFraction * sourceLength : Math.Max(0, shapeLength - near - far);

        var needed = 0.0;
        if (nearFraction > 0) needed = Math.Max(needed, near / nearFraction);
        if (farFraction > 0) needed = Math.Max(needed, far / farFraction);
        if (middleFraction > 0) needed = Math.Max(needed, middle / middleFraction);

        // No band asked for anything (every fraction zero, or the shape has no room): the source's own size is the only
        // honest answer left.
        return needed <= 0 ? sourceLength : Math.Min(needed, MaxBakeLength);
    }

    /// <summary>Cut <paramref name="bounds"/> (already in WORLD/device space) into the nine pieces.
    /// <para>The corners are drawn at <see cref="NineSliceBrush.Border"/>, or - unset - at the size the slice fractions
    /// give against the source's own pixels, which is the 1:1 case. When the shape is too small for its own corners the
    /// border is scaled DOWN proportionally rather than letting opposite corners overlap and draw each other's pixels;
    /// this is what CSS border-image does too, and it is the difference between a skin that degrades and one that
    /// smears.</para></summary>
    /// <param name="texels">The size of the texture actually bound for this fill, for the half-texel inset below; unset
    /// means the source states its own. For a BITMAP the two are the same number; for a VECTOR the source has no texels
    /// at all and the texture is a bake, so they part company - one count used to answer both "how big is a corner"
    /// (source units) and "how wide is a texel" (texture), and only the first of those is the source's to answer.</param>
    public static TextureItem[] Bake(NineSliceBrush brush, Rect bounds, double opacity, int transformSlot, int fadeSlot,
        double scaleX = 1.0, double scaleY = 1.0, Size texels = default)
    {
        var source = brush.Source;
        if (source == null) return null;

        // PIXELS, not ImageSource.Width: that is the DPI-SCALED size, and BitmapSource's raw-pixel constructor stores the
        // dpi (96) in the scale, so Width comes back 96x the picture. Both things below are pixel counts - how big a
        // corner is 1:1, and how many times a strip fits - so a size that is not pixels made every corner and every tile
        // wrong by that factor.
        var (sourceWidth, sourceHeight) = PixelSize(source);
        if (sourceWidth <= 0 || sourceHeight <= 0) return null;

        // TEXELS of the texture that will be sampled - the same numbers for a bitmap, the BAKE's for a vector.
        var texelWidth = texels.Width > 0 ? texels.Width : sourceWidth;
        var texelHeight = texels.Height > 0 ? texels.Height : sourceHeight;

        var slice = Clamp(brush.Slice);

        // Corner size in DEVICE px. Unset border -> the slice fractions against the source's own pixel size, scaled by
        // how much bigger the shape is drawn than the source is... no: 1:1 means exactly the source's pixels, which is
        // what a skin wants. The world scale is already folded into `bounds`, so ask the source directly.
        var border = brush.Border;
        // ...times the world scale, because `bounds` are DEVICE pixels while both a stated Border and the source own
        // pixels are LOGICAL. Without it the frame keeps its pixel size while the shape grows - the corners shrink
        // relative to everything else, and the strips between them change length, which is what a scaled display shows.
        var left = (border.Left > 0 ? border.Left : slice.Left * sourceWidth) * scaleX;
        var top = (border.Top > 0 ? border.Top : slice.Top * sourceHeight) * scaleY;
        var right = (border.Right > 0 ? border.Right : slice.Right * sourceWidth) * scaleX;
        var bottom = (border.Bottom > 0 ? border.Bottom : slice.Bottom * sourceHeight) * scaleY;

        // Too small for its own frame: shrink the border, keeping the ratio, so the corners meet instead of overlapping.
        var hScale = left + right > bounds.Width && left + right > 0 ? bounds.Width / (left + right) : 1.0;
        var vScale = top + bottom > bounds.Height && top + bottom > 0 ? bounds.Height / (top + bottom) : 1.0;
        left *= hScale;
        right *= hScale;
        top *= vScale;
        bottom *= vScale;

        var tint = brush.Tint.ToVector4();
        tint.W *= (float)(opacity * brush.Opacity);

        // Columns and rows, in both spaces at once: x/w on the shape, u/du on the source.
        var xs = new[] { bounds.X, bounds.X + left, bounds.Right - right };
        var ws = new[] { left, Math.Max(0, bounds.Width - left - right), right };
        var us = new[] { 0.0, slice.Left, 1.0 - slice.Right };
        var dus = new[] { slice.Left, Math.Max(0, 1.0 - slice.Left - slice.Right), slice.Right };

        var ys = new[] { bounds.Y, bounds.Y + top, bounds.Bottom - bottom };
        var hs = new[] { top, Math.Max(0, bounds.Height - top - bottom), bottom };
        var vs = new[] { 0.0, slice.Top, 1.0 - slice.Bottom };
        var dvs = new[] { slice.Top, Math.Max(0, 1.0 - slice.Top - slice.Bottom), slice.Bottom };

        var round = brush.EdgeMode == NineSliceEdgeMode.Round;
        var repeat = round || brush.EdgeMode == NineSliceEdgeMode.Repeat;
        var items = new List<TextureItem>(9);

        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                var middle = row == 1 && column == 1;
                if (middle && !brush.DrawCenter) continue;

                var w = ws[column];
                var h = hs[row];
                if (w <= 0 || h <= 0) continue;   // a zero slice contributes nothing - skip rather than draw a null quad

                // Only what is STRETCHED can repeat instead: the middle column tiles horizontally, the middle row
                // vertically. A corner is neither, so it always draws its own piece once.
                // The MIDDLE piece is not an edge: tiled at the edges' pitch it turns into a grid, denser the smaller the
                // slice, so it only tiles when asked for outright.
                var tiles = repeat && (!middle || brush.TileCenter);
                var repeatX = tiles && column == 1 && dus[1] > 0 ? Whole(w / (dus[1] * sourceWidth * scaleX), round) : 1.0;
                var repeatY = tiles && row == 1 && dvs[1] > 0 ? Whole(h / (dvs[1] * sourceHeight * scaleY), round) : 1.0;

                // HALF-TEXEL INSET. The piece samples a strip of the source, and a linear sampler asked for the strip's
                // very edge blends in the texel BEYOND it - the neighbouring piece's pixels. On a tiled edge that lands
                // at every wrap of frac(), which draws a thin line at each tile seam. Pulling the range in to the texel
                // CENTRES removes it, and costs nothing in the shader.
                var uv = Inset(us[column], dus[column], texelWidth);
                var vv = Inset(vs[row], dvs[row], texelHeight);

                items.Add(new TextureItem
                {
                    Bounds = new Vector4F((float)xs[column], (float)ys[row], (float)w, (float)h),
                    // No corner radius: the picture carries its own shape. A slice always REPEATS - a stretched one is
                    // simply one tile across its quad, so the same path serves both without a second branch.
                    Params = new Vector4F(0, transformSlot, 1, 0),
                    Tile = new Vector4F((float)Math.Max(1e-3, repeatX), (float)Math.Max(1e-3, repeatY), 0, 0),
                    Rotation = new Vector4F(1, 0, 0, 1),   // a slice is never turned - its quad IS the layout
                    Drawn = new Vector4F(0, 0, 1, 1),   // each slice fills its own quad exactly - nothing to fit
                    UvRect = new Vector4F((float)uv.Start, (float)vv.Start, (float)uv.Length, (float)vv.Length),
                    Tint = tint
                });
            }
        }

        return items.ToArray();
    }

    // How many times a strip repeats. ROUND nudges it to a whole number so the last tile is not cut mid-motif - the
    // strip is drawn a little larger or smaller instead, which reads far better on a studded or stitched edge. Never
    // below one: a strip shorter than its own tile still draws that tile, clipped by its piece.
    private static double Whole(double repeats, bool round)
        => round ? Math.Max(1, Math.Round(repeats)) : repeats;

    // The source's size in ITS OWN units - what a corner is 1:1 at, and what one repeat of an edge motif spans. A bitmap
    // states it in texels; a drawing has no texels and states its own extent, which is the unit its author cut it in.
    // NOT the texture's texel count: for a vector those are the bake's, and a bake is made to fit, so reading a corner's
    // size out of it would make the corner change size whenever the panel did.
    private static (double Width, double Height) PixelSize(ImageSource source) => source switch
    {
        BitmapSource bitmap => (bitmap.PixelWidth, bitmap.PixelHeight),
        _ => (source.Width, source.Height)
    };

    // Pull a normalised range in by half a texel at each end, so sampling it never reaches past its own pixels. A range
    // thinner than one texel would invert - keep its centre instead, which is the best a sub-texel strip can do.
    private static (double Start, double Length) Inset(double start, double length, double sizeInPixels)
    {
        if (length <= 0 || sizeInPixels <= 0) return (start, length);

        var half = 0.5 / sizeInPixels;
        if (length <= 2 * half) return (start + length * 0.5, 0);

        return (start + half, length - 2 * half);
    }

    // Slice fractions live in 0..1 and opposite pairs cannot claim more than the whole: a source cut 0.7 left and 0.7
    // right has no middle, and the two corners would sample each other's pixels. Scale the pair down instead of clamping
    // one of them, so the cut stays where the author put it in proportion.
    private static Thickness Clamp(Thickness slice)
    {
        var left = Math.Clamp(slice.Left, 0, 1);
        var top = Math.Clamp(slice.Top, 0, 1);
        var right = Math.Clamp(slice.Right, 0, 1);
        var bottom = Math.Clamp(slice.Bottom, 0, 1);

        var h = left + right;
        if (h > 1)
        {
            left /= h;
            right /= h;
        }

        var v = top + bottom;
        if (v > 1)
        {
            top /= v;
            bottom /= v;
        }

        return new Thickness(left, top, right, bottom);
    }
}
