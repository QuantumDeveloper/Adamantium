using System.Collections.Generic;
using Adamantium.Fonts;

namespace Adamantium.Engine.Graphics.Fonts
{
    public static class FontAtlasStore
    {
        private static Dictionary<FontParameters, FontAtlas> _fontAtlasMap;
        static FontAtlasStore()
        {
            _fontAtlasMap = new Dictionary<FontParameters, FontAtlas>();
        }
        public static FontAtlas GetOrCreateFrom(GraphicsDevice graphicsDevice, Typeface typeface, FontParameters fontParameters)
        {
            if (!_fontAtlasMap.TryGetValue(fontParameters, out var atlas))
            {
                atlas = new FontAtlas(graphicsDevice, typeface, fontParameters);
                _fontAtlasMap.Add(fontParameters, atlas);
            }

            return atlas;
        }
    }

    
}
