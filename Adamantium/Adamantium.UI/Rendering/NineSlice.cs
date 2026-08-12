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

    /// <summary>Cut <paramref name="bounds"/> (already in WORLD/device space) into the nine pieces.
    /// <para>The corners are drawn at <see cref="NineSliceBrush.Border"/>, or - unset - at the size the slice fractions
    /// give against the source's own pixels, which is the 1:1 case. When the shape is too small for its own corners the
    /// border is scaled DOWN proportionally rather than letting opposite corners overlap and draw each other's pixels;
    /// this is what CSS border-image does too, and it is the difference between a skin that degrades and one that
    /// smears.</para></summary>
    public static TexRectItem[] Bake(NineSliceBrush brush, Rect bounds, double opacity, int transformSlot, double scaleX = 1.0, double scaleY = 1.0)
    {
        var source = brush.Source;
        if (source == null) return null;

        // PIXELS, not ImageSource.Width: that is the DPI-SCALED size, and BitmapSource's raw-pixel constructor stores the
        // dpi (96) in the scale, so Width comes back 96x the picture. Both things below are pixel counts - how big a
        // corner is 1:1, and how many times a strip fits - so a size that is not pixels made every corner and every tile
        // wrong by that factor.
        var (sourceWidth, sourceHeight) = PixelSize(source);
        if (sourceWidth <= 0 || sourceHeight <= 0) return null;

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
        var items = new List<TexRectItem>(9);

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
                var uv = Inset(us[column], dus[column], sourceWidth);
                var vv = Inset(vs[row], dvs[row], sourceHeight);

                items.Add(new TexRectItem
                {
                    Bounds = new Vector4F((float)xs[column], (float)ys[row], (float)w, (float)h),
                    Params = new Vector4F(0, transformSlot, 0, 0),   // no corner radius: the picture carries its own shape
                    UvRect = new Vector4F((float)uv.Start, (float)vv.Start, (float)uv.Length, (float)vv.Length),
                    UvRepeat = new Vector4F((float)Math.Max(1e-3, repeatX), (float)Math.Max(1e-3, repeatY), 0, 0),
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

    // The source's size in TEXELS. A bitmap states it outright; anything else can only offer its logical size, which for
    // a source with no pixels of its own (a future drawing/visual brush) is the honest answer anyway.
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
