using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.TextureGeneration
{
    public class TextureAtlasGenerator
    {
        private FontAtlasData atlasData;
        private Size atlasSize;
        private UInt32 glyphTextureSize;
        private byte sampleRate;
        private double pxRange;
        private Typeface typeface;
        private IFont font;
        private uint startGlyphIndex;
        private uint glyphCount;
        private GlyphSortingVariant sortingVariant;
        private uint glyphMargin;

        public TextureAtlasGenerator(
            Typeface typeface, 
            IFont font, 
            uint glyphTextureSize,
            byte sampleRate, 
            double pxRange, 
            uint startGlyphIndex, 
            uint glyphCount,
            GlyphSortingVariant sortingVariant,
            uint glyphMargin)
        {
            this.glyphTextureSize = glyphTextureSize;
            this.sampleRate = sampleRate;
            this.pxRange = pxRange;
            this.typeface = typeface;
            this.font = font;
            this.startGlyphIndex = startGlyphIndex;
            this.glyphCount = glyphCount;
            this.sortingVariant = sortingVariant;
            this.glyphMargin = glyphMargin;
            atlasData = new FontAtlasData(glyphTextureSize);
        }

        public FontAtlasData PrepareTextureAtlas(bool useProportionalSize = true)
        {
            Parallel.For((int)startGlyphIndex, (int)(startGlyphIndex + glyphCount),
                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, (index) => CalculateTextureDataForGlyph(index, useProportionalSize));

            var totalBytes = atlasData.GlyphData.Sum(x => x.Pixels.Length);
            var totalPixels = totalBytes / 4; 
            var pixelsPerRow = (uint)Math.Ceiling(Math.Sqrt(totalPixels));

            return sortingVariant == GlyphSortingVariant.ByIndex
                ? CalculateForSortingByIndex(pixelsPerRow)
                : CalculateForSortingBySize(pixelsPerRow);
        }

        private FontAtlasData CalculateForSortingBySize(uint pixelsPerRow)
        {
            var textureData = atlasData.GlyphData
                .GroupBy(x => x.BoundingRect.Height)
                .OrderByDescending(x => x.Key)
                .SelectMany(group => group.OrderByDescending(x => x.BoundingRect.Width))
                .ToList();
            
            return CalculateFontAtlasData(textureData, pixelsPerRow);
        }
        
        public FontAtlasData CalculateForSortingByIndex(uint pixelsPerRow)
        {
            var textureData = atlasData.GlyphData
                .OrderBy(x => x.GlyphIndex)
                .ToList();

            return CalculateFontAtlasData(textureData, pixelsPerRow);
        }

        private FontAtlasData CalculateFontAtlasData(List<GlyphTextureData> textureData, uint pixelsPerRow, bool makeFullCalculations = false)
        {
            int yOffset = 0;
            int xOffset = 0;
            int resultWidth = (int)pixelsPerRow;
            var heights = new List<int>();
            
            foreach (var glyphData in textureData)
            {
                var textureWidth = (int)glyphData.FullGlyphSize.Width;
                heights.Add((int)glyphData.FullGlyphSize.Height);

                glyphData.BoundingRect.Left = xOffset;
                glyphData.BoundingRect.Top = yOffset;
                
                if (xOffset + textureWidth >= resultWidth)
                {
                    xOffset = 0;
                    var maxHeight = heights.Max();
                    yOffset += maxHeight;
                    heights.Clear();
                        
                    glyphData.BoundingRect.Left = xOffset;
                    glyphData.BoundingRect.Top = yOffset;
                }
                xOffset += textureWidth;
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
            foreach (var glyphData in textureData)
            {
                // used when we need to copy all data to the resulting texture
                if (makeFullCalculations)
                {
                    var textureWidth = (int)glyphData.FullGlyphSize.Width;
                    var textureHeight = (int)glyphData.FullGlyphSize.Height;

                    xOffset = glyphData.BoundingRect.Left * 4;
                    yOffset = glyphData.BoundingRect.Top;

                    for (int y = 0; y < textureHeight; y++)
                    {
                        var sourceIndex = y * textureWidth * 4;
                        var destinationIndex = xOffset + ((yOffset + y) * resultWidth);
                        Array.Copy(glyphData.Pixels, sourceIndex, atlasData.ImageData, destinationIndex,
                            textureWidth * 4);
                    }
                }
                else
                {
                    glyphData.CalculateUV(atlasSize);
                }
            }
            
            return atlasData;
        }

        public GlyphTextureData[] GenerateTextureForGlyphs(Glyph[] glyphs)
        {
            if (glyphs == null || glyphs.Length == 0)
            {
                return Array.Empty<GlyphTextureData>();
            }

            Parallel.ForEach(glyphs,
                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, GenerateTextureForGlyph);

            return atlasData.GetGlyphData(glyphs.Select(x=>x.Index).ToArray());
        }

        public FontAtlasData GenerateTextureAtlas()
        {
            if (glyphCount <= 0)
            {
                return default;
            }

            Parallel.For((int)startGlyphIndex, (int)(startGlyphIndex + glyphCount),
                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, GenerateTextureForGlyph);

            var totalBytes = atlasData.GlyphData.Sum(x => x.Pixels.Length);
            var totalPixels = totalBytes / 4; 
            var pixelsPerRow = (uint)Math.Ceiling(Math.Sqrt(totalPixels));

            var textureData = atlasData.GlyphData
                .GroupBy(x => x.BoundingRect.Height)
                .OrderByDescending(x => x.Key)
                .SelectMany(group => group.OrderByDescending(x => x.BoundingRect.Width))
                .ToList();
            
            return CalculateFontAtlasData(textureData, pixelsPerRow, true);
        }
        
        private void GenerateTextureForGlyph(int glyphIndex)
        {
            typeface.GetGlyphByIndex((uint)glyphIndex, out var glyph);
            glyph.Sample(sampleRate);

            var textureData = glyph.GenerateDirectMSDF(glyphTextureSize, pxRange, font.UnitsPerEm, glyphMargin);
            if (textureData == null) return;
            
            atlasData.AddGlyphData(textureData);
        }

        private void GenerateTextureForGlyph(Glyph glyph)
        {
            glyph.CalculateEmRelatedMultipliers(font.UnitsPerEm);
            glyph.Sample(sampleRate);
            var textureData = atlasData.GetGlyphData(glyph.Index);
            if (textureData == null) return;

            glyph.GenerateGlyphData(textureData, pxRange, font.UnitsPerEm);
        }

        private void CalculateTextureDataForGlyph(int glyphIndex, bool useProportionalSize = true)
        {
            typeface.GetGlyphByIndex((uint)glyphIndex, out var glyph);
            glyph.Sample(sampleRate);
            var textureData = glyph.IsEmpty ? null : glyph.PrepareData(glyphTextureSize, font.UnitsPerEm, glyphMargin, useProportionalSize);
            if (textureData == null) return;
            atlasData.AddGlyphData(textureData);
        }
    }
}