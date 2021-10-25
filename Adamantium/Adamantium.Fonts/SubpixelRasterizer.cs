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
        private ushort em;
        private double gamma;
        private double inversedGamma;
        
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
            public bool IsVisible;
        }

        public SubpixelRasterizer()
        {
            gamma = 1.43;
            inversedGamma = 1.0 / gamma;
        }
        
        public Color[] RasterizeGlyphBySubpixels(uint textSize, Color foreground, Color background, Rectangle glyphBoundingRectangle, List<LineSegment2D> glyphSegments, ushort em)
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
            this.em = em;

            // 1. Sample glyph with triple size width
            var sampledData = SampleSubpixels();
            
            // 2. Process sampled data
            return ProcessGlyphSubpixelSampling(sampledData);
        }
        
        private double[,] SampleSubpixels()
        {
            // 1. Calculate boundaries for original glyph
            //double glyphSize = Math.Max(glyphBoundingRectangle.Width, glyphBoundingRectangle.Height);
            var emSquareDimensions = new Vector2D(em);

            // 2. Place glyph in center of calculated boundaries
            var glyphCenter = glyphBoundingRectangle.Center;
            var emSquareCenter = emSquareDimensions / 2;
            var diff = emSquareCenter - glyphCenter;

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
            
            var subpixels = new double[width, height];
            
            var maxDist = Double.MinValue;
            
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                {
                    // determine the closest segment to current sampling point
                    var samplingPoint = new Vector2D(emSquareDimensions.X / width * x, emSquareDimensions.Y - (emSquareDimensions.Y / height * y));

                    subpixels[x, y] = GetSignedDistance(samplingPoint);

                    if (subpixels[x, y] > maxDist) maxDist = subpixels[x, y];
                }
            }

            // 4. Normalize
            var minDist = -maxDist;

            // for visualizing
            visSubpixels = new byte[width, height];
            // -------------
            
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                {
                    if (subpixels[x, y] < minDist) subpixels[x, y] = minDist;
                    if (subpixels[x, y] > maxDist) subpixels[x, y] = maxDist;

                    subpixels[x, y] = (subpixels[x, y] - minDist) / (maxDist - minDist);
                    visSubpixels[x, y] = (byte)(255 * subpixels[x, y]);
                }
            }

            return subpixels;
        }
        
        private double GetSignedDistance(Vector2D point)
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

            return closestDistance;
        }

        private Color[] ProcessGlyphSubpixelSampling(double[,] sampledData)
        {
            var rebalancedSubpixels = RebalanceEnergy(sampledData);

            return ConvertToPixels(rebalancedSubpixels);
        }

        private List<Subpixel> RebalanceEnergy(double[,] subpixels)
        {
            var width = subpixels.GetLength(0);
            var height = subpixels.GetLength(1);

            var redForegroundLinear = Math.Pow(foreground.R / 255.0, inversedGamma);
            var greenForegroundLinear = Math.Pow(foreground.G / 255.0, inversedGamma);
            var blueForegroundLinear = Math.Pow(foreground.B / 255.0, inversedGamma);

            var redBackgroundLinear = Math.Pow(background.R / 255.0, inversedGamma);
            var greenBackgroundLinear = Math.Pow(background.G / 255.0, inversedGamma);
            var blueBackgroundLinear = Math.Pow(background.B / 255.0, inversedGamma);

            var blendedSubpixels = new List<Subpixel>();

            // for visualizing
            //visSubpixels = new byte[width, height];
            // -------------
            
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var blendedValue = 0.0;

                    var currentEnergy = subpixels[x, y];// > 0.5 ? 1.0 : subpixels[x, y];

                    if (x % 3 == 0) blendedValue = currentEnergy * redForegroundLinear   + (1.0 - currentEnergy) * redBackgroundLinear;
                    if (x % 3 == 1) blendedValue = currentEnergy * greenForegroundLinear + (1.0 - currentEnergy) * greenBackgroundLinear;
                    if (x % 3 == 2) blendedValue = currentEnergy * blueForegroundLinear  + (1.0 - currentEnergy) * blueBackgroundLinear;
                    
                    var blendedSubpixel = new Subpixel();
                    blendedSubpixel.Energy = (byte)(255.0 * Math.Pow(blendedValue, gamma));
                    //blendedSubpixel.IsVisible = (currentEnergy > 0.5);
                    blendedSubpixel.IsVisible = true;
                    
                    blendedSubpixels.Add(blendedSubpixel);
                    
                    // for visualizing
                    //visSubpixels[x, y] = (byte)(255.0 * Math.Pow(blendedValue, gamma));
                    // ----------------
                }
            }

            return blendedSubpixels;
        }

        private Color[] ConvertToPixels(List<Subpixel> subpixels)
        {
            var pixels = new List<Color>();
            
            for (var i = 2; i < subpixels.Count; i+=3)
            {
                bool isRedVisible = subpixels[i - 2].IsVisible;
                bool isGreenVisible = subpixels[i - 1].IsVisible;
                bool isBlueVisible = subpixels[i].IsVisible;
                
                byte red = subpixels[i - 2].Energy;
                byte green = subpixels[i - 1].Energy;
                byte blue = subpixels[i].Energy;

                byte alpha = (byte)(isRedVisible || isGreenVisible || isBlueVisible ? 255 : 0);

                pixels.Add(Color.FromRgba((byte)red, (byte)green, (byte)blue, 255));
            }

            return pixels.ToArray();
        }
    }
}