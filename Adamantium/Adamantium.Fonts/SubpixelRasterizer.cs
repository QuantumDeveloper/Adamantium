using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public class SubpixelRasterizer
    {
        private List<LineSegment2D> segments;
        private Rectangle boundingRectangle;
        private uint size;

        public Color[] RasterizeGlyphBySubpixels(uint requiredSize, Rectangle glyphBoundingRectangle, List<LineSegment2D> glyphSegments)
        {
            segments = glyphSegments;
            boundingRectangle = glyphBoundingRectangle;
            size = requiredSize;
            
            // 1. Sample glyph with triple size width
            var sampledData = SampleSubpixels();
            
            // 2. Process sampled data
            return ProcessGlyphSubpixelSampling(sampledData);
        }
        
        private bool[,] SampleSubpixels()
        {
            // 1. Calculate boundaries for original glyph
            double glyphSize = Math.Max(boundingRectangle.Width, boundingRectangle.Height);
            var originalDimensions = new Vector2D(glyphSize);

            // 2. Place glyph in center of calculated boundaries
            var glyphCenter = boundingRectangle.Center;
            var originalCenter = originalDimensions / 2;
            var diff = originalCenter - glyphCenter;

            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                var start = segment.Start;
                var end = segment.End;

                segment = new LineSegment2D(start + diff, end + diff);

                segments[index] = segment;
            }
            
            // 3. Sample glyph by subpixels
            var width = size * 3;
            var height = size;
            
            var subpixels = new bool[width, height];
            
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                {
                    // determine the closest segment to current sampling point
                    var samplingPoint = new Vector2D(originalDimensions.X / width * (x + 0.5), originalDimensions.Y - (originalDimensions.Y / height * (y + 0.5)));

                    subpixels[x, y] = IsSubpixelInsideGlyph(samplingPoint);
                }
            }

            return subpixels;
        }
        
        private bool IsSubpixelInsideGlyph(Vector2D point)
        {
            double closestDistance = Double.MaxValue;

            // there can be up to two closest segments in case if point is close to segments' connection
            // we will store both and then determine the signed pseudo-distance
            // if these two signed pseudo-distances have different signs, use the one with negative, because the point is outside
            var closestSegments = new List<LineSegment2D>();

            foreach (var segment in segments)
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

            return (closestDistance >= 0);
        }

        private Color[] ProcessGlyphSubpixelSampling(bool[,] sampledData)
        {
            var width = sampledData.GetLength(0);
            var height = sampledData.GetLength(1);
            
            var subpixels = new byte[width, height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    subpixels[x, y] = (sampledData[x, y] ? (byte)0 : (byte)255);
                }
            }

            var rebalancedSubpixels = RebalanceEnergy(subpixels);

            return ConvertToPixels(rebalancedSubpixels);
        }

        private List<byte> RebalanceEnergy(byte[,] subpixels)
        {
            var width = subpixels.GetLength(0);
            var height = subpixels.GetLength(1);

            var rebalancedSubpixels = new List<byte>();

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    byte mostLeftNeighborEnergy = 255;
                    byte leastLeftNeighborEnergy = 255;
                    byte currentSubpixelEnergy = subpixels[x, y];
                    byte leastRightNeighborEnergy = 255;
                    byte mostRightNeighborEnergy = 255;

                    if (x > 0)
                    {
                        leastLeftNeighborEnergy = subpixels[x - 1, y];
                    }
                    
                    if (x > 1)
                    {
                        mostLeftNeighborEnergy = subpixels[x - 2, y];
                    }

                    if (x < (width - 2))
                    {
                        mostRightNeighborEnergy = subpixels[x + 2, y];
                    }
                    
                    if (x < (width - 1))
                    {
                        leastRightNeighborEnergy = subpixels[x + 1, y];
                    }

                    var rebalancedSubpixelEnergy = (mostLeftNeighborEnergy / 9.0) + ((leastLeftNeighborEnergy * 2.0) / 9.0) + ((currentSubpixelEnergy * 3.0) / 9.0) + ((leastRightNeighborEnergy * 2.0) / 9.0) + (mostRightNeighborEnergy / 9.0);

                    rebalancedSubpixels.Add((byte)rebalancedSubpixelEnergy);
                }
            }
            
            return rebalancedSubpixels;
        }

        private Color[] ConvertToPixels(List<byte> subpixels)
        {
            var pixels = new List<Color>();
            
            for (var i = 2; i < subpixels.Count; i+=3)
            {
                byte red = subpixels[i - 2];
                byte green = subpixels[i - 1];
                byte blue = subpixels[i];
                byte alpha = (byte)(red == 255 && green == 255 && blue == 255 ? 0 : 255);

                pixels.Add(Color.FromRgba(red, green, blue, alpha));
            }

            return pixels.ToArray();
        }
    }
}