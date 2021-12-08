using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.Common
{
    public class FeatureService
    {
        private List<Feature> gposFeatures;
        private List<Feature> gsubFeatures;
        private Dictionary<FeatureInfo, Feature> gposFeaturesMap;
        private Dictionary<FeatureInfo, Feature> gsubFeaturesMap;
        private List<FontLanguage> availableLanguages;
        private Dictionary<LanguageTag, FontLanguage> languagesMap;
        private List<Feature> availableFeatures;
        private Dictionary<FeatureInfo, Feature> availableFeaturesMap;
        private List<Feature> enabledFeatures;

        public FeatureService()
        {
            gposFeatures = new List<Feature>();
            gsubFeatures = new List<Feature>();
            gposFeaturesMap = new Dictionary<FeatureInfo, Feature>();
            gsubFeaturesMap = new Dictionary<FeatureInfo, Feature>();

            availableLanguages = new List<FontLanguage>();
            languagesMap = new Dictionary<LanguageTag, FontLanguage>();

            availableFeatures = new List<Feature>();
            availableFeaturesMap = new Dictionary<FeatureInfo, Feature>();
            enabledFeatures = new List<Feature>();
        }

        public IReadOnlyCollection<Feature> GPOSFeatures => gposFeatures.AsReadOnly();
        
        public IReadOnlyCollection<Feature> GSUBFeatures => gsubFeatures.AsReadOnly();

        public IReadOnlyCollection<Feature> AvailableFeatures => availableFeatures.AsReadOnly();

        public IReadOnlyCollection<Feature> EnabledFeatures => enabledFeatures.AsReadOnly();

        public Feature RequiredGPOSFeature { get; internal set; }
        
        public Feature RequiredGSUBFeature { get; internal set; }

        public IReadOnlyCollection<FontLanguage> AvailableLanguages => availableLanguages.AsReadOnly();

        internal bool TryGetLanguage(LanguageTag languageTag, out FontLanguage language)
        {
            return languagesMap.TryGetValue(languageTag, out language);
        }

        internal bool TryGetFeature(string featureName, out Feature feature)
        {
            return availableFeaturesMap.TryGetValue(FeatureInfos.GetFeature(featureName), out feature);
        }

        internal void AddLanguage(FontLanguage fontLanguage)
        {
            if (languagesMap.ContainsKey(fontLanguage.Info)) return;
            
            availableLanguages.Add(fontLanguage);
            languagesMap[fontLanguage.Info] = fontLanguage;
        }
        
        public bool IsLanguageAvailableByMsdnName(string language)
        {
            return languagesMap.ContainsKey(LanguageTags.GetMsdnLanguage(language));
        }

        public bool IsLanguageAvailableByIsoName(string language)
        {
            return languagesMap.ContainsKey(LanguageTags.GetIsoLanguage(language));
        }

        public bool IsLanguageAvailableByIsoName(string language, out FontLanguage fontLanguage)
        {
            if (languagesMap.TryGetValue(LanguageTags.GetIsoLanguage(language), out fontLanguage))
            {
                return true;
            }

            return false;
        }

        internal void AddFeature(Feature feature, FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.GPOS:
                    if (!gposFeaturesMap.ContainsKey(feature.Info))
                    {
                        gposFeatures.Add(feature);
                        gposFeaturesMap[feature.Info] = feature;
                        availableFeatures.Add(feature);
                        availableFeaturesMap[feature.Info] = feature;
                    }
                    break;
                case FeatureKind.GSUB:
                    if (!gsubFeaturesMap.ContainsKey(feature.Info))
                    {
                        gsubFeatures.Add(feature);
                        gsubFeaturesMap[feature.Info] = feature;
                        availableFeatures.Add(feature);
                        availableFeaturesMap[feature.Info] = feature;
                    }
                    break;
            }
        }

        public void EnableFeature(string featureName, bool enable)
        {
            var info = FeatureInfos.GetFeature(featureName);
            if (availableFeaturesMap.TryGetValue(info, out var featureItem))
            {
                featureItem.IsEnabled = enable;
                if (featureItem.IsEnabled && !enabledFeatures.Contains(featureItem))
                {
                    enabledFeatures.Add(featureItem);
                }
                else
                {
                    enabledFeatures.Remove(featureItem);
                }
            }
        }
        
        public bool ApplyFeature(string featureName, GlyphLayoutContainer container, uint index, uint length)
        {
            if (TryGetFeature(featureName, out var feature))
            {
                EnableFeature(featureName, true);
                feature.Apply(container, index, length);
                //@TODO check how to properly return if the feature is actually applied 
                return true;
            }

            return false;
        }

        public bool IsFeatureEnabled(string featureName)
        {
            var info = FeatureInfos.GetFeature(featureName);
            if (availableFeaturesMap.TryGetValue(info, out var feature))
            {
                return enabledFeatures.Contains(feature);
            }

            return false;
        }
    }
}