using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adamantium.Fonts.Common
{
    internal class FeatureCache
    {
        private Dictionary<FeatureInfo, GlyphCache> featureCache;

        public FeatureCache()
        {
            featureCache = new Dictionary<FeatureInfo, GlyphCache>();
        }

        public void AddToFeatureCache(FeatureInfo info, Glyph glyph, GlyphPosition positionData)
        {
            if (!featureCache.TryGetValue(info, out var cache))
            {
                featureCache[info] = new GlyphCache(glyph, new GlyphFeatureData(positionData));
            }
            else
            {
                cache.AddToGlyphCache(glyph, positionData);
            }
        }

        public bool IsGlyphCached(Feature feature, Glyph glyph)
        {
            if (!featureCache.ContainsKey(feature.Info)) return false;

            return featureCache[feature.Info].IsGlyphCached(glyph);
        }

        public void Clear()
        {
            foreach (var cache in featureCache)
            {
                cache.Value.Clear();
            }

            featureCache.Clear();
        }
    }
}
