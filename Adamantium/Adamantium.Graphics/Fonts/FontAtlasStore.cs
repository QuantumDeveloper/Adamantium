using System.Collections.Generic;
using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Graphics.Core;

namespace Adamantium.Graphics.Fonts
{
    public static class FontAtlasStore
    {
        private static Dictionary<FontParameters, FontAtlas> _fontAtlasMap;
        static FontAtlasStore()
        {
            _fontAtlasMap = new Dictionary<FontParameters, FontAtlas>();
        }
        /// <summary>Rasterize glyphs INLINE instead of on a worker. A live window can let text fill in over the next
        /// frames, because there are next frames; a ONE-SHOT render - a bitmap, a designer preview, an off-screen test -
        /// has only the frame it is asked for, and text missing from it is text missing for good. Those paths turn this
        /// on for the duration of the render.</summary>
        public static bool SynchronousFill { get; set; }

        public static FontAtlas GetOrCreateFrom(IGraphicsDevice graphicsDevice, Typeface typeface, FontParameters fontParameters)
        {
            if (!_fontAtlasMap.TryGetValue(fontParameters, out var atlas))
            {
                atlas = new FontAtlas(graphicsDevice, typeface, fontParameters);
                _fontAtlasMap.Add(fontParameters, atlas);
            }

            return atlas;
        }

        /// <summary>Upload every atlas's finished glyphs, on the thread that owns the device. Called once a frame by the
        /// renderer; true when something landed, which is the caller's cue that text built before it is out of date.</summary>
        public static bool PumpReadyGlyphs()
        {
            var landed = false;
            foreach (var atlas in _fontAtlasMap.Values)
            {
                landed |= atlas.PumpReady();
            }

            return landed;
        }

        /// <summary>Is any atlas still rasterizing? While this is true the renderer keeps asking for frames, so the
        /// glyphs that arrive have a frame to appear in.</summary>
        public static bool HasPendingGlyphs
        {
            get
            {
                foreach (var atlas in _fontAtlasMap.Values)
                {
                    if (atlas.HasPendingGlyphs) return true;
                }

                return false;
            }
        }
    }
}
