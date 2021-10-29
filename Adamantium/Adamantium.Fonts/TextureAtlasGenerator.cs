using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Mathematics;
using Adamantium.UI;
using Color = Adamantium.Mathematics.Color;

namespace Adamantium.Fonts
{
    public enum GeneratorType
    {
        Msdf,
        Subpixel
    }

    public class TextureAtlasGenerator
    {
        private Color[,] rawAtlas;
        private List<Color> atlas;
        private Size atlasDimensions;
        private Size atlasSize;
        private uint glyphTextureSize;
        private byte sampleRate;
        private TypeFace typeFace;
        private IFont font;
        private GeneratorType generatorType;
        private int startGlyphIndex;

        public TextureAtlasData GenerateTextureAtlas(TypeFace typeFace, IFont font, uint glyphTextureSize, byte sampleRate, int startGlyphIndex, int glyphCount, GeneratorType generatorType)
        {
            if (startGlyphIndex < 0 || glyphCount <= 0)
            {
                return default;
            }
            
            this.glyphTextureSize = glyphTextureSize;
            this.sampleRate = sampleRate;
            this.typeFace = typeFace;
            this.font = font;
            this.generatorType = generatorType;
            this.startGlyphIndex = startGlyphIndex;
            
            atlasDimensions = CalculateAtlasDimensions(glyphCount);
            atlasSize = new Size(atlasDimensions.Width * glyphTextureSize, atlasDimensions.Height * glyphTextureSize);
         
            atlas = new List<Color>();
            rawAtlas = new Color[(uint)atlasSize.Width, (uint)atlasSize.Height];
                
            Console.Write("[");
            Parallel.For(startGlyphIndex, startGlyphIndex + glyphCount, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, GenerateTextureForGlyph);
            Console.Write("]\n");

            for (var y = 0; y < atlasSize.Height; y++)
            {
                for (var x = 0; x < atlasSize.Width; x++)
                {
                    atlas.Add(rawAtlas[x, y]);
                }
            }

            var data = new TextureAtlasData(atlas.ToArray(), atlasSize);
            return data;
        }

        private void GenerateTextureForGlyph(int glyphIndex)
        {
            typeFace.GetGlyphByIndex((uint)glyphIndex, out var glyph);
            var em = font.UnitsPerEm;

            var relativeGlyphIndex = glyphIndex - startGlyphIndex;

            var startX = (relativeGlyphIndex % (int)atlasDimensions.Width) * glyphTextureSize;
            var startY = (relativeGlyphIndex / (int)atlasDimensions.Width) * glyphTextureSize;

            glyph.Sample(sampleRate);

            Color[,] rawGlyph;
            
            if (generatorType == GeneratorType.Msdf)
            {
                rawGlyph = glyph.GenerateDirectMSDF(glyphTextureSize, em);
            }
            else
            {
                rawGlyph = glyph.RasterizeGlyphBySubpixels(glyphTextureSize, em);
            }

            for (var y = 0; y < glyphTextureSize; y++)
            {
                for (var x = 0; x < glyphTextureSize; x++)
                {
                    var atlasPixelX = x + startX;
                    var atlasPixelY = y + startY;
                    rawAtlas[atlasPixelX, atlasPixelY] = rawGlyph[x, y];
                }
            }

            Console.Write(".");
        }

        private Size CalculateAtlasDimensions(int glyphCount)
        {
            var glyphsPerRow = (uint)Math.Ceiling(Math.Sqrt(glyphCount));
            var glyphsPerColumn = (uint)Math.Ceiling((double)glyphCount / glyphsPerRow);

            return new Size(glyphsPerRow, glyphsPerColumn);
        }
    }
}