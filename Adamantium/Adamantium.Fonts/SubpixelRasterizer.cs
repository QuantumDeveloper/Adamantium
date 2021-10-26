using System;
using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.GPOS;
using Adamantium.Imaging;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
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

        /*private double EncodedToBrightness(byte encoded)
        {
            return Math.Pow(encoded / 255.0, gamma);
        }
        
        private byte BrightnessToEncoded(double brightness)
        {
            return (byte)(255.0 * Math.Pow(brightness, 1.0 / gamma));
        }*/
        
        public Color[] RasterizeGlyphBySubpixels(uint textSize, Rectangle glyphBoundingRectangle, List<LineSegment2D> glyphSegments, ushort em)
        {
            if (glyphSegments.Count == 0)
            {
                return new List<Color>().ToArray();
            }
            
            this.textSize = textSize;
            this.glyphBoundingRectangle = glyphBoundingRectangle;
            this.glyphSegments = glyphSegments;
            this.em = em;

            // Sample glyph with triple size width
            return SampleSubpixels();
        }
        
        private Color[] SampleSubpixels()
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

            var distances = new List<double>();

            var minDist = Double.MaxValue;
            var maxDist = Double.MinValue;
            
            for (var y = 0; y < height; ++y)
            {
                for (var x = 0; x < width; ++x)
                {
                    // determine the closest segment to current sampling point
                    var samplingPoint = new Vector2D((emSquare.Width / width * (x + 0.0)) + emSquare.X, emSquare.Height - (emSquare.Height / height * (y + 0.0)) + emSquare.Y);

                    var distance = GetSignedDistance(samplingPoint);

                    distances.Add(distance);

                    if (distance < minDist) minDist = distance;
                    if (distance > maxDist) maxDist = distance;
                }
            }

            // 4. Normalize
            maxDist = Math.Min(Math.Abs(minDist), Math.Abs(maxDist));
            minDist = -maxDist * 2;

            var colors = new List<Color>();
            Color color = default;
            color.A = 255;
            
            for (var i = 0; i < distances.Count; i++)
            {
                if (distances[i] < minDist) distances[i] = minDist;
                if (distances[i] > maxDist) distances[i] = maxDist;

                if (i % 3 == 0) color.R = (byte)(255 * (distances[i] - minDist) / (maxDist - minDist));
                if (i % 3 == 1) color.G = (byte)(255 * (distances[i] - minDist) / (maxDist - minDist));
                if (i % 3 == 2)
                {
                    color.B = (byte)(255 * (distances[i] - minDist) / (maxDist - minDist));
                    colors.Add(color);
                }
            }

            return colors.ToArray();
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

        /*private Color[] ProcessGlyphSubpixelSampling(Subpixel[,] sampledData)
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
        }*/   
    }
}