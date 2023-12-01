using Adamantium.Mathematics;
using MessagePack;
using System.Collections.Generic;

namespace Adamantium.Fonts.TextureGeneration
{
    [MessagePackObject]
    public class FontAtlasData
    {
        [Key(0)]
        public Size AtlasSize { get; set; }
        [Key(2)]
        public List<GlyphTextureData> GlyphData { get; }
        [Key(3)]
        public byte[] ImageData { get; set; }
        [Key(4)]
        public byte[] FontData { get; set; }
        [Key(5)]
        public string Name { get; set; }

        internal FontAtlasData()
        {
            GlyphData = new List<GlyphTextureData>();
        }
    }
}