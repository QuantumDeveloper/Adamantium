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
            
            var groups = atlasData.GlyphData.GroupBy(x => x.Height).OrderByDescending(x=>x.Key).Select(group=>group.OrderByDescending(x=>x.Width)).ToList();
            
            uint yOffset = 0;
            uint xOffset = 0;
            int resultWidth = (int)pixelsPerRow;
            var heights = new List<uint>();
            int index = 0;
            foreach (var group in groups)
            {
                foreach (var glyphData in group)
                {
                    var textureWidth = glyphData.Width;
                    heights.Add(glyphData.Height);
                    
                    glyphData.X = (uint)xOffset;
                    glyphData.Y = (uint)yOffset;
                    // glyphData.UV = (float)(xOffset / atlasSize.Width);
                    // glyphData.V = (float)(yOffset / atlasSize.Height);
                    
                    xOffset += textureWidth;
                    if (xOffset >= resultWidth)
                    {
                        xOffset = 0;
                        var maxHeight = heights.Max();
                        yOffset += (uint)maxHeight;
                        heights.Clear();
                        glyphData.X = (uint)xOffset;
                        glyphData.Y = (uint)yOffset;
                    }

                    index++;
                }
            }
            if (heights.Count > 0)
            {
                var maxHeight = heights.Max();
                yOffset += (uint)maxHeight;
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
                    var textureWidth = (int)glyphData.Width;
                    var textureHeight = (int)glyphData.Height;
                    
                    xOffset = (uint)(glyphData.X * 4);
                    yOffset = (uint)glyphData.Y;

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