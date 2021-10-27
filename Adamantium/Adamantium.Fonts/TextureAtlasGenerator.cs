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
        private TypeFace typeFace;
        private IFont font;
        private GeneratorType generatorType;
        private int startGlyphIndex;

        public TextureAtlasData GenerateTextureAtlas(TypeFace typeFace, IFont font, uint glyphTextureSize, int startGlyphIndex, int glyphCount, GeneratorType generatorType)
        {
            this.glyphTextureSize = glyphTextureSize;
            this.typeFace = typeFace;
            this.font = font;
            this.generatorType = generatorType;
            this.startGlyphIndex = startGlyphIndex;
            
            atlasDimensions = CalculateAtlasDimensions(glyphCount);
            atlasSize = new Size(atlasDimensions.Width * glyphTextureSize, atlasDimensions.Height * glyphTextureSize);
         
            atlas = new List<Color>();
            rawAtlas = new Color[(uint)atlasSize.Width, (uint)atlasSize.Height];
                
            Console.Write("[");
            Parallel.For(startGlyphIndex, startGlyphIndex + glyphCount, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, GenerateTextureAtlas);
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

        private void GenerateTextureAtlas(int glyphIndex)
        {
            typeFace.GetGlyphByIndex((uint)glyphIndex, out var glyph);
            var em = font.UnitsPerEm;

            var relativeGlyphIndex = glyphIndex - startGlyphIndex;

            var startX = (relativeGlyphIndex % (int)atlasDimensions.Width) * glyphTextureSize;
            var startY = (relativeGlyphIndex / (int)atlasDimensions.Width) * glyphTextureSize;

            var UVStart = new Vector2D(startX / atlasSize.Width, startY / atlasSize.Height);
            var UVEnd   = new Vector2D((startX + glyphTextureSize) / atlasSize.Width, (startY + glyphTextureSize) / atlasSize.Height);

            glyph.MsdfAtlasUV = new GlyphUVCoordinates() { Start = UVStart, End = UVEnd};

            glyph.Sample(10);

            var rawGlyph = new Color[glyphTextureSize, glyphTextureSize];
            
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

        private Size CalculateAtlasDimensions(int glyphCnt)
        {
            var glyphsPerRow = (uint)Math.Ceiling(Math.Sqrt(glyphCnt));
            var glyphsPerColumn = (uint)Math.Ceiling((double)glyphCnt / glyphsPerRow);

            return new Size(glyphsPerRow, glyphsPerColumn);
        }
    }
}