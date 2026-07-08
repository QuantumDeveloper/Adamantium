using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Mathematics;

namespace Adamantium.Fonts.TextureGeneration
{
    public class TextureAtlasGenerator
    {
        private FontAtlasData atlasData;
        private Typeface typeface;
        private FontParameters parameters;
        private IFont font;

        public TextureAtlasGenerator(
            Typeface typeface,
            IFont font,
            FontAtlasData atlasData,
            FontParameters parameters)
        {
            this.atlasData = atlasData;
            this.parameters = parameters;
            this.typeface = typeface;
            this.font = font;
        }

        // This method is needed when we want to create a texture for a certain number of glyphs
        // before we start to process them 
        public FontAtlasData PrepareTextureAtlas(bool useProportionalSize = true)
        {
            Parallel.For((int)parameters.StartGlyphIndex, (int)(parameters.StartGlyphIndex + parameters.GlyphCount),
                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                (index) => CalculateTextureDataForGlyph((uint)index, useProportionalSize));
        
            var totalBytes = atlasData.GlyphData.Sum(x => x.Pixels.Length);
            var totalPixels = totalBytes / 4; 
            var pixelsPerRow = (uint)Math.Ceiling(Math.Sqrt(totalPixels));
        
            return parameters.SortingVariant == GlyphSortingVariant.ByIndex
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

        private void CalculateTextureDataForAtlas(GlyphTextureData[] textureData)
        {
            // Tight shelf packing (next-fit): place each glyph's bitmap (FullGlyphSize) right after the
            // previous one on the current shelf; when it would overflow the atlas width, drop to a new shelf
            // just below the tallest bitmap on the shelf so far. The shelf is only as tall as its tallest
            // glyph (no reserved per-row height), so vertical space isn't wasted and the texture holds the
            // most glyphs. Each glyph is already centred inside its own cell (cell = body + margin on every
            // side, body centred by the generator), so no extra alignment is needed here. The cursor lives on
            // atlasData so packing continues correctly as the dynamic atlas keeps adding glyphs across calls.
            var atlasWidth = (int)atlasData.AtlasSize.Width;
            var atlasHeight = (int)atlasData.AtlasSize.Height;

            foreach (var glyphData in textureData)
            {
                var w = (int)glyphData.FullGlyphSize.Width;
                var h = (int)glyphData.FullGlyphSize.Height;

                if (atlasData.PackX + w > atlasWidth)
                {
                    atlasData.PackX = 0;
                    atlasData.PackY += atlasData.ShelfHeight;
                    atlasData.ShelfHeight = 0;
                }

                // The shelf overflows this LAYER's height -> move to the next array layer (reset cursor + layer++). This
                // is what lets the atlas hold more than one 1024x1024 slice's worth of glyphs. The old code let PackY grow
                // past the texture (off-atlas UVs) and separately drove the upload Depth from the layer, which crashed the
                // GPU once a second layer appeared (>~256 glyphs).
                if (atlasData.PackY + h > atlasHeight)
                {
                    atlasData.AdvanceToNextLayer();
                }

                glyphData.BoundingRect.Left = atlasData.PackX;
                glyphData.BoundingRect.Top = atlasData.PackY;
                glyphData.DepthLayer = atlasData.CurrentDepthLayer;

                atlasData.PackX += w;
                if (h > atlasData.ShelfHeight)
                    atlasData.ShelfHeight = h;

                glyphData.CalculateUV(atlasData.AtlasSize);
            }
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
            var atlasSize = new Size(resultWidth, yOffset);
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

        public void CopyTextureDataToImage(IReadOnlyList<GlyphTextureData> textureData)
        {
            var atlasWidth = atlasData.AtlasSize.Width;
            var bytesPerPixel = 4;
            var atlasStride = atlasWidth * bytesPerPixel;
            var bytes = (ulong)(atlasData.AtlasSize.Width * atlasData.AtlasSize.Height * 4);
            atlasData.ImageData = new byte[bytes];
            foreach (var glyph in textureData)
            {
                var glyphPixels = glyph.Pixels;
                // Source bitmap is the FULL glyph (body + margin), not just the body BoundingRect. Copying by
                // the body size clipped off the margin ring - exactly where the colored MSDF background lives -
                // and (since the real row stride is FullGlyphSize.Width) misread the rows. Match the GPU upload
                // path (FontAtlas.ProcessTextureData), which uses FullGlyphSize.
                var glyphWidth = (int)glyph.FullGlyphSize.Width;
                var glyphHeight = (int)glyph.FullGlyphSize.Height;
                var glyphStride = glyphWidth * bytesPerPixel;

                var destPixelX = glyph.BoundingRect.X;
                var destPixelY = glyph.BoundingRect.Y;
        
                for (int y = 0; y < glyphHeight; ++y)
                {
                    var sourceIndex = y * glyphStride;

                    var destinationIndex = (int)((destPixelY + y) * atlasStride + (destPixelX * bytesPerPixel));

                    Buffer.BlockCopy(
                        src: glyphPixels, 
                        srcOffset: sourceIndex, 
                        dst: atlasData.ImageData, 
                        dstOffset: destinationIndex, 
                        count: glyphStride);
                }
            }
        }

        public IReadOnlyList<GlyphTextureData> GenerateTextureForGlyphs(IReadOnlyList<Glyph> glyphs)
        {
            if (glyphs == null || glyphs.Count == 0)
            {
                return [];
            }

            Parallel.ForEach(glyphs,
                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, GenerateTextureForGlyph);

            var data = atlasData.GetGlyphData(glyphs.Select(x=>x.Index).ToArray());
            // Shelf-packs each glyph and assigns its BoundingRect, DepthLayer (which array slice) and UV. The layer now
            // comes from the packer overflowing a slice, not a fixed 256 counter.
            CalculateTextureDataForAtlas(data);

            return data;
        }

        public FontAtlasData GenerateTextureAtlas()
        {
            if (parameters.GlyphCount <= 0)
            {
                return null;
            }

            Parallel.For((int)parameters.StartGlyphIndex, (int)(parameters.StartGlyphIndex + parameters.GlyphCount),
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
            glyph.Sample(parameters.SampleRate);

            GenerateTextureForGlyph(glyph);
        }

        private void GenerateTextureForGlyph(Glyph glyph)
        {
            glyph.CalculateEmRelatedMultipliers(font.UnitsPerEm);
            glyph.Sample(parameters.SampleRate);
            var textureData = glyph.GenerateDirectMSDF(parameters.MsdfTextureSize, parameters.PixelRange, font.UnitsPerEm, parameters.GlyphMargin);
            
            if (textureData == null) 
                return;
            
            atlasData.AddGlyphData(textureData);
        }

        private GlyphTextureData CalculateTextureDataForGlyph(uint glyphIndex, bool useProportionalSize = true)
        {
            typeface.GetGlyphByIndex(glyphIndex, out var glyph);
            glyph.Sample(parameters.SampleRate);
            var textureData = glyph.IsEmpty ? null : glyph.PrepareData(parameters.MsdfTextureSize, font.UnitsPerEm, parameters.GlyphMargin, useProportionalSize);
            
            if (textureData == null) 
                return null;
            
            atlasData.AddGlyphData(textureData);
            return textureData;
        }
    }
}