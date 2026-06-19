using System;
using Adamantium.Mathematics;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.TextureGeneration
{
    public class FontAtlasData
    {
        private object lockObject = new Object();
        
        public Size AtlasSize { get; set; }
        private List<GlyphTextureData> glyphData { get; }

        public IReadOnlyList<GlyphTextureData> GlyphData => glyphData.AsReadOnly();
        private Dictionary<uint, GlyphTextureData> glyphDataMap;
        public byte[] ImageData { get; set; }
        public byte[] FontData { get; set; }
        public string Name { get; set; }
        
        public uint GlyphTextureSize { get; }

        // Running cursor for tight shelf packing of glyph bitmaps (shelf height = tallest glyph on it).
        public int PackX { get; set; }
        public int PackY { get; set; }
        public int ShelfHeight { get; set; }

        public uint CurrentIndexInArray { get; private set; }
        
        public uint CurrentDepthLayer { get; private set; }
        
        public uint NextIndexInArray
        {
            get
            {
                var index = CurrentIndexInArray++;
                if (index > 255)
                {
                    CurrentIndexInArray = 0;
                    index = 0;
                    CurrentDepthLayer++;
                }
                return index;
            }
        }

        public FontAtlasData(uint glyphTextureSize)
        {
            GlyphTextureSize = glyphTextureSize;
            glyphData = new List<GlyphTextureData>();
            glyphDataMap = new Dictionary<uint, GlyphTextureData>();
            CurrentDepthLayer = 1;
        }

        public FontAtlasData(uint glyphTextureSize, Size atlasSize) : this(glyphTextureSize)
        {
            AtlasSize = atlasSize;
        }

        public void GenerateGlyphDataMap()
        {
            glyphDataMap = GlyphData.ToDictionary(x => x.GlyphIndex);
        }

        public void AddGlyphData(GlyphTextureData glyphTextureData)
        {
            lock (lockObject)
            {
                glyphData.Add(glyphTextureData);
                glyphDataMap[glyphTextureData.GlyphIndex] = glyphTextureData;
            }
        }

        public GlyphTextureData GetGlyphData(uint index)
        {
            glyphDataMap.TryGetValue(index, out var data);
            return data;
        }
        
        public GlyphTextureData[] GetGlyphData(params uint[] glyphIndices)
        {
            var datas = new List<GlyphTextureData>();
            for (int i = 0; i < glyphIndices.Length; i++)
            {
                if (glyphDataMap.TryGetValue(glyphIndices[i], out var data))
                {
                    datas.Add(data);
                }
            }
            
            return datas.ToArray();
        }

        public RectangleF GetUVCoordinatesForGlyph(uint index)
        {
            if (glyphDataMap.TryGetValue(index, out var data))
            {
                return data.UVRect;
            }

            return default;
        }

    }
}