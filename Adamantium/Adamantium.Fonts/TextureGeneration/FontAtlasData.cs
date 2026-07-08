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

        // Which array-texture LAYER glyphs are currently packed into (0-based). Each layer is one AtlasSize slice; the
        // shelf packer advances to the next layer (AdvanceToNextLayer) when a glyph won't fit in the current one's height.
        public uint CurrentDepthLayer { get; private set; }

        // Number of array layers the atlas texture has. Packing never advances past the last one.
        public uint LayerCount { get; }

        // Set once packing has had to clamp to the last layer (further glyphs overwrite it). The caller can warn; the old
        // behaviour instead wrote layer 2 into a 1-layer 2D image, which crashed the GPU past ~256 glyphs.
        public bool LayersExhausted { get; private set; }

        // Move packing to the next array layer: reset the shelf cursor and bump the layer, clamped at the last layer.
        public void AdvanceToNextLayer()
        {
            PackX = 0;
            PackY = 0;
            ShelfHeight = 0;
            if (CurrentDepthLayer + 1 < LayerCount)
                CurrentDepthLayer++;
            else
                LayersExhausted = true;
        }

        public FontAtlasData(uint glyphTextureSize, uint layerCount = 1)
        {
            GlyphTextureSize = glyphTextureSize;
            glyphData = new List<GlyphTextureData>();
            glyphDataMap = new Dictionary<uint, GlyphTextureData>();
            LayerCount = Math.Max(1, layerCount);
            CurrentDepthLayer = 0;
        }

        public FontAtlasData(uint glyphTextureSize, Size atlasSize, uint layerCount = 1) : this(glyphTextureSize, layerCount)
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