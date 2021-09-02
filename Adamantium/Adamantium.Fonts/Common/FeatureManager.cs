using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.Common
{
    public class FeatureManager
    {
        private List<Feature> gposFeatures;
        private List<Feature> gsubFeatures;
        private Dictionary<FeatureInfo, Feature> gposFeaturesMap;
        private Dictionary<FeatureInfo, Feature> gsubFeaturesMap;
        private List<FontLanguage> availableLanguages;
        private Dictionary<LanguageTag, FontLanguage> languagesMap;
        private List<Feature> availableFeatures;
        private Dictionary<FeatureInfo, Feature> featuresMap;
        private List<Feature> enabledFeatures;

        public FeatureManager()
        {
            gposFeatures = new List<Feature>();
            gsubFeatures = new List<Feature>();
            gposFeaturesMap = new Dictionary<FeatureInfo, Feature>();
            gsubFeaturesMap = new Dictionary<FeatureInfo, Feature>();

            availableLanguages = new List<FontLanguage>();
            languagesMap = new Dictionary<LanguageTag, FontLanguage>();

            availableFeatures = new List<Feature>();
            featuresMap = new Dictionary<FeatureInfo, Feature>();
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
            return featuresMap.TryGetValue(FeatureInfos.GetFeature(featureName), out feature);
        }

        internal void AddLanguage(FontLanguage fontLanguage)
        {
            if (languagesMap.ContainsKey(fontLanguage.Info)) return;
            
            availableLanguages.Add(fontLanguage);
            languagesMap[fontLanguage.Info] = fontLanguage;
        }

        // internal void SetFeatures(IEnumerable<Feature> inputFeatures, FeatureKind featureKind)
        // {
        //     switch (featureKind)
        //     {
        //         case FeatureKind.GPOS:
        //             gposFeatures.AddRange(inputFeatures);
        //             gposFeaturesMap = gposFeatures.ToDictionary(x => x.Info);
        //             break;
        //         case FeatureKind.GSUB:
        //             gsubFeatures.AddRange(inputFeatures);
        //             gsubFeaturesMap = gsubFeatures.ToDictionary(x => x.Info);
        //             break;
        //     }
        // }
        
        // public void Merge(IEnumerable<Feature> features, FeatureKind featureKind)
        // {
        //     SetFeatures(features, FeatureKind.GPOS);
        // }

        internal void AddFeature(Feature feature, FeatureKind kind)
        {
            switch (kind)
            {
                case FeatureKind.GPOS:
                    if (!gposFeaturesMap.ContainsKey(feature.Info))
                    {
                        gposFeatures.Add(feature);
                        gposFeaturesMap[feature.Info] = feature;
                    }

                    break;
                case FeatureKind.GSUB:
                    if (!gsubFeaturesMap.ContainsKey(feature.Info))
                    {
                        gsubFeatures.Add(feature);
                        gsubFeaturesMap[feature.Info] = feature;
                    }

                    break;
            }
        }

        public void EnableFeature(string featureName, bool enable)
        {
            var info = FeatureInfos.GetFeature(featureName);
            if (featuresMap.TryGetValue(info, out var featureItem))
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

        public bool IsFeatureEnabled(string featureName)
        {
            var info = FeatureInfos.GetFeature(featureName);
            if (featuresMap.TryGetValue(info, out var feature))
            {
                return enabledFeatures.Contains(feature);
            }

            return false;
        }
    }
}