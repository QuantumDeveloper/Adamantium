using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    internal class FeatureCache
    {
        private readonly Dictionary<FeatureInfo, GlyphLayoutData> featureCache;
        public uint GlyphIndex { get; }
        
        public GlyphLayoutData Layout { get; }

        public FeatureCache(uint glyphIndex)
        {
            GlyphIndex = glyphIndex;
            featureCache = new Dictionary<FeatureInfo, GlyphLayoutData>();
            Layout = new GlyphLayoutData();
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

            Layout.Position += positionData;
        }

        public void RemoveFeatureData(FeatureInfo featureInfo)
        {
            if (featureCache.TryGetValue(featureInfo, out var data))
            {
                Layout.SubtractData(data);
            }
        }
        
        public void AddToFeatureCache(FeatureInfo featureInfo, params uint[] glyphs)
        {
            if (featureCache.TryGetValue(featureInfo, out var data))
            {
                data.AppendGlyphs(glyphs);
            }
            else
            {
                featureCache[featureInfo] = new GlyphLayoutData(glyphs);
            }
            
            Layout.AppendGlyphs(glyphs);
        }

        public void AddToFeatureCache(FeatureInfo featureInfo, params ushort[] glyphs)
        {
            if (featureCache.TryGetValue(featureInfo, out var data))
            {
                data.AppendGlyphs(glyphs);
            }
            else
            {
                featureCache[featureInfo] = new GlyphLayoutData(glyphs);
            }
            
            Layout.AppendGlyphs(glyphs);
        }

        public bool IsFeatureCached(FeatureInfo featureInfo)
        {
            return featureCache.ContainsKey(featureInfo);
        }

        public GlyphLayoutData GetLayoutDataForFeature(FeatureInfo featureInfo)
        {
            return featureCache[featureInfo];
        }

        public void Clear()
        {
            featureCache.Clear();
        }
    }
}
