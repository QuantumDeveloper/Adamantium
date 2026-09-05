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
            var atlas = _fontAtlasMap.GetOrAdd(fontParameters, _ => new FontAtlas(graphicsDevice, typeface, fontParameters));
            ReportSharedAtlas(typeface, fontParameters, atlas);
            return atlas;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<FontParameters, Typeface> _atlasOwner = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _reportedSharing = new();

        // The key of the map above is the RASTERIZATION settings and nothing else - no typeface. Every font in the
        // application asks with FontParameters.Default, so a second typeface is handed the FIRST one's atlas and then
        // looks its glyphs up BY INDEX, where the same number means a different letter. This says so out loud, once per
        // pair, before anything is changed: the shape of the corruption (right advances, wrong letters, and different
        // letters between runs depending on who asked first) matches, and a matching shape is not a measurement.
        private static void ReportSharedAtlas(Typeface typeface, FontParameters parameters, FontAtlas atlas)
        {
            var owner = _atlasOwner.GetOrAdd(parameters, typeface);
            if (ReferenceEquals(owner, typeface)) return;

            var key = $"{Describe(owner)} -> {Describe(typeface)}";
            if (!_reportedSharing.TryAdd(key, 0)) return;

            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.AppContext.BaseDirectory, "glyph-probe.log"),
                    $"[FONT-ATLAS] {System.DateTime.Now:HH:mm:ss} SHARED ATLAS: it was created for '{Describe(owner)}' " +
                    $"and is now being handed to '{Describe(typeface)}' - same FontParameters, and the typeface is not " +
                    $"part of the key. Glyphs are addressed by INDEX, so this one reads the other font's pictures." +
                    System.Environment.NewLine);
            }
            catch
            {
            }
        }

        private static string Describe(Typeface typeface)
        {
            if (typeface == null) return "<null>";
            var font = typeface.Fonts is { Count: > 0 } ? typeface.Fonts[0] : null;
            return font?.FullName ?? typeface.GetType().Name + "#" + typeface.GetHashCode().ToString("x4");
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
