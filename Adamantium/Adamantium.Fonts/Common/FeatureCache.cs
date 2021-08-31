using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    internal class FeatureCache
    {
        private readonly Dictionary<FeatureInfo, GlyphLayoutData> featureCache;
        private readonly Glyph glyph;

        public FeatureCache(Glyph glyph)
        {
            this.glyph = glyph;
            featureCache = new Dictionary<FeatureInfo, GlyphLayoutData>();
        }

        public void AddToFeatureCache(FeatureInfo featureInfo, GlyphPosition positionData)
        {
            if (featureCache.TryGetValue(featureInfo, out var data))
            {
                data.Position = positionData;
            }
            else
            {
                featureCache[featureInfo] = new GlyphLayoutData(positionData);
            }
        }
        
        public void AddToFeatureCache(FeatureInfo featureInfo, params Glyph[] glyphs)
        {
            if (featureCache.TryGetValue(featureInfo, out var data))
            {
                data.AppendGlyphs(glyphs);
            }
            else
            {
                featureCache[featureInfo] = new GlyphLayoutData(glyphs);
            }
        }

        public bool IsFeatureCached(FeatureInfo featureInfo)
        {
            return featureCache.ContainsKey(featureInfo);
        }

        public GlyphLayoutData GetLayoutData(FeatureInfo featureInfo)
        {
            return featureCache[featureInfo];
        }

        public void Clear()
        {
            featureCache.Clear();
        }
    }
}
