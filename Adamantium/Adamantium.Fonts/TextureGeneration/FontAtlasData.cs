using System;
using Adamantium.Mathematics;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.TextureGeneration
{
    [MessagePackObject]
    public class FontAtlasData
    {
        private object lockObject = new Object();
        
        [Key(0)]
        public Size AtlasSize { get; set; }
        [Key(1)]
        private List<GlyphTextureData> glyphData { get; }

        public IReadOnlyList<GlyphTextureData> GlyphData => glyphData.AsReadOnly();
        [IgnoreMember] 
        private Dictionary<uint, GlyphTextureData> glyphDataMap;
        [Key(2)]
        public byte[] ImageData { get; set; }
        [Key(3)]
        public byte[] FontData { get; set; }
        [Key(4)]
        public string Name { get; set; }
        
        public uint GlyphTextureSize { get; }

        public FontAtlasData(uint glyphTextureSize)
        {
            GlyphTextureSize = glyphTextureSize;
            glyphData = new List<GlyphTextureData>();
            glyphDataMap = new Dictionary<uint, GlyphTextureData>();
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

        public static FontAtlasData Load(byte[] data)
        {
            var resolver = CompositeResolver.Create(
               new IMessagePackFormatter[] { TypelessFormatter.Instance },
               new IFormatterResolver[] { StandardResolverAllowPrivate.Instance });

            var options = MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(resolver);

            var instance = MessagePackSerializer.Deserialize<FontAtlasData>(data, options);
            instance.GenerateGlyphDataMap();
            return instance;
        }

        public byte[] Save()
        {
            var resolver = CompositeResolver.Create(
               new IMessagePackFormatter[] { TypelessFormatter.Instance },
               new IFormatterResolver[] { StandardResolverAllowPrivate.Instance });

            var options = MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(resolver);

            var compressedData = MessagePackSerializer.Serialize(this, options);

            return compressedData;
        }
    }
}