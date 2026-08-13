using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.UI.Rendering;

/// <summary>Turns an <see cref="ImageBrush"/>'s tiling and stretch into the two numbers the textured batch actually
/// draws with: WHICH part of the source a copy samples, and HOW MANY copies fit. Pure arithmetic, kept apart from the
/// collector so it can be tested without a device - the same reason <see cref="NineSlice"/> is.</summary>
internal static class ImageTiling
{
    private static readonly Vector4F Whole = new(0, 0, 1, 1);
    private static readonly Vector4F One = new(1, 1, 0, 0);
    
    /// <summary>The sub-rectangle of the source one copy samples, how many times it repeats across the bounds, and the
    /// rectangle it is drawn in (which only <see cref="Stretch.Uniform"/> and <see cref="Stretch.None"/> shrink).</summary>
    public static (Rect Bounds, Vector4F UvRect, Vector4F UvRepeat) Layout(ImageBrush brush, Rect bounds, double scaleX, double scaleY)
    {
        var (sourceWidth, sourceHeight) = PixelSize(brush.Source);

        if (brush.TileMode != TileMode.None && sourceWidth > 0 && sourceHeight > 0)
        {
            // TILED: one copy is TileSize (or the source's own pixels), and the count is however many fit - fractional,
            // so the last one is cut by the shape's edge exactly as a tiled surface should be.
            var tile = brush.TileSize;
            var tileWidth = (tile.Width > 0 ? tile.Width : sourceWidth) * scaleX;
            var tileHeight = (tile.Height > 0 ? tile.Height : sourceHeight) * scaleY;
            var repeatX = tileWidth > 0 ? bounds.Width / tileWidth : 1.0;
            var repeatY = tileHeight > 0 ? bounds.Height / tileHeight : 1.0;

            return (bounds,
                new Vector4F(0, 0, 1, 1),
                new Vector4F((float)Math.Max(1e-3, repeatX), (float)Math.Max(1e-3, repeatY), Mirror(brush.TileMode), 0));
        }

        // ONE copy, fitted. Fill takes the whole shape; UniformToFill covers it and CROPS the overflow (the uv shrinks);
        // Uniform and None instead shrink what is DRAWN and leave the rest of the shape untouched.
        return brush.Stretch switch
        {
            Stretch.UniformToFill => (bounds, Crop(bounds, sourceWidth * scaleX, sourceHeight * scaleY), One),
            Stretch.Uniform => (Fit(bounds, sourceWidth * scaleX, sourceHeight * scaleY, cover: false), Whole, One),
            Stretch.None => (Fit(bounds, sourceWidth * scaleX, sourceHeight * scaleY, cover: false, natural: true), Whole, One),
            _ => (bounds, Whole, One)
        };
    }

    /// <summary>The size a VECTOR source is baked at: the rectangle its picture is actually DRAWN in. A drawing maps
    /// its viewbox onto whatever box the bake is given, so baking at the fill box and fitting afterwards squashes it
    /// twice - which is why Stretch did nothing at all on a drawing.</summary>
    public static Size BakeSize(ImageBrush brush, Size box)
    {
        var (naturalWidth, naturalHeight) = PixelSize(brush.Source);
        if (naturalWidth <= 0 || naturalHeight <= 0 || box.Width <= 0 || box.Height <= 0)
        {
            return box;
        }

        // TILED: one copy is a TILE, so that is the unit to bake - each copy then carries its own resolution.
        if (brush.TileMode != TileMode.None)
        {
            var tile = brush.TileSize;
            return new Size(tile.Width > 0 ? tile.Width : naturalWidth, tile.Height > 0 ? tile.Height : naturalHeight);
        }

        return brush.Stretch switch
        {
            Stretch.Uniform => Scaled(naturalWidth, naturalHeight, Math.Min(box.Width / naturalWidth, box.Height / naturalHeight)),
            Stretch.UniformToFill => Scaled(naturalWidth, naturalHeight, Math.Max(box.Width / naturalWidth, box.Height / naturalHeight)),
            Stretch.None => new Size(naturalWidth, naturalHeight),
            _ => box
        };
    }

    private static Size Scaled(double width, double height, double scale) => new(width * scale, height * scale);

    // The mirror flag the shader reads out of UvRepeat.z: bit 0 = mirror X, bit 1 = mirror Y. Packed rather than given a
    // field of its own because the record is read per fragment and two spare components were already there.
    private static float Mirror(TileMode mode) => mode switch
    {
        TileMode.FlipX => 1f,
        TileMode.FlipY => 2f,
        TileMode.FlipXY => 3f,
        _ => 0f
    };

    // UniformToFill: the picture covers the shape, so the sampled rectangle is the CENTRED part of the source with the
    // shape's aspect ratio - the overflow is simply never sampled.
    private static Vector4F Crop(Rect bounds, double sourceWidth, double sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return Whole;

        var shapeAspect = bounds.Width / bounds.Height;
        var sourceAspect = sourceWidth / sourceHeight;

        if (Math.Abs(shapeAspect - sourceAspect) < 1e-6) return Whole;

        if (sourceAspect > shapeAspect)
        {
            // The source is the wider one: keep its full height, take a centred slice of its width.
            var w = shapeAspect / sourceAspect;
            return new Vector4F((float)((1 - w) * 0.5), 0, (float)w, 1);
        }

        var h = sourceAspect / shapeAspect;
        return new Vector4F(0, (float)((1 - h) * 0.5), 1, (float)h);
    }

    // Uniform / None: the picture keeps its shape, so the DRAWN rectangle shrinks and centres inside the bounds. None
    // uses the source's own size outright (clamped to the bounds - a picture bigger than its shape is cropped by the
    // shape, not scaled).
    private static Rect Fit(Rect bounds, double sourceWidth, double sourceHeight, bool cover, bool natural = false)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0) return bounds;

        double width;
        double height;
        if (natural)
        {
            width = Math.Min(sourceWidth, bounds.Width);
            height = Math.Min(sourceHeight, bounds.Height);
        }
        else
        {
            var scale = Math.Min(bounds.Width / sourceWidth, bounds.Height / sourceHeight);
            width = sourceWidth * scale;
            height = sourceHeight * scale;
        }

        return new Rect(
            bounds.X + (bounds.Width - width) * 0.5,
            bounds.Y + (bounds.Height - height) * 0.5,
            width,
            height);
    }

    // The source's size in TEXELS - see NineSlice.PixelSize for why ImageSource.Width is the wrong question.
    private static (double Width, double Height) PixelSize(ImageSource source) => source switch
    {
        BitmapSource bitmap => (bitmap.PixelWidth, bitmap.PixelHeight),
        null => (0, 0),
        _ => (source.Width, source.Height)
    };
}
