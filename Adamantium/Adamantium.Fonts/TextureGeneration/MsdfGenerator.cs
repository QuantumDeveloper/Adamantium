using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.TextureGeneration
{
    public class MsdfGenerator
    {
        private List<MsdfGlyphSegment> segments;

        // --- PREPROCESSORS ---
        private List<List<MsdfGlyphSegment>> SplitToRawContours()
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

        private bool FindFirstSharpAngle(List<MsdfGlyphSegment> contour, int angleThreshold, out int startIndex)
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

        private List<Contour> SplitToEdgedContours()
        {
            var angleThreshold = 135;

            var res = new List<Contour>();
            var rawContours = SplitToRawContours();

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
        private void ColorEdges()
        {
            var segmentLengthThreshold = 10;
            var contours = SplitToEdgedContours();

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

        private ColoredDistance GetColoredDistances(Vector2 point, double range)
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

            // prepare distance data for normalization
            coloredDistance.RedDistance = coloredDistance.RedDistance / range + 0.5;
            coloredDistance.GreenDistance = coloredDistance.GreenDistance / range + 0.5;
            coloredDistance.BlueDistance = coloredDistance.BlueDistance / range + 0.5;
            coloredDistance.AlphaDistance = coloredDistance.AlphaDistance / range + 0.5;

            return coloredDistance;
        }

        /// <summary>
        /// Generates MSDF texture
        /// </summary>
        /// <param name="originalSize">Width and height of MSDF texture</param>
        /// <param name="pxRange">Pixel range for generation</param>
        /// <param name="glyphBoundingRectangle">Bounding rectangle of original glyph</param>
        /// <param name="glyphSegments">array of glyph segments for generation</param>
        /// <param name="unitsPerEm">Size of glyph width and height in em</param>
        /// <param name="glyphIndex">Glyph index</param>
        /// <returns>MSDF color data in for of single-dimension array</returns>
        public GlyphTextureData GenerateDirectMSDF(
            uint originalSize, 
            double pxRange, 
            Rectangle glyphBoundingRectangle,
            List<LineSegment2D> glyphSegments, 
            ushort unitsPerEm, 
            uint glyphIndex)
        {
            if (glyphSegments.Count == 0)
            {
                return null;
            }

            segments = new List<MsdfGlyphSegment>();

            foreach (var segment in glyphSegments)
            {
                segments.Add(new MsdfGlyphSegment(segment.Start, segment.End));
            }

            var widthRatio = (double)(glyphBoundingRectangle.Width) / unitsPerEm;
            var heightRatio = (double)(glyphBoundingRectangle.Height) / unitsPerEm;
            var size = new Size(Math.Ceiling(originalSize * widthRatio),
                Math.Ceiling(originalSize * heightRatio));

            // 1. Color all segments
            ColorEdges();

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
                        coloredDistances[x, y] = GetColoredDistances(samplingPoint, range);
                    }
                    else
                    {
                        coloredDistances[x, y] = minColoredDistance;
                    }
                }
            }

            // 5. Fix artefacts
            //FixArtefacts(coloredDistances, size);

            var textureData = new GlyphTextureData((uint)size.Width, (uint)size.Height, glyphIndex);

            // 6. Normalize MSDF and SDF to [0 .. 255] range
            int index = 0;
            for (var y = 0; y < size.Height; y++)
            {
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

        // --- ARTEFACT FIXING ---
        // true - no collision, false - collision
        private bool CheckNeighbor(ColoredDistance neighbor, ColoredDistance current)
        {
            const double threshold = 2.5;

            var cnt = 0;

            bool isNeighborRedPositive = neighbor.RedDistance >= 0;
            bool isNeighborGreenPositive = neighbor.GreenDistance >= 0;
            bool isNeighborBluePositive = neighbor.BlueDistance >= 0;

            bool isCurrentRedPositive = current.RedDistance >= 0;
            bool isCurrentGreenPositive = current.GreenDistance >= 0;
            bool isCurrentBluePositive = current.BlueDistance >= 0;

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
        private bool CheckForCollision(List<ColoredDistance> neighbors, ColoredDistance current)
        {
            foreach (var neighbor in neighbors)
            {
                if (!CheckNeighbor(neighbor, current))
                {
                    return false;
                }
            }

            return true;
        }

        private void FixArtefacts(ColoredDistance[,] data, uint textureSize)
        {
            var correctionList = new List<CorrectionLocation>();

            for (var y = 1; y < textureSize - 1; y++)
            {
                for (var x = 1; x < textureSize - 1; x++)
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

                    if (CheckForCollision(neighbors, current)) continue;

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