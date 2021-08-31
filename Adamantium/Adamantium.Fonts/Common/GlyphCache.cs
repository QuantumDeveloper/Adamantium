using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    internal class GlyphCache
    {
        //private Dictionary<Glyph, GlyphFeatureData> glyphCache;
        private Dictionary<Glyph, FeatureCache> glyphCache;

        public GlyphCache(Glyph glyph, GlyphLayoutData data)
        {
            //glyphCache = new Dictionary<Glyph, GlyphFeatureData>();
            glyphCache = new Dictionary<Glyph, FeatureCache>();

            //glyphCache[glyph] = data;
        }

        /// <summary>
        /// Positioning
        /// </summary>
        /// <param name="glyph"></param>
        /// <param name="info"></param>
        /// <param name="position"></param>
        public void AddFeatureDataToGlyph(FeatureInfo info, Glyph glyph, GlyphPosition position)
        {
            if (glyphCache.TryGetValue(glyph, out var featureCache))
            {
                featureCache.AddToFeatureCache(info, position);
            }
            else
            {
                glyphCache[glyph] = new FeatureCache(glyph);
                glyphCache[glyph].AddToFeatureCache(info, position);
            }

            glyph.Layout.Position += position;
        }

        /// <summary>
        /// Substitution
        /// </summary>
        /// <param name="glyph"></param>
        /// <param name="info"></param>
        /// <param name="glyphs"></param>
        public void AddFeatureDataToGlyph(FeatureInfo info, Glyph glyph, params Glyph[] glyphs)
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

            glyph.Layout.AppendGlyphs(glyphs);
        }

        public void RemoveFeatureDataFromGlyph(FeatureInfo info, Glyph glyph)
        {
            if (!glyphCache.ContainsKey(glyph)) return;
            
            var layout = glyphCache[glyph].GetLayoutData(info);
            glyph.Layout.SubtractData(layout);
        }

        public bool IsGlyphCached(Glyph glyph)
        {
            return glyphCache.ContainsKey(glyph);
        }

        public bool IsFeatureCached(Glyph glyph, FeatureInfo featureInfo)
        {
            return glyphCache.ContainsKey(glyph) && glyphCache[glyph].IsFeatureCached(featureInfo);
        }

        public GlyphLayoutData GetGlyphLayoutData(FeatureInfo info, Glyph glyph)
        {
            return glyphCache[glyph].GetLayoutData(info);
        }

        public void Clear()
        {
            glyphCache.Clear();
        }
    }
}
