using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.TextureGeneration;

public class SubpixelRasterizer
{
    private uint textSize;
    private Rectangle glyphBoundingRectangle;
    private List<LineSegment2D> glyphSegments;
    private ushort em;

    // for visualizing
    private byte[,] visSubpixels;
    public byte[,] GetVisSubpixels()
    {
        return visSubpixels;
    }
    // ----------------

    public Color[,] RasterizeGlyphBySubpixels(uint textSize, Rectangle glyphBoundingRectangle, List<LineSegment2D> glyphSegments, ushort em)
    {
        if (glyphSegments.Count == 0)
        {
            return new Color[textSize, textSize];
        }

        this.textSize = textSize;
        this.glyphBoundingRectangle = glyphBoundingRectangle;
        this.glyphSegments = glyphSegments;
        this.em = em;

        // Sample glyph with triple size width
        return SampleSubpixels();
    }

    private Color[,] SampleSubpixels()
    {
        // 1. Calculate boundaries for original glyph (the position of the EM square)
        var emSquare = new Rectangle(0, 0, em, em);

        // 2. Place EM square so that its center matches glyph center
        var glyphCenter = glyphBoundingRectangle.Center;
        var emSquareCenter = emSquare.Center;
        var diff = glyphCenter - emSquareCenter;
        diff.X = Math.Floor(diff.X);
        diff.Y = Math.Floor(diff.Y);

        emSquare.X += (int)diff.X;
        emSquare.Y += (int)diff.Y;

        // 3. Sample glyph by subpixels
        var width = textSize * 3;
        var height = textSize;

        var distances = new double[width, height];

        var minDist = double.MaxValue;
        var maxDist = double.MinValue;

        for (var y = 0; y < height; ++y)
        {
            for (var x = 0; x < width; ++x)
            {
                // determine the closest segment to current sampling point
                var samplingPoint = new Vector2(emSquare.Width / width * (x + 0.5) + emSquare.X, emSquare.Height - emSquare.Height / height * (y + 0.5) + emSquare.Y);

                var distance = GetSignedDistance(samplingPoint);

                distances[x, y] = distance;

                if (distance < minDist) minDist = distance;
                if (distance > maxDist) maxDist = distance;
            }
        }

        // 4. Normalize
        maxDist = Math.Min(Math.Abs(minDist), Math.Abs(maxDist));
        minDist = -maxDist;

        var colors = new Color[textSize, textSize];
        Color color = default;

        for (var y = 0; y < height; ++y)
        {
            var resX = 0;

            for (var x = 0; x < width; ++x)
            {
                if (distances[x, y] < minDist) distances[x, y] = minDist;
                if (distances[x, y] > maxDist) distances[x, y] = maxDist;

                if (x % 3 == 0) color.R = (byte)(255 * (distances[x, y] - minDist) / (maxDist - minDist));
                if (x % 3 == 1) color.G = (byte)(255 * (distances[x, y] - minDist) / (maxDist - minDist));
                if (x % 3 == 2)
                {
                    color.B = (byte)(255 * (distances[x, y] - minDist) / (maxDist - minDist));
                    color.A = color.R == 0 && color.G == 0 && color.B == 0 ? (byte)0 : (byte)255;

                    colors[resX++, y] = color;
                }
            }
        }

        return colors;
    }

    private double GetSignedDistance(Vector2 point)
    {
        double closestDistance = double.MaxValue;

        // there can be up to two closest segments in case if point is close to segments' connection
        // we will store both and then determine the signed pseudo-distance
        // if these two signed pseudo-distances have different signs, use the one with negative, because the point is outside
        var closestSegments = new List<LineSegment2D>();

        foreach (var segment in glyphSegments)
        {
            var distance = GlyphSegmentsMath.GetDistanceToSegment(segment, point);

            if (distance <= closestDistance)
            {
                if (distance < closestDistance)
                {
                    closestSegments.Clear();
                    closestDistance = distance;
                }

                closestSegments.Add(segment);
            }
        }

        closestDistance = GlyphSegmentsMath.GetSignedDistanceToSegmentsJoint(closestSegments, point, false);

        return closestDistance;
    }
}