using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Mathematics;
using Color = Adamantium.Mathematics.Color;

namespace Adamantium.Fonts.TextureGeneration
{
    public enum AtlasGeneratorKind
    {
        Msdf,
        Subpixel
    }

    public class TextureAtlasGenerator
    {
        private object objLocker = new object();

        private FontAtlasData atlasData;
        private Size atlasSize;
        private UInt32 glyphTextureSize;
        private byte sampleRate;
        private double pxRange;
        private TypeFace typeFace;
        private IFont font;
        private uint startGlyphIndex;

        public FontAtlasData GenerateTextureAtlas(
            TypeFace typeFace, 
            IFont font, 
            uint glyphTextureSize,
            byte sampleRate, 
            double pxRange, 
            uint startGlyphIndex, 
            uint glyphCount)
        {
            if (glyphCount <= 0)
            {
                return default;
            }

            this.glyphTextureSize = glyphTextureSize;
            this.sampleRate = sampleRate;
            this.pxRange = pxRange;
            this.typeFace = typeFace;
            this.font = font;
            this.startGlyphIndex = startGlyphIndex;

            var timer1 = Stopwatch.StartNew();

            atlasData = new FontAtlasData();

            Parallel.For((int)startGlyphIndex, (int)(startGlyphIndex + glyphCount),
                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, GenerateTextureForGlyph);

            timer1.Stop();
            var timer2 = Stopwatch.StartNew();
            var totalBytes = atlasData.GlyphData.Sum(x => x.Pixels.Length);
            var totalPixels = totalBytes / 4; 
            var pixelsPerRow = (uint)Math.Ceiling(Math.Sqrt(totalPixels));

            var groups = atlasData.GlyphData
                .GroupBy(x => x.BoundingRect.Height)
                .OrderByDescending(x => x.Key)
                .Select(group => group.OrderByDescending(x => x.BoundingRect.Width))
                .ToList();
            
            int yOffset = 0;
            int xOffset = 0;
            int resultWidth = (int)pixelsPerRow;
            var heights = new List<int>();
            foreach (var group in groups)
            {
                foreach (var glyphData in group)
                {
                    var textureWidth = glyphData.BoundingRect.Width;
                    heights.Add(glyphData.BoundingRect.Height);
                    
                    glyphData.BoundingRect.Left = (int)xOffset;
                    glyphData.BoundingRect.Top = (int)yOffset;
                    glyphData.UV.Left = (float)(xOffset / atlasSize.Width);
                    glyphData.UV.Top = (float)(yOffset / atlasSize.Height);
                    glyphData.UV.Right = (float)(glyphData.BoundingRect.Right / atlasSize.Width);
                    glyphData.UV.Bottom = (float)(glyphData.BoundingRect.Bottom / atlasSize.Height);
                    
                    xOffset += textureWidth;
                    if (xOffset >= resultWidth)
                    {
                        xOffset = 0;
                        var maxHeight = heights.Max();
                        yOffset += maxHeight;
                        heights.Clear();
                        
                        glyphData.BoundingRect.Left = (int)xOffset;
                        glyphData.BoundingRect.Top = (int)yOffset;
                    }
                }
            }
            if (heights.Count > 0)
            {
                var maxHeight = heights.Max();
                yOffset += maxHeight;
                heights.Clear();
            }
            
            atlasSize = new Size(resultWidth, yOffset);
            
            atlasData.ImageData = new byte[(int)atlasSize.Width * (int)atlasSize.Height * 4];
            atlasData.AtlasSize = atlasSize;
            
            resultWidth = (int)atlasSize.Width * 4;
            foreach (var group in groups)
            {
                foreach (var glyphData in group)
                {
                    var textureWidth = glyphData.BoundingRect.Width;
                    var textureHeight = glyphData.BoundingRect.Height;
                    
                    xOffset = glyphData.BoundingRect.Left * 4;
                    yOffset = glyphData.BoundingRect.Top;

                    for (int y = 0; y < textureHeight; y++)
                    {
                        var sourceIndex = y * textureWidth * 4;
                        var destinationIndex = xOffset + ((yOffset + y) * resultWidth);
                        Array.Copy(glyphData.Pixels, sourceIndex, atlasData.ImageData, destinationIndex, textureWidth * 4);
                    }
                }
            }
            timer2.Stop();

            return atlasData;
        }

        private void GenerateTextureForGlyph(int glyphIndex)
        {
            typeFace.GetGlyphByIndex((uint)glyphIndex, out var glyph);
            glyph.Sample(sampleRate);

            GlyphTextureData rawGlyph = glyph.GenerateDirectMSDF(glyphTextureSize, pxRange, font.UnitsPerEm);
            if (rawGlyph == null) return;

            lock (objLocker)
            {
                atlasData.GlyphData.Add(rawGlyph);
            }
        }

        private Size CalculateAtlasDimensions(uint glyphCount)
        {
            var glyphsPerRow = (uint)Math.Ceiling(Math.Sqrt(glyphCount));
            var glyphsPerColumn = (uint)Math.Ceiling((double)glyphCount / glyphsPerRow);

            return new Size(glyphsPerRow, glyphsPerColumn);
        }
    }
}