using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering;

/// <summary>Turns a <see cref="TileBrush"/>'s four mechanisms into what the textured passes draw with. They compose in
/// one fixed order and each is meaningless alone, which is why this is one function rather than four:
/// <list type="number">
/// <item>VIEWBOX picks the part of the source a tile shows.</item>
/// <item>VIEWPORT places one tile in the shape, and so states its size.</item>
/// <item>STRETCH + alignment fit that content inside its tile.</item>
/// <item>TILEMODE decides whether the tile repeats, mirrored or not.</item>
/// </list>
/// Pure arithmetic, kept apart from the collector so it can be tested without a device - the same reason
/// <see cref="NineSlice"/> is.</summary>
internal static class ImageTiling
{
    private static readonly Vector4F WholeTile = new(0, 0, 1, 1);

    /// <param name="sourceIsSlice">The texture this layout will sample holds ONLY the viewbox's slice of the source,
    /// baked at that slice's own resolution (a vector source - see <see cref="SliceOf"/>). Everything about the fit is
    /// unchanged; only the final uv is re-expressed, because the sub-rectangle it names has become the whole picture.
    /// A raster source is never sliced - its pixels exist once, at their own resolution, and cropping them costs
    /// nothing to sample.</param>
    public static TileLayout Layout(TileBrush brush, Rect bounds, double scaleX, double scaleY, bool sourceIsSlice = false)
    {
        var content = brush.ContentSize;
        var repeats = brush.TileMode != TileMode.None;

        var viewport = Viewport(brush, bounds, scaleX, scaleY);
        if (viewport.Width <= 0 || viewport.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            // Nothing to lay out - one tile over the whole shape, whole source. The honest "as if nothing was said".
            return new TileLayout(WholeTile, new Vector4F(1, 1, 0, 0), WholeTile, NoRotation, 0f, false);
        }

        var tile = new Vector4F(
            (float)(bounds.Width / viewport.Width),
            (float)(bounds.Height / viewport.Height),
            (float)((viewport.X - bounds.X) / viewport.Width),
            (float)((viewport.Y - bounds.Y) / viewport.Height));

        var rotation = Rotation(brush, bounds, viewport, ref tile);

        var uv = Viewbox(brush, content);
        var drawn = WholeTile;

        // What the tile has to fit is the VIEWBOX's slice of the content, not the whole of it.
        var contentWidth = content.Width * uv.Z;
        var contentHeight = content.Height * uv.W;

        if (contentWidth > 0 && contentHeight > 0)
        {
            switch (brush.Stretch)
            {
                case Stretch.UniformToFill:
                    // The content COVERS its tile, so the crop happens on the source: keep the tile whole and shrink
                    // the sampled rectangle to the tile's aspect.
                    uv = Crop(uv, contentWidth, contentHeight, viewport.Width, viewport.Height, brush);
                    break;

                case Stretch.Uniform:
                    drawn = Fit(contentWidth, contentHeight, viewport.Width, viewport.Height, brush, natural: false);
                    break;

                case Stretch.None:
                    drawn = Fit(contentWidth * scaleX, contentHeight * scaleY, viewport.Width, viewport.Height, brush, natural: true);
                    break;
            }
        }

        // The picture the shader will sample IS the slice, so the uv computed above - a sub-rectangle of the whole
        // drawing - has to be restated as a fraction OF THE SLICE. Usually that is the whole texture; it stays a
        // sub-rectangle when UniformToFill cropped the slice further to the tile's aspect.
        if (sourceIsSlice)
        {
            var slice = Viewbox(brush, content);
            if (slice is { Z: > 0, W: > 0 })
            {
                uv = new Vector4F((uv.X - slice.X) / slice.Z, (uv.Y - slice.Y) / slice.W, uv.Z / slice.Z, uv.W / slice.W);
            }
        }

        return new TileLayout(uv, tile, drawn, rotation, Mirror(brush.TileMode), repeats);
    }

    /// <summary>The viewbox as a 0..1 rectangle of the source - what a tile actually shows. Public because a VECTOR
    /// source is baked to exactly this slice: baking the whole drawing and then sampling a tenth of it is a tenth of
    /// the resolution, which is the blur a small viewbox used to bring with it.</summary>
    public static Vector4F SliceOf(TileBrush brush) => Viewbox(brush, brush.ContentSize);

    private static readonly Vector4F NoRotation = new(1, 0, 0, 1);

    // The whole turn, resolved to ONE 2x2 the shader multiplies a fragment by. Three things are folded in here rather
    // than in the pixel shader, which this driver's compiler is measurably sensitive to the size of:
    //   * the INVERSE (a fragment is mapped back into the grid), which for a rotation is the transpose;
    //   * the shape's ASPECT - turning normalised coordinates of a non-square shape shears it, so the matrix is
    //     conjugated by the size;
    //   * the CENTRE, which becomes a shift of the grid's origin: turning about c and then scaling by the grid is the
    //     same as turning about zero and starting the grid somewhere else.
    private static Vector4F Rotation(TileBrush brush, Rect bounds, Rect viewport, ref Vector4F tile)
    {
        var radians = brush.RotationAngle * Math.PI / 180.0;
        if (Math.Abs(radians) < 1e-9)
        {
            return NoRotation;
        }

        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        // A(-1) * R(-angle) * A, with A = diag(width, height): the turn happens in PIXELS, so it is conjugated by the
        // shape's size on the way in and out of normalised space. Swap the two aspect factors and it shears instead.
        var aspect = bounds.Height / bounds.Width;

        var m00 = cos;
        var m01 = sin * aspect;
        var m10 = -sin / aspect;
        var m11 = cos;

        // The stated centre is a fraction of the TILE; the matrix works in fractions of the SHAPE, so it is placed
        // through the viewport first.
        var stated = brush.RotationCenter;
        var centreX = (viewport.X - bounds.X + stated.X * viewport.Width) / bounds.Width;
        var centreY = (viewport.Y - bounds.Y + stated.Y * viewport.Height) / bounds.Height;

        tile = new Vector4F(
            tile.X,
            tile.Y,
            (float)(tile.Z - (centreX - (m00 * centreX + m01 * centreY)) * tile.X),
            (float)(tile.W - (centreY - (m10 * centreX + m11 * centreY)) * tile.Y));

        return new Vector4F((float)m00, (float)m01, (float)m10, (float)m11);
    }

    /// <summary>The size a VECTOR source is baked at: the rectangle its picture is actually DRAWN in. A drawing maps
    /// its viewbox onto whatever box the bake is given, so baking at the fill box and fitting afterwards squashes it
    /// twice - which is why Stretch did nothing at all on a drawing.</summary>
    public static Size BakeSize(TileBrush brush, Size box)
    {
        // The unit being fitted is the VIEWBOX's slice, not the whole drawing: a tile shows the slice, so the slice is
        // what has to arrive at the tile's resolution. Sizing the whole picture instead meant a viewbox of 0.3 baked
        // its visible part at a third of the pixels the tile draws - measured on the stand as an edge that took 2 px
        // to cross where a full viewbox took 1.
        var slice = SliceOf(brush);
        var whole = brush.ContentSize;
        var content = new Size(whole.Width * slice.Z, whole.Height * slice.W);
        if (content.Width <= 0 || content.Height <= 0 || box.Width <= 0 || box.Height <= 0)
        {
            return box;
        }

        // The unit to bake is one TILE, tiled or not: a single copy is still laid out in the viewport, so measuring it
        // against the whole shape baked it at the wrong size - and, since the size is the cache key, at a size nothing
        // ever asked for again.
        var tile = TileSize(brush, box);

        return brush.Stretch switch
        {
            Stretch.Uniform => Scaled(content, Math.Min(tile.Width / content.Width, tile.Height / content.Height)),
            Stretch.UniformToFill => Scaled(content, Math.Max(tile.Width / content.Width, tile.Height / content.Height)),
            Stretch.None => content,
            _ => tile
        };
    }

    // One tile's rectangle in the SHAPE. Relative units are a fraction of the shape (so the brush survives a resize);
    // absolute units are logical px scaled to the device, so a 32px tile stays 32px whatever it dresses.
    private static Rect Viewport(TileBrush brush, Rect bounds, double scaleX, double scaleY)
    {
        var viewport = brush.Viewport;
        if (brush.ViewportUnits == BrushMappingMode.Absolute)
        {
            return new Rect(
                bounds.X + viewport.X * scaleX,
                bounds.Y + viewport.Y * scaleY,
                viewport.Width * scaleX,
                viewport.Height * scaleY);
        }

        return new Rect(
            bounds.X + viewport.X * bounds.Width,
            bounds.Y + viewport.Y * bounds.Height,
            viewport.Width * bounds.Width,
            viewport.Height * bounds.Height);
    }

    // The part of the source a tile shows, always as a 0..1 sub-rectangle - absolute units are stated in the content's
    // own units (a picture's texels), so they are divided by its size here and the shader only ever sees normalised uv.
    private static Vector4F Viewbox(TileBrush brush, Size content)
    {
        var viewbox = brush.Viewbox;
        if (brush.ViewboxUnits == BrushMappingMode.Absolute)
        {
            if (content.Width <= 0 || content.Height <= 0)
            {
                return WholeTile;
            }

            return new Vector4F(
                (float)(viewbox.X / content.Width),
                (float)(viewbox.Y / content.Height),
                (float)(viewbox.Width / content.Width),
                (float)(viewbox.Height / content.Height));
        }

        return new Vector4F((float)viewbox.X, (float)viewbox.Y, (float)viewbox.Width, (float)viewbox.Height);
    }

    // Uniform / None: the content keeps its aspect, so it occupies only part of its tile and the alignment says which
    // part. None uses the content's own size outright, clamped to the tile - a picture bigger than its tile is cropped
    // by the tile, not scaled.
    private static Vector4F Fit(double contentWidth, double contentHeight, double tileWidth, double tileHeight,
        TileBrush brush, bool natural)
    {
        double width;
        double height;
        if (natural)
        {
            width = Math.Min(contentWidth, tileWidth);
            height = Math.Min(contentHeight, tileHeight);
        }
        else
        {
            var scale = Math.Min(tileWidth / contentWidth, tileHeight / contentHeight);
            width = contentWidth * scale;
            height = contentHeight * scale;
        }

        var fractionX = width / tileWidth;
        var fractionY = height / tileHeight;

        return new Vector4F(
            (float)(OffsetX(brush.AlignmentX) * (1.0 - fractionX)),
            (float)(OffsetY(brush.AlignmentY) * (1.0 - fractionY)),
            (float)fractionX,
            (float)fractionY);
    }

    // UniformToFill: the content covers its tile, so the SAMPLED rectangle shrinks to the tile's aspect and the
    // alignment says which part of the source survives.
    private static Vector4F Crop(Vector4F uv, double contentWidth, double contentHeight, double tileWidth,
        double tileHeight, TileBrush brush)
    {
        var tileAspect = tileWidth / tileHeight;
        var contentAspect = contentWidth / contentHeight;

        if (Math.Abs(tileAspect - contentAspect) < 1e-6)
        {
            return uv;
        }

        if (contentAspect > tileAspect)
        {
            // The content is the wider one: keep its full height, take a slice of its width.
            var fraction = (float)(tileAspect / contentAspect);
            return new Vector4F(uv.X + uv.Z * (float)OffsetX(brush.AlignmentX) * (1f - fraction), uv.Y, uv.Z * fraction, uv.W);
        }

        var vertical = (float)(contentAspect / tileAspect);
        return new Vector4F(uv.X, uv.Y + uv.W * (float)OffsetY(brush.AlignmentY) * (1f - vertical), uv.Z, uv.W * vertical);
    }

    // One tile's size in the same units the caller's box is in - what a bake has to be made at.
    private static Size TileSize(TileBrush brush, Size box)
    {
        var viewport = brush.Viewport;
        if (brush.ViewportUnits == BrushMappingMode.Absolute)
        {
            return new Size(viewport.Width, viewport.Height);
        }

        return new Size(viewport.Width * box.Width, viewport.Height * box.Height);
    }

    private static double OffsetX(AlignmentX alignment) => alignment switch
    {
        AlignmentX.Left => 0.0,
        AlignmentX.Right => 1.0,
        _ => 0.5
    };

    private static double OffsetY(AlignmentY alignment) => alignment switch
    {
        AlignmentY.Top => 0.0,
        AlignmentY.Bottom => 1.0,
        _ => 0.5
    };

    private static Size Scaled(Size size, double scale) => new(size.Width * scale, size.Height * scale);

    private static float Mirror(TileMode mode) => mode switch
    {
        TileMode.FlipX => 1f,
        TileMode.FlipY => 2f,
        TileMode.FlipXY => 3f,
        _ => 0f
    };
}
