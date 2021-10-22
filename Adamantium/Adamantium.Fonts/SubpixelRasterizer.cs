using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.GPOS;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public class SubpixelRasterizer
    {
        private uint textSize;
        private Color foreground;
        private Color background;
        private Rectangle glyphBoundingRectangle;
        private List<LineSegment2D> glyphSegments;
        
        // for visualizing
        private byte[,] visSubpixels;
        public byte[,] GetVisSubpixels()
        {
            return visSubpixels;
        }
        // ----------------
        
        private struct Subpixel
        {
            public byte Energy;
            public bool isInsideGlyph;
        }
        
        public Color[] RasterizeGlyphBySubpixels(uint textSize, Color foreground, Color background, Rectangle glyphBoundingRectangle, List<LineSegment2D> glyphSegments)
        {
            if (glyphSegments.Count == 0)
            {
                return new List<Color>().ToArray();
            }
            
            this.textSize = textSize;
            this.foreground = foreground;
            this.background = background;
            this.glyphBoundingRectangle = glyphBoundingRectangle;
            this.glyphSegments = glyphSegments;

            // 1. Sample glyph with triple size width
            var sampledData = SampleSubpixels();
            
            // 2. Process sampled data
            return ProcessGlyphSubpixelSampling(sampledData);
        }
        
        private bool[,] SampleSubpixels()
        {
            // 1. Calculate boundaries for original glyph
            double glyphSize = Math.Max(glyphBoundingRectangle.Width, glyphBoundingRectangle.Height);
            var originalDimensions = new Vector2D(glyphSize);

            // 2. Place glyph in center of calculated boundaries
            var glyphCenter = glyphBoundingRectangle.Center;
            var originalCenter = originalDimensions / 2;
            var diff = originalCenter - glyphCenter;

            for (var index = 0; index < glyphSegments.Count; index++)
            {
                var segment = glyphSegments[index];
                var start = segment.Start;
                var end = segment.End;

                segment = new LineSegment2D(start + diff, end + diff);

                glyphSegments[index] = segment;
            }
            
            // 3. Sample glyph by subpixels
            var width = textSize * 3;
            var height = textSize;
            
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

            return (closestDistance >= 0);
        }

        private Color[] ProcessGlyphSubpixelSampling(bool[,] sampledData)
        {
            var width = sampledData.GetLength(0);
            var height = sampledData.GetLength(1);

            var subpixels = new Subpixel[width, height];
            
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    subpixels[x, y].isInsideGlyph = sampledData[x, y];
                    
                    if (x % 3 == 0) subpixels[x, y].Energy = subpixels[x, y].isInsideGlyph ? foreground.R : background.R;
                    if (x % 3 == 1) subpixels[x, y].Energy = subpixels[x, y].isInsideGlyph ? foreground.G : background.G;
                    if (x % 3 == 2) subpixels[x, y].Energy = subpixels[x, y].isInsideGlyph ? foreground.B : background.B;
                }
            }

            var rebalancedSubpixels = RebalanceEnergy(subpixels);

            return ConvertToPixels(rebalancedSubpixels);
        }

        private List<Subpixel> RebalanceEnergy(Subpixel[,] subpixels)
        {
            var width = subpixels.GetLength(0);
            var height = subpixels.GetLength(1);

            // for visualizing
            visSubpixels = new byte[width, height];
            // -------------
            
            var rebalancedSubpixels = new List<Subpixel>();

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    Subpixel mostLeftNeighbor = new Subpixel();
                    Subpixel leastLeftNeighbor = new Subpixel();
                    Subpixel currentSubpixel = subpixels[x, y];
                    Subpixel leastRightNeighbor = new Subpixel();
                    Subpixel mostRightNeighbor = new Subpixel();

                    if (x == 0)
                    {
                        mostLeftNeighbor = new Subpixel() {isInsideGlyph = false, Energy = background.G};
                        leastLeftNeighbor = new Subpixel() {isInsideGlyph = false, Energy = background.B};
                        leastRightNeighbor = subpixels[x + 1, y];
                        mostRightNeighbor = subpixels[x + 2, y];
                    }
                    else if (x == 1)
                    {
                        mostLeftNeighbor = new Subpixel() {isInsideGlyph = false, Energy = background.B};
                        leastLeftNeighbor = subpixels[x - 1, y];
                        leastRightNeighbor = subpixels[x + 1, y];
                        mostRightNeighbor = subpixels[x + 2, y];
                    }
                    else if (x == (width - 2))
                    {
                        mostLeftNeighbor = subpixels[x - 2, y];
                        leastLeftNeighbor = subpixels[x - 1, y];
                        leastRightNeighbor = subpixels[x + 1, y];
                        mostRightNeighbor = new Subpixel() {isInsideGlyph = false, Energy = background.R};
                    }
                    else if (x == (width - 1))
                    {
                        mostLeftNeighbor = subpixels[x - 2, y];
                        leastLeftNeighbor = subpixels[x - 1, y];
                        leastRightNeighbor = new Subpixel() {isInsideGlyph = false, Energy = background.R};
                        mostRightNeighbor = new Subpixel() {isInsideGlyph = false, Energy = background.G};
                    }
                    else
                    {
                        mostLeftNeighbor = subpixels[x - 2, y];
                        leastLeftNeighbor = subpixels[x - 1, y];
                        leastRightNeighbor = subpixels[x + 1, y];
                        mostRightNeighbor = subpixels[x + 2, y];
                    }

                    var rebalancedSubpixelEnergy = (mostLeftNeighbor.Energy / 9.0) +
                                                   ((leastLeftNeighbor.Energy * 2.0) / 9.0) +
                                                   ((currentSubpixel.Energy * 3.0) / 9.0) +
                                                   ((leastRightNeighbor.Energy * 2.0) / 9.0) +
                                                   (mostRightNeighbor.Energy / 9.0);

                    bool isWholePixelInside = false;

                    if (x % 3 == 0) isWholePixelInside = currentSubpixel.isInsideGlyph && leastRightNeighbor.isInsideGlyph && mostRightNeighbor.isInsideGlyph;
                    if (x % 3 == 1) isWholePixelInside = leastLeftNeighbor.isInsideGlyph && currentSubpixel.isInsideGlyph && leastRightNeighbor.isInsideGlyph;
                    if (x % 3 == 2) isWholePixelInside = mostLeftNeighbor.isInsideGlyph && leastLeftNeighbor.isInsideGlyph && currentSubpixel.isInsideGlyph;
                    
                    bool isPixelPartiallyInside = false;

                    if (x % 3 == 0) isPixelPartiallyInside = currentSubpixel.isInsideGlyph || leastRightNeighbor.isInsideGlyph || mostRightNeighbor.isInsideGlyph;
                    if (x % 3 == 1) isPixelPartiallyInside = leastLeftNeighbor.isInsideGlyph || currentSubpixel.isInsideGlyph || leastRightNeighbor.isInsideGlyph;
                    if (x % 3 == 2) isPixelPartiallyInside = mostLeftNeighbor.isInsideGlyph || leastLeftNeighbor.isInsideGlyph || currentSubpixel.isInsideGlyph;

                    if (!isWholePixelInside && isPixelPartiallyInside)
                    {
                        currentSubpixel.Energy = (byte) rebalancedSubpixelEnergy;
                    }

                    // for visualizing
                    visSubpixels[x, y] = currentSubpixel.Energy;
                    // ----------------
                    
                    rebalancedSubpixels.Add(currentSubpixel);
                }
            }

            return rebalancedSubpixels;
        }

        private Color[] ConvertToPixels(List<Subpixel> subpixels)
        {
            var pixels = new List<Color>();
            
            for (var i = 2; i < subpixels.Count; i+=3)
            {
                bool isRedInside = subpixels[i - 2].isInsideGlyph;
                bool isGreenInside = subpixels[i - 1].isInsideGlyph;
                bool isBlueInside = subpixels[i].isInsideGlyph;
                
                byte red = subpixels[i - 2].Energy;
                byte green = subpixels[i - 1].Energy;
                byte blue = subpixels[i].Energy;

                //byte alpha = (byte)(isRedVisible || isGreenVisible || isBlueVisible ? 255 : 0);

                pixels.Add(Color.FromRgba(red, green, blue, 255));
            }

            return pixels.ToArray();
        }
    }
}