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
        [Key(0)]
        public Size AtlasSize { get; set; }
        [Key(1)]
        public List<GlyphTextureData> GlyphData { get; }
        [IgnoreMember]
        public Dictionary<uint, GlyphTextureData> GlyphDataMap { get; private set; }
        [Key(2)]
        public byte[] ImageData { get; set; }
        [Key(3)]
        public byte[] FontData { get; set; }
        [Key(4)]
        public string Name { get; set; }

        public FontAtlasData()
        {
            GlyphData = new List<GlyphTextureData>();
        }

        public void GenerateGlyphDataMap()
        {
            GlyphDataMap = GlyphData.ToDictionary(x => x.GlyphIndex);
        }

        public RectangleF GetTextureAtlasUVCoordinates(uint glyphIndex)
        {
            if (GlyphDataMap.TryGetValue(glyphIndex, out var value))
            {
                return value.UV;
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