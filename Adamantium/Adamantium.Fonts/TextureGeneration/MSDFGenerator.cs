using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.TextureGeneration
{
    public static class MSDFGenerator
    {
        public static void GenerateGlyphData(this Glyph glyph, GlyphTextureData textureData, double pxRange, ushort unitsPerEm)
        {
            GenerateMSDFForExistingTextureData(glyph, textureData, pxRange, unitsPerEm);
        }
        
        public static GlyphTextureData PrepareData(this Glyph glyph, uint size, ushort unitsPerEm, uint margin, bool useProportionalSize = true)
        {
            return CalculateBasicTextureData(glyph, size, unitsPerEm, margin, useProportionalSize);
        }
        
        // --- PREPROCESSORS ---
        private static List<List<MsdfGlyphSegment>> SplitToRawContours(List<MsdfGlyphSegment> segments)
        {
            var res = new List<List<MsdfGlyphSegment>>();
            var contour = new List<MsdfGlyphSegment>();

            for (var i = 0; i < segments.Count - 1; ++i)
            {
                var currentSegment = segments[i];
                var nextSegment = segments[i + 1];

                contour.Add(currentSegment);

                if (!GlyphSegmentsMath.IsSegmentsConnected(ref currentSegment.Segment, ref nextSegment.Segment))
                {
                    res.Add(contour);
                    contour = new List<MsdfGlyphSegment>();
                }

                if (i == segments.Count - 2)
                {
                    contour.Add(nextSegment);
                    res.Add(contour);
                }
            }

            return res;
        }

        private static bool FindFirstSharpAngle(List<MsdfGlyphSegment> contour, int angleThreshold, out int startIndex)
        {
            for (int i = startIndex = 0; i < contour.Count; i++)
            {
                var currentSeg = contour[i];

                if (i == contour.Count - 1)
                {
                    startIndex = 0;
                }
                else
                {
                    startIndex = i + 1;
                }

                var nextSeg = contour[startIndex];

                if (GlyphSegmentsMath.GetSegmentsAngle(currentSeg.Segment, nextSeg.Segment) < angleThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<Contour> SplitToEdgedContours(List<MsdfGlyphSegment> segments)
        {
            var angleThreshold = 135;

            var res = new List<Contour>();
            var rawContours = SplitToRawContours(segments);

            foreach (var contour in rawContours)
            {
                var edgedContour = new Contour();
                var startIndex = 0;

                if (!FindFirstSharpAngle(contour, angleThreshold, out startIndex))
                {
                    var currentEdge = new Edge();
                    currentEdge.Segments = contour;
                    edgedContour.Edges.Add(currentEdge);
                }
                else
                {
                    var currentEdge = new Edge();
                    var index = startIndex;
                    var cnt = 0;
                    while (cnt < contour.Count)
                    {
                        var currentSeg = contour[index];
                        currentEdge.Segments.Add(currentSeg);
                        index++;

                        if (index == contour.Count)
                        {
                            index = 0;
                        }

                        var nextSeg = contour[index];

                        if (GlyphSegmentsMath.GetSegmentsAngle(currentSeg.Segment, nextSeg.Segment) < angleThreshold)
                        {
                            edgedContour.Edges.Add(currentEdge);
                            currentEdge = new Edge();
                        }

                        cnt++;
                    }
                }

                res.Add(edgedContour);
            }

            return res;
        }

        // --- MAIN FUNCS ---
        // edge is a list of connected segments which have no sharp corners within them
        private static void ColorEdges(List<MsdfGlyphSegment> segments)
        {
            var segmentLengthThreshold = 10;
            var contours = SplitToEdgedContours(segments);

            segments.Clear();

            foreach (var contour in contours)
            {
                var currentColor = Color.FromRgba(255, contour.Edges.Count == 1 ? (byte)255 : (byte)0, 255, 255);

                foreach (var edge in contour.Edges)
                {
                    for (int i = 0; i < edge.Segments.Count; i++)
                    {
                        var currentSeg = edge.Segments[i];
                        currentSeg.MsdfColor = currentColor;
                        //currentSeg.MsdfColor = currentSeg.Direction.Length() < segmentLengthThreshold ? Colors.White : currentColor;

                        segments.Add(currentSeg);
                    }

                    currentColor = currentColor == Color.FromRgba(255, 255, 0, 255)
                        ? Color.FromRgba(0, 255, 255, 255)
                        : Color.FromRgba(255, 255, 0, 255);
                }
            }
        }

        private static ColoredDistance GetColoredDistances(List<MsdfGlyphSegment> segments, Vector2 point, double range, bool isTtf)
        {
            double closestRedDistance = double.MaxValue;
            double closestGreenDistance = double.MaxValue;
            double closestBlueDistance = double.MaxValue;
            double closestAlphaDistance = double.MaxValue;

            // there can be up to two closest segments in case if point is close to segments' connection
            // we will store both and then determine the signed pseudo-distance
            // if these two signed pseudo-distances have different signs, use the one with negative, because the point is outside
            var closestRedSegments = new List<LineSegment2D>();
            var closestGreenSegments = new List<LineSegment2D>();
            var closestBlueSegments = new List<LineSegment2D>();
            var closestAlphaSegments = new List<LineSegment2D>();

            foreach (var segment in segments)
            {
                var distance = GlyphSegmentsMath.GetDistanceToSegment(segment.Segment, point);

                if (MsdfGeneratorHelper.ApplyColorMask(segment.MsdfColor, true, false, false) != Colors.Black
                    && distance <= closestRedDistance)
                {
                    if (distance < closestRedDistance)
                    {
                        closestRedSegments.Clear();
                        closestRedDistance = distance;
                    }

                    closestRedSegments.Add(segment.Segment);
                }

                if (MsdfGeneratorHelper.ApplyColorMask(segment.MsdfColor, false, true, false) != Colors.Black
                    && distance <= closestGreenDistance)
                {
                    if (distance < closestGreenDistance)
                    {
                        closestGreenSegments.Clear();
                        closestGreenDistance = distance;
                    }

                    closestGreenSegments.Add(segment.Segment);
                }

                if (MsdfGeneratorHelper.ApplyColorMask(segment.MsdfColor, false, false, true) != Colors.Black
                    && distance <= closestBlueDistance)
                {
                    if (distance < closestBlueDistance)
                    {
                        closestBlueSegments.Clear();
                        closestBlueDistance = distance;
                    }

                    closestBlueSegments.Add(segment.Segment);
                }

                if (distance <= closestAlphaDistance)
                {
                    if (distance < closestAlphaDistance)
                    {
                        closestAlphaSegments.Clear();
                        closestAlphaDistance = distance;
                    }

                    closestAlphaSegments.Add(segment.Segment);
                }
            }

            var coloredDistance = new ColoredDistance();

            coloredDistance.RedDistance =
                GlyphSegmentsMath.GetSignedDistanceToSegmentsJoint(closestRedSegments, point, true);
            coloredDistance.GreenDistance =
                GlyphSegmentsMath.GetSignedDistanceToSegmentsJoint(closestGreenSegments, point, true);
            coloredDistance.BlueDistance =
                GlyphSegmentsMath.GetSignedDistanceToSegmentsJoint(closestBlueSegments, point, true);
            coloredDistance.AlphaDistance =
                GlyphSegmentsMath.GetSignedDistanceToSegmentsJoint(closestAlphaSegments, point, false);

            if (isTtf)
            {
                coloredDistance.RedDistance = -coloredDistance.RedDistance;
                coloredDistance.GreenDistance = -coloredDistance.GreenDistance;
                coloredDistance.BlueDistance = -coloredDistance.BlueDistance;
                coloredDistance.AlphaDistance = -coloredDistance.AlphaDistance;
            }

            // prepare distance data for normalization
            coloredDistance.RedDistance = coloredDistance.RedDistance / range + 0.5;
            coloredDistance.GreenDistance = coloredDistance.GreenDistance / range + 0.5;
            coloredDistance.BlueDistance = coloredDistance.BlueDistance / range + 0.5;
            coloredDistance.AlphaDistance = coloredDistance.AlphaDistance / range + 0.5;

            return coloredDistance;
        }

        public static GlyphTextureData CalculateBasicTextureData(
            Glyph glyph,
            uint originalSize,
            ushort unitsPerEm,
            uint margin,
            bool useProportionalSize = true)
        {
            if (!useProportionalSize)
            {
                return new GlyphTextureData(originalSize, originalSize, glyph.Index, margin,
                    glyph.RelatedCharacters.FirstOrDefault());
            }
            var glyphBoundingRectangle = glyph.BoundingRectangle;
            var widthRatio = (double)(glyphBoundingRectangle.Width) / unitsPerEm;
            var heightRatio = (double)(glyphBoundingRectangle.Height) / unitsPerEm;
            var size = new Size(Math.Ceiling(originalSize * widthRatio),
                Math.Ceiling(originalSize * heightRatio));
            
            var textureData = new GlyphTextureData((uint)size.Width, (uint)size.Height, glyph.Index, margin, glyph.RelatedCharacters.FirstOrDefault());

            return textureData;
        }

        /// <summary>
        /// Generates MSDF texture
        /// </summary>
        /// <param name="glyph">glyph to process</param>
        /// <param name="originalSize">Width and height of MSDF texture</param>
        /// <param name="pxRange">Pixel range for generation</param>
        /// <param name="unitsPerEm">Size of glyph width and height in em</param>
        /// <param name="margin"></param>
        /// <returns>MSDF color data in for of single-dimension array</returns>
        public static GlyphTextureData GenerateDirectMSDF(
            this Glyph glyph,
            uint originalSize,
            double pxRange,
            ushort unitsPerEm,
            uint margin)
        {
            var glyphSegments = glyph.GetMergedOutlineSegments();
            if (glyphSegments.Count == 0)
            {
                return null;
            }

            var segments = new List<MsdfGlyphSegment>();

            foreach (var segment in glyphSegments)
            {
                segments.Add(new MsdfGlyphSegment(segment.Start, segment.End));
            }

            var glyphBoundingRectangle = glyph.BoundingRectangle;

            // Isotropic scale relative to the EM square (the typographic design reference): the SAME factor
            // for X and Y, so the distance field is NOT stretched anisotropically (the old square-fit used
            // scaleX != scaleY) and relative glyph sizes are preserved (M larger than i). We scale against
            // the em, NOT the font's global max-glyph bbox: that bbox is the union over all glyphs and is
            // routinely inflated by a few outliers (ornaments, .notdef, composites), which would shrink
            // every normal letter to a speck. The rare glyph taller/wider than the em is scaled down
            // uniformly so it still fits the fixed cell while keeping scaleX == scaleY.
            var unitScale = (double)originalSize / unitsPerEm;
            var glyphWidth = glyphBoundingRectangle.Width * unitScale;
            var glyphHeight = glyphBoundingRectangle.Height * unitScale;
            var overflow = Math.Max(glyphWidth, glyphHeight) / originalSize;
            if (overflow > 1.0)
            {
                glyphWidth /= overflow;
                glyphHeight /= overflow;
            }
            var size = new Size(
                Math.Max(1, Math.Ceiling(glyphWidth)),
                Math.Max(1, Math.Ceiling(glyphHeight)));

            // 1. Color all segments
            ColorEdges(segments);

            // 2. Calculate boundaries for original glyph (the position of the EM square)
            var emSquare = new Rectangle(0, 0, unitsPerEm, unitsPerEm);

            // 3. Place EM square so that its center matches glyph center
            var glyphCenter = glyphBoundingRectangle.Center;
            var emSquareCenter = emSquare.Center;
            var diff = glyphCenter - emSquareCenter;
            diff.X = Math.Floor(diff.X);
            diff.Y = Math.Floor(diff.Y);

            emSquare.X += (int)diff.X;
            emSquare.Y += (int)diff.Y;

            // 4. Generate colored pseudo-distance field
            var coloredDistances = new ColoredDistance[(int)size.Width, (int)size.Height];

            // var scaleX = size.Width / emSquare.Width;
            // var scaleY = size.Height / emSquare.Height;

            var scaleX = size.Width / glyphBoundingRectangle.Width;
            var scaleY = size.Height / glyphBoundingRectangle.Height;

            var range = MsdfGeneratorHelper.GetRange(pxRange, scaleX, scaleY);

            //var additionalSpace = glyphBoundingRectangle.Width * 0.02;
            var additionalSpace = 0;
            
            var textureData = new GlyphTextureData((uint)size.Width, (uint)size.Height, glyph.Index, margin, glyph.RelatedCharacters.FirstOrDefault());

            ColoredDistance minColoredDistance;

            var value = -emSquare.Width / 2 / range + 0.5;
            minColoredDistance.RedDistance = value;
            minColoredDistance.GreenDistance = value;
            minColoredDistance.BlueDistance = value;
            minColoredDistance.AlphaDistance = value;

            for (var y = 0; y < size.Height; ++y)
            {
                for (var x = 0; x < size.Width; ++x)
                {
                    // determine the closest segment to current sampling point
                    //var samplingPoint = new Vector2D(originalDimensions.X / size * (x + 0.5), originalDimensions.Y - (originalDimensions.Y / size * (y + 0.5)));
                    //var samplingPoint = new Vector2(emSquare.Width / size.Width * (x + 0.5) + emSquare.X, emSquare.Height - emSquare.Height / size.Height * (y + 0.5) + emSquare.Y);

                    var samplingPoint =
                        new Vector2(glyphBoundingRectangle.Width / size.Width * (x + 0.5) + glyphBoundingRectangle.X,
                            glyphBoundingRectangle.Height - (glyphBoundingRectangle.Height / size.Height * (y + 0.5) -
                                                             glyphBoundingRectangle.Y));

                    if (samplingPoint.X >= glyphBoundingRectangle.X - additionalSpace &&
                        samplingPoint.X <= glyphBoundingRectangle.Right + additionalSpace &&
                        samplingPoint.Y >= glyphBoundingRectangle.Y - additionalSpace &&
                        samplingPoint.Y <= glyphBoundingRectangle.Bottom + additionalSpace)
                    {
                        coloredDistances[x, y] = GetColoredDistances(segments, samplingPoint, range, glyph.OutlineType == OutlineType.TrueType);
                    }
                    else
                    {
                        coloredDistances[x, y] = minColoredDistance;
                    }
                }
            }

            // 5. Fix artifacts
            // FixArtifacts(coloredDistances, (int)size.Width, (int)size.Height); // disabled: the simple
            // clash detector can collapse legitimate corners -> distorted/dark glyph chunks.

            // 6. Normalize MSDF and SDF to [0 .. 255] range.
            // Pixels is allocated for FullGlyphSize = (size + margin*2): write the field inset by `margin`
            // on every side so the glyph sits centred with a margin border. Writing contiguously from 0
            // (ignoring the row stride) garbles the glyph whenever margin > 0 and breaks the atlas layout.
            var marginPx = (int)margin;
            var rowStride = ((int)size.Width + marginPx * 2) * 4;
            for (var y = 0; y < size.Height; y++)
            {
                var index = rowStride * (y + marginPx) + marginPx * 4;
                for (var x = 0; x < size.Width; x++)
                {
                    var distance = coloredDistances[x, y];
                    var red = MsdfGeneratorHelper.PixelFloatToByte(distance.RedDistance);
                    var green = MsdfGeneratorHelper.PixelFloatToByte(distance.GreenDistance);
                    var blue = MsdfGeneratorHelper.PixelFloatToByte(distance.BlueDistance);
                    var alpha = MsdfGeneratorHelper.PixelFloatToByte(distance.AlphaDistance);

                    textureData.Pixels[index + 0] = red;
                    textureData.Pixels[index + 1] = green;
                    textureData.Pixels[index + 2] = blue;
                    textureData.Pixels[index + 3] = alpha;

                    index += 4;
                }
            }

            return textureData;
        }
        
        public static void GenerateMSDFForExistingTextureData(
            Glyph glyph,
            GlyphTextureData textureData,
            double pxRange,
            ushort unitsPerEm)
        {
            var glyphSegments = glyph.GetMergedOutlineSegments();
            if (glyphSegments.Count == 0)
            {
                return;
            }

            var segments = new List<MsdfGlyphSegment>();

            foreach (var segment in glyphSegments)
            {
                segments.Add(new MsdfGlyphSegment(segment.Start, segment.End));
            }

            // 1. Color all segments
            ColorEdges(segments);

            // 2. Calculate boundaries for original glyph (the position of the EM square)
            var emSquare = new Rectangle(0, 0, unitsPerEm, unitsPerEm);

            var glyphBoundingRectangle = glyph.BoundingRectangle;

            // 3. Place EM square so that its center matches glyph center
            var glyphCenter = glyphBoundingRectangle.Center;
            var emSquareCenter = emSquare.Center;
            var diff = glyphCenter - emSquareCenter;
            diff.X = Math.Floor(diff.X);
            diff.Y = Math.Floor(diff.Y);

            emSquare.X += (int)diff.X;
            emSquare.Y += (int)diff.Y;

            var size = textureData.BoundingRect.Size;

            // 4. Generate colored pseudo-distance field
            var coloredDistances = new ColoredDistance[(int)size.Width, (int)size.Height];

            var scaleX = size.Width / glyphBoundingRectangle.Width;
            var scaleY = size.Height / glyphBoundingRectangle.Height;

            var range = MsdfGeneratorHelper.GetRange(pxRange, scaleX, scaleY);

            var additionalSpace = glyphBoundingRectangle.Width * 0.5;

            ColoredDistance minColoredDistance;

            var value = -(emSquare.Width / 2) / range + 0.5;
            minColoredDistance.RedDistance = value;
            minColoredDistance.GreenDistance = value;
            minColoredDistance.BlueDistance = value;
            minColoredDistance.AlphaDistance = value;

            var paddingX = glyphBoundingRectangle.Width * 0.1f;
            var paddingY = glyphBoundingRectangle.Height * 0.1f;
            glyphBoundingRectangle.Width += (int)(2 * paddingX);
            glyphBoundingRectangle.Height += (int)(2 * paddingY);
            //var paddingX = 0;
            //var paddingY = 0;

            for (var y = 0; y < size.Height; ++y)
            {
                for (var x = 0; x < size.Width; ++x)
                {
                    // determine the closest segment to current sampling point
                    //var samplingPoint =
                    //    new Vector2(glyphBoundingRectangle.Width / size.Width * (x + 0.5) + glyphBoundingRectangle.X,
                    //        glyphBoundingRectangle.Height - (glyphBoundingRectangle.Height / size.Height * (y + 0.5) -
                    //                                         glyphBoundingRectangle.Y));
                    var samplingPoint =
                            new Vector2(glyphBoundingRectangle.Width / size.Width * (x + 0.5) + glyphBoundingRectangle.X - paddingX,
                                glyphBoundingRectangle.Height - (glyphBoundingRectangle.Height / size.Height * (y + 0.5) -
                                                                 glyphBoundingRectangle.Y + paddingY));

                    if (samplingPoint.X >= glyphBoundingRectangle.X - additionalSpace &&
                        samplingPoint.X <= glyphBoundingRectangle.Right + additionalSpace &&
                        samplingPoint.Y >= glyphBoundingRectangle.Y - additionalSpace &&
                        samplingPoint.Y <= glyphBoundingRectangle.Bottom + additionalSpace)
                    {
                        coloredDistances[x, y] = GetColoredDistances(segments, samplingPoint, range, glyph.OutlineType == OutlineType.TrueType);
                    }
                    else
                    {
                        coloredDistances[x, y] = minColoredDistance;
                    }
                }
            }

            // 5. Fix artifacts
            // FixArtifacts(coloredDistances, (int)size.Width, (int)size.Height); // disabled: the simple
            // clash detector can collapse legitimate corners -> distorted/dark glyph chunks.

            // 6. Normalize MSDF and SDF to [0 .. 255] range
            var margin = (int)textureData.Margin;
            var rowStride = (size.Width + (margin * 2)) * 4;
            for (var y = 0; y < size.Height; y++)
            {
                var index = (int)(rowStride * (y + margin)) + (margin * 4);
                for (var x = 0; x < size.Width; x++)
                {
                    var distance = coloredDistances[x, y];
                    var red = MsdfGeneratorHelper.PixelFloatToByte(distance.RedDistance);
                    var green = MsdfGeneratorHelper.PixelFloatToByte(distance.GreenDistance);
                    var blue = MsdfGeneratorHelper.PixelFloatToByte(distance.BlueDistance);
                    var alpha = MsdfGeneratorHelper.PixelFloatToByte(distance.AlphaDistance);

                    textureData.Pixels[index + 0] = red;
                    textureData.Pixels[index + 1] = green;
                    textureData.Pixels[index + 2] = blue;
                    textureData.Pixels[index + 3] = alpha;

                    index += 4;
                }
            }
        }
        
        // --- ARTIFACT FIXING ---
        // true - no collision, false - collision
        private static bool CheckNeighbor(ColoredDistance neighbor, ColoredDistance current, double threshold)
        {
            var cnt = 0;

            // Distances are normalized (/range + 0.5), so the contour is at 0.5: inside >= 0.5, outside < 0.5.
            bool isNeighborRedPositive = neighbor.RedDistance >= 0.5;
            bool isNeighborGreenPositive = neighbor.GreenDistance >= 0.5;
            bool isNeighborBluePositive = neighbor.BlueDistance >= 0.5;

            bool isCurrentRedPositive = current.RedDistance >= 0.5;
            bool isCurrentGreenPositive = current.GreenDistance >= 0.5;
            bool isCurrentBluePositive = current.BlueDistance >= 0.5;

            if (isNeighborRedPositive ^ isCurrentRedPositive &&
                Math.Abs(neighbor.RedDistance - current.RedDistance) > threshold)
            {
                ++cnt;
            }

            if (isNeighborGreenPositive ^ isCurrentGreenPositive &&
                Math.Abs(neighbor.GreenDistance - current.GreenDistance) > threshold)
            {
                ++cnt;
            }

            if (isNeighborBluePositive ^ isCurrentBluePositive &&
                Math.Abs(neighbor.BlueDistance - current.BlueDistance) > threshold)
            {
                ++cnt;
            }

            return cnt < 2;
        }

        // true - no collision, false - collision
        private static bool CheckForCollision(List<ColoredDistance> neighbors, ColoredDistance current, double threshold)
        {
            foreach (var neighbor in neighbors)
            {
                if (!CheckNeighbor(neighbor, current, threshold))
                {
                    return false;
                }
            }

            return true;
        }

        private static void FixArtifacts(ColoredDistance[,] data, int width, int height)
        {
            // Clash threshold in normalized units (field is /range + 0.5, so ~0.33 change per texel at
            // PixelRange=3). Corners legitimately flip 2 channels by ~0.33; only flag clearly larger jumps
            // as real clashes, otherwise normal edges/corners get destroyed. Retune if PixelRange changes.
            var threshold = 0.5;
            var correctionList = new List<CorrectionLocation>();

            for (var y = 1; y < height - 1; y++)
            {
                for (var x = 1; x < width - 1; x++)
                {
                    var current = data[x, y];
                    var neighbors = new List<ColoredDistance>
                    {
                        // get 8 neighbors of the current pixel
                        data[x - 1, y - 1],
                        data[x, y - 1],
                        data[x + 1, y - 1],
                        data[x - 1, y],
                        data[x + 1, y],
                        data[x - 1, y + 1],
                        data[x, y + 1],
                        data[x + 1, y + 1]
                    };

                    if (CheckForCollision(neighbors, current, threshold)) continue;

                    var correction = new CorrectionLocation
                    {
                        X = x,
                        Y = y
                    };

                    correctionList.Add(correction);
                }
            }

            foreach (var correction in correctionList)
            {
                var pixel = data[correction.X, correction.Y];
                var median = MsdfGeneratorHelper.Median(pixel.RedDistance, pixel.GreenDistance, pixel.BlueDistance);

                pixel.RedDistance = pixel.GreenDistance = pixel.BlueDistance = median;

                data[correction.X, correction.Y] = pixel;
            }
        }
    }
}