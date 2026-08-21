using System.Collections.Generic;
using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Graphics.Core;

namespace Adamantium.Graphics.Fonts
{
    public static class FontAtlasStore
    {
        // Concurrent because more than one thread reaches it: text is laid out wherever the layout pass runs, and a
        // virtualizing panel measures its tiles across cores. Creating the atlas itself is still a GPU call and belongs
        // to the thread that owns the device.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<FontParameters, FontAtlas> _fontAtlasMap = new();
        /// <summary>Rasterize glyphs INLINE instead of on a worker. A live window can let text fill in over the next
        /// frames, because there are next frames; a ONE-SHOT render - a bitmap, a designer preview, an off-screen test -
        /// has only the frame it is asked for, and text missing from it is text missing for good. Those paths turn this
        /// on for the duration of the render.</summary>
        public static bool SynchronousFill { get; set; }

        public static FontAtlas GetOrCreateFrom(IGraphicsDevice graphicsDevice, Typeface typeface, FontParameters fontParameters)
        {
            return _fontAtlasMap.GetOrAdd(fontParameters, _ => new FontAtlas(graphicsDevice, typeface, fontParameters));
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

            if (landed) LandedVersion++;

            return landed;
        }

        /// <summary>Bumped every time letters land. There is more than one render cache - window content, the adorner
        /// stage, the popup stage - and the pump drains a QUEUE, so only the FIRST one to ask is told that something
        /// arrived; the rest get false and would never refresh their own text. A version they can each remember answers
        /// "did anything land since I last looked" for every one of them, independently of who did the pumping.
        /// <para>Without it a SlidePanel opened with a blank close cross the first time and a correct one the second,
        /// once the atlas was warm.</para></summary>
        public static int LandedVersion { get; private set; }

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
