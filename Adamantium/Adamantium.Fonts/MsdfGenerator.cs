using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Engine.Core.Models;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Fonts
{
    public class MsdfGenerator
    {
        private List<MsdfGlyphSegment> segments;

        // --- PREPROCESSORS ---
        private List<List<MsdfGlyphSegment>> SplitToRawContours()
        {
            var res = new List<List<MsdfGlyphSegment>>();
            var contour = new List<MsdfGlyphSegment>();

            for (var i = 0; i < (segments.Count - 1); ++i)
            {
                var currentSegment = segments[i];
                var nextSegment = segments[i + 1];
                
                contour.Add(currentSegment);

                if (!GlyphSegmentsMath.IsSegmentsConnected(ref currentSegment.Segment, ref nextSegment.Segment))
                {
                    res.Add(contour);
                    contour = new List<MsdfGlyphSegment>();
                }

                if (i == (segments.Count - 2))
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

                if (i == (contour.Count - 1))
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
                var currentColor = Color.FromRgba(255, contour.Edges.Count == 1 ? (byte) 255 : (byte) 0, 255, 255);

                foreach (var edge in contour.Edges)
                {
                    for (int i = 0; i < edge.Segments.Count; i++)
                    {
                        var currentSeg = edge.Segments[i];
                        currentSeg.MsdfColor = currentColor;
                        //currentSeg.MsdfColor = currentSeg.Direction.Length() < segmentLengthThreshold ? Colors.White : currentColor;

                        segments.Add(currentSeg);
                    }

                    currentColor = currentColor == Color.FromRgba(255, 255, 0, 255) ? Color.FromRgba(0, 255, 255, 255) : Color.FromRgba(255, 255, 0, 255);
                }
            }
        }
        
        private ColoredDistance GetColoredDistances(Vector2D point, double range)
        {
            double closestRedDistance = Double.MaxValue;
            double closestGreenDistance = Double.MaxValue;
            double closestBlueDistance = Double.MaxValue;
            double closestAlphaDistance = Double.MaxValue;

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

                if (ApplyColorMask(segment.MsdfColor, true, false, false) != Colors.Black
                    && distance <= closestRedDistance)
                {
                    if (distance < closestRedDistance)
                    {
                        closestRedSegments.Clear();
                        closestRedDistance = distance;
                    }
                    
                    closestRedSegments.Add(segment.Segment);
                }
                
                if (ApplyColorMask(segment.MsdfColor, false, true, false) != Colors.Black
                    && distance <= closestGreenDistance)
                {
                    if (distance < closestGreenDistance)
                    {
                        closestGreenSegments.Clear();
                        closestGreenDistance = distance;
                    }
                    
                    closestGreenSegments.Add(segment.Segment);
                }
                
                if (ApplyColorMask(segment.MsdfColor, false, false, true) != Colors.Black
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
            
            coloredDistance.redDistance = GlyphSegmentsMath.GetSignedDistanceToSegmentsJoint(closestRedSegments, point, true);
            coloredDistance.greenDistance = GlyphSegmentsMath.GetSignedDistanceToSegmentsJoint(closestGreenSegments, point, true);
            coloredDistance.blueDistance = GlyphSegmentsMath.GetSignedDistanceToSegmentsJoint(closestBlueSegments, point, true);
            coloredDistance.alphaDistance = GlyphSegmentsMath.GetSignedDistanceToSegmentsJoint(closestAlphaSegments, point, false);

            // prepare distance data for normalization
            coloredDistance.redDistance = coloredDistance.redDistance / range + 0.5;
            coloredDistance.greenDistance = coloredDistance.greenDistance / range + 0.5;
            coloredDistance.blueDistance = coloredDistance.blueDistance / range + 0.5;
            coloredDistance.alphaDistance = coloredDistance.alphaDistance / range + 0.5;

            return coloredDistance;
        }

        /// <summary>
        /// Generates MSDF texture
        /// </summary>
        /// <param name="size">Width and height of MSDF texture</param>
        /// <param name="glyphBoundingRectangle">Bounding rectangle of original glyph</param>
        /// <returns>MSDF color data in for of single-dimension array</returns>
        public Color[,] GenerateDirectMSDF(uint size, double pxRange, Rectangle glyphBoundingRectangle, List<LineSegment2D> glyphSegments, ushort em)
        {
            //var msdf = new List<Color>();
            var msdf = new Color[size, size];

            if (glyphSegments.Count == 0)
            {
                //return msdf.ToArray();
                return msdf;
            }
            
            segments = new List<MsdfGlyphSegment>();

            foreach (var segment in glyphSegments)
            {
                segments.Add(new MsdfGlyphSegment(segment.Start, segment.End));
            }
            
            // 1. Color all segments
            ColorEdges();
            
            // 2. Calculate boundaries for original glyph (the position of the EM square)
            var emSquare = new Rectangle(0, 0, em, em);

            // 3. Place EM square so that its center matches glyph center
            var glyphCenter = glyphBoundingRectangle.Center;
            var emSquareCenter = emSquare.Center;
            var diff = glyphCenter - emSquareCenter;
            diff.X = Math.Floor(diff.X);
            diff.Y = Math.Floor(diff.Y);

            emSquare.X += (int)diff.X;
            emSquare.Y += (int)diff.Y;

            // 4. Generate colored pseudo-distance field
            var coloredDistances = new ColoredDistance[size, size];

            var scale = size / (double)emSquare.Width;
            var range = GetRange(pxRange, scale, scale);

            var additionalSpace = emSquare.Width / 90.0;
            
            ColoredDistance minColoredDistance;
                        
            minColoredDistance.redDistance   = (-emSquare.Width / 2) / range + 0.5;
            minColoredDistance.greenDistance = (-emSquare.Width / 2) / range + 0.5;
            minColoredDistance.blueDistance  = (-emSquare.Width / 2) / range + 0.5;
            minColoredDistance.alphaDistance = (-emSquare.Width / 2) / range + 0.5;
            
            for (var y = 0; y < size; ++y)
            {
                for (var x = 0; x < size; ++x)
                {
                    // determine the closest segment to current sampling point
                    //var samplingPoint = new Vector2D(originalDimensions.X / size * (x + 0.5), originalDimensions.Y - (originalDimensions.Y / size * (y + 0.5)));
                    var samplingPoint = new Vector2D((emSquare.Width / size * (x + 0.5)) + emSquare.X, emSquare.Height - (emSquare.Height / size * (y + 0.5)) + emSquare.Y);

                    if (samplingPoint.X >= (glyphBoundingRectangle.X - additionalSpace) && samplingPoint.X <= (glyphBoundingRectangle.Right + additionalSpace) &&
                        samplingPoint.Y >= (glyphBoundingRectangle.Y - additionalSpace) && samplingPoint.Y <= (glyphBoundingRectangle.Bottom + additionalSpace))
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
            
            // 6. Normalize MSDF and SDF to [0 .. 255] range
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var red = PixelFloatToByte(coloredDistances[x, y].redDistance);
                    var green = PixelFloatToByte(coloredDistances[x, y].greenDistance);
                    var blue = PixelFloatToByte(coloredDistances[x, y].blueDistance);
                    var alpha = PixelFloatToByte(coloredDistances[x, y].alphaDistance);

                    var color = Color.FromRgba(red, green, blue, alpha);

                    //msdf.Add(color);
                    msdf[x, y] = color;
                }
            }

            //return msdf.ToArray();
            return msdf;
        }

        // --- ARTEFACT FIXING ---
        // true - no collision, false - collision
        private bool CheckNeighbor(ColoredDistance neighbor, ColoredDistance current)
        {
            const double threshold = 2.5;

            var cnt = 0;
            
            bool isNeighborRedPositive = (neighbor.redDistance >= 0);
            bool isNeighborGreenPositive = (neighbor.greenDistance >= 0);
            bool isNeighborBluePositive = (neighbor.blueDistance >= 0);

            bool isCurrentRedPositive = (current.redDistance >= 0);
            bool isCurrentGreenPositive = (current.greenDistance >= 0);
            bool isCurrentBluePositive = (current.blueDistance >= 0);

            if (isNeighborRedPositive ^ isCurrentRedPositive && 
                Math.Abs(neighbor.redDistance - current.redDistance) > threshold)
            {
                ++cnt;
            }
            
            if (isNeighborGreenPositive ^ isCurrentGreenPositive && 
                Math.Abs(neighbor.greenDistance - current.greenDistance) > threshold)
            {
                ++cnt;
            }
            
            if (isNeighborBluePositive ^ isCurrentBluePositive && 
                Math.Abs(neighbor.blueDistance - current.blueDistance) > threshold)
            {
                ++cnt;
            }

            return (cnt < 2);
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

                    if (!CheckForCollision(neighbors, current))
                    {
                        var correction = new CorrectionLocation
                        {
                            X = x,
                            Y = y
                        };

                        correctionList.Add(correction);
                    }
                }
            }

            foreach (var correction in correctionList)
            {
                var pixel = data[correction.X, correction.Y];
                var median = Median(pixel.redDistance, pixel.greenDistance, pixel.blueDistance);

                pixel.redDistance = pixel.greenDistance = pixel.blueDistance = median;

                data[correction.X, correction.Y] = pixel;
            }
        }
        
        // --- HELPERS ---
        private double Median(double a, double b, double c)
        {
            return Math.Max(Math.Min(a,b), Math.Min(Math.Max(a,b), c));
        }

        private double GetRange(double pxRange, double scaleX, double scaleY)
        {
            return pxRange / Math.Min(scaleX, scaleY);
        }

        private byte PixelFloatToByte(double x)
        {
            return (byte)(Clamp(256.0 * x, 255.0));
        }
        
        private double Clamp(double n, double b)
        {
            var tmp = n > 0 ? 1.0 : 0.0;

            return ((n >= 0 && n <= b) ? n : (tmp * b));
        }

        private Color ApplyColorMask(Color color, bool redMask, bool greenMask, bool blueMask)
        {
            color.R *= redMask ? (byte)1 : (byte)0;
            color.G *= greenMask ? (byte)1 : (byte)0;
            color.B *= blueMask ? (byte)1 : (byte)0;

            return color;
        }

        // --- ADDITIONAL DATA TYPES ---
        private struct ColoredDistance
        {
            public double redDistance;
            public double greenDistance;
            public double blueDistance;
            public double alphaDistance;
        }
        
        private struct CorrectionLocation
        {
            public int X;
            public int Y;
        }
        
        private class Contour
        {
            public List<Edge> Edges;

            public Contour()
            {
                Edges = new List<Edge>();
            }
        }
        
        private class Edge
        {
            public List<MsdfGlyphSegment> Segments;

            public Edge()
            {
                Segments = new List<MsdfGlyphSegment>();
            }
        }
    }
}