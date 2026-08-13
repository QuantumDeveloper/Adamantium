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

    public static TileLayout Layout(TileBrush brush, Rect bounds, double scaleX, double scaleY)
    {
        var content = brush.ContentSize;
        var repeats = brush.TileMode != TileMode.None;

        var viewport = Viewport(brush, bounds, scaleX, scaleY);
        if (viewport.Width <= 0 || viewport.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            // Nothing to lay out - one tile over the whole shape, whole source. The honest "as if nothing was said".
            return new TileLayout(WholeTile, new Vector4F(1, 1, 0, 0), WholeTile, 0f, false);
        }

        var tile = new Vector4F(
            (float)(bounds.Width / viewport.Width),
            (float)(bounds.Height / viewport.Height),
            (float)((viewport.X - bounds.X) / viewport.Width),
            (float)((viewport.Y - bounds.Y) / viewport.Height));

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

        return new TileLayout(uv, tile, drawn, Mirror(brush.TileMode), repeats);
    }

    /// <summary>The size a VECTOR source is baked at: the rectangle its picture is actually DRAWN in. A drawing maps
    /// its viewbox onto whatever box the bake is given, so baking at the fill box and fitting afterwards squashes it
    /// twice - which is why Stretch did nothing at all on a drawing.</summary>
    public static Size BakeSize(TileBrush brush, Size box)
    {
        var content = brush.ContentSize;
        if (content.Width <= 0 || content.Height <= 0 || box.Width <= 0 || box.Height <= 0)
        {
            return box;
        }

        // TILED: one copy is a TILE, so that is the unit to bake - each copy then carries its own resolution.
        var tile = brush.TileMode != TileMode.None
            ? TileSize(brush, box)
            : box;

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
