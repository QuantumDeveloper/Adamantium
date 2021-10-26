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
            public double Distance;
            public bool IsVisible;
        }

        public SubpixelRasterizer()
        {
            gamma = 1.43;
        }

        private double EncodedToBrightness(byte encoded)
        {
            return Math.Pow(encoded / 255.0, gamma);
        }
        
        private byte BrightnessToEncoded(double brightness)
        {
            return (byte)(255.0 * Math.Pow(brightness, 1.0 / gamma));
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
        
        private Subpixel[,] SampleSubpixels()
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
            
            var subpixels = new Subpixel[width, height];
            
            var minDist = Double.MaxValue;
            var maxDist = Double.MinValue;
            
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                {
                    // determine the closest segment to current sampling point
                    var samplingPoint = new Vector2D((emSquare.Width / width * x) + emSquare.X, emSquare.Height - (emSquare.Height / height * y) + emSquare.Y);

                    subpixels[x, y].Distance = GetSignedDistance(samplingPoint);
                    subpixels[x, y].IsVisible = (subpixels[x, y].Distance >= 0);

                    if (subpixels[x, y].Distance < minDist) minDist = subpixels[x, y].Distance;
                    if (subpixels[x, y].Distance > maxDist) maxDist = subpixels[x, y].Distance;
                }
            }

            // 4. Normalize
            maxDist = Math.Min(Math.Abs(minDist), maxDist);
            minDist = -maxDist * 2;

            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                {
                    if (subpixels[x, y].Distance < minDist) subpixels[x, y].Distance = minDist;
                    if (subpixels[x, y].Distance > maxDist) subpixels[x, y].Distance = maxDist;

                    subpixels[x, y].Distance = (subpixels[x, y].Distance - minDist) / (maxDist - minDist);
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

        private Color[] ProcessGlyphSubpixelSampling(Subpixel[,] sampledData)
        {
            var blendedSubpixels = BlendSubpixel(sampledData);

            return ConvertToPixels(blendedSubpixels);
        }

        private List<Subpixel> BlendSubpixel(Subpixel[,] subpixels)
        {
            var width = subpixels.GetLength(0);
            var height = subpixels.GetLength(1);

            var redForegroundLinear = EncodedToBrightness(foreground.R);
            var greenForegroundLinear = EncodedToBrightness(foreground.G);
            var blueForegroundLinear = EncodedToBrightness(foreground.B);

            var redBackgroundLinear = EncodedToBrightness(background.R);
            var greenBackgroundLinear = EncodedToBrightness(background.G);
            var blueBackgroundLinear = EncodedToBrightness(background.B);

            var blendedSubpixels = new List<Subpixel>();

            // for visualizing
            visSubpixels = new byte[width, height];
            // -------------
            
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var blendedValue = 0.0;

                    var currentDistance = subpixels[x, y].Distance;

                    if (x % 3 == 0) blendedValue = currentDistance * redForegroundLinear   + (1.0 - currentDistance) * redBackgroundLinear;
                    if (x % 3 == 1) blendedValue = currentDistance * greenForegroundLinear + (1.0 - currentDistance) * greenBackgroundLinear;
                    if (x % 3 == 2) blendedValue = currentDistance * blueForegroundLinear  + (1.0 - currentDistance) * blueBackgroundLinear;
                    
                    var blendedSubpixel = new Subpixel();
                    blendedSubpixel.Energy = BrightnessToEncoded(blendedValue);
                    blendedSubpixel.IsVisible = subpixels[x, y].IsVisible;

                    blendedSubpixels.Add(blendedSubpixel);
                    
                    // for visualizing
                    visSubpixels[x, y] = blendedSubpixel.Energy;
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

                pixels.Add(Color.FromRgba(red, green, blue, 255));
            }

            return pixels.ToArray();
        }
    }
}