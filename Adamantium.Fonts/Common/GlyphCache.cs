using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    internal class GlyphCache
    {
        private Dictionary<uint, FeatureCache> glyphCache;

        public GlyphCache()
        {
            glyphCache = new Dictionary<uint, FeatureCache>();
        }

        /// <summary>
        /// Positioning
        /// </summary>
        /// <param name="glyphIndex"></param>
        /// <param name="info"></param>
        /// <param name="position"></param>
        public void AddFeatureDataToGlyph(FeatureInfo info, uint glyphIndex, GlyphPosition position)
        {
            if (glyphCache.TryGetValue(glyphIndex, out var featureCache))
            {
                featureCache.AddToFeatureCache(info, position);
            }
            else
            {
                glyphCache[glyphIndex] = new FeatureCache(glyphIndex);
                glyphCache[glyphIndex].AddToFeatureCache(info, position);
            }
        }

        /// <summary>
        /// Substitution
        /// </summary>
        /// <param name="info"></param>
        /// /// <param name="glyph"></param>
        /// <param name="glyphs"></param>
        public void AddFeatureDataToGlyph(FeatureInfo info, uint glyph, params uint[] glyphs)
        {
            if (glyphCache.TryGetValue(glyph, out var featureCache))
            {
                featureCache.AddToFeatureCache(info, glyphs);
            }
            else
            {
                glyphCache[glyph] = new FeatureCache(glyph);
                glyphCache[glyph].AddToFeatureCache(info, glyphs);
            }
        }

        public void RemoveFeatureDataFromGlyph(FeatureInfo info, uint glyphIndex)
        {
            if (!glyphCache.ContainsKey(glyphIndex)) return;
            
            //var layout = glyphCache[glyphIndex].GetLayoutData(info);
        }

        public bool IsGlyphCached(uint glyphIndex)
        {
            return glyphCache.ContainsKey(glyphIndex);
        }

        public bool IsFeatureCached(FeatureInfo featureInfo, uint glyphIndex)
        {
            return glyphCache.ContainsKey(glyphIndex) && glyphCache[glyphIndex].IsFeatureCached(featureInfo);
        }

        // public GlyphLayoutData GetGlyphLayoutData(FeatureInfo info, uint glyphIndex)
        // {
        //     return glyphCache[glyphIndex].GetLayoutData(info);
        // }

        public void Clear()
        {
            glyphCache.Clear();
        }
    }
}
