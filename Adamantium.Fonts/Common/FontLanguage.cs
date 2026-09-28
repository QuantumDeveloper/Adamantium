using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.Common
{
    public class FontLanguage
    {
        private List<Feature> gposFeatures;
        private List<Feature> gsubFeatures;
        private Dictionary<FeatureInfo, Feature> gposFeaturesMap;
        private Dictionary<FeatureInfo, Feature> gsubFeaturesMap;

        public FontLanguage(LanguageTag tag)
        {
            Info = tag;
            gposFeatures = new List<Feature>();
            gsubFeatures = new List<Feature>();
            gposFeaturesMap = new Dictionary<FeatureInfo, Feature>();
            gsubFeaturesMap = new Dictionary<FeatureInfo, Feature>();
        }
        
        public LanguageTag Info { get; }
        
        public IReadOnlyCollection<Feature> GPOSFeatures => gposFeatures.AsReadOnly();
        
        public IReadOnlyCollection<Feature> GSUBFeatures => gsubFeatures.AsReadOnly();
        
        public Feature RequiredGPOSFeature { get; internal set; }
        
        public Feature RequiredGSUBFeature { get; internal set; }

        internal void SetFeatures(IEnumerable<Feature> inputFeatures, FeatureKind featureKind)
        {
            switch (featureKind)
            {
                case FeatureKind.GPOS:
                    gposFeatures.AddRange(inputFeatures);
                    gposFeaturesMap = gposFeatures.ToDictionary(x => x.Info);
                    break;
                case FeatureKind.GSUB:
                    gsubFeatures.AddRange(inputFeatures);
                    gsubFeaturesMap = gsubFeatures.ToDictionary(x => x.Info);
                    break;
            }
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

        public void Merge(FontLanguage language)
        {
            SetFeatures(language.gposFeatures, FeatureKind.GPOS);
            SetFeatures(language.gsubFeatures, FeatureKind.GSUB);
        }

        public override string ToString()
        {
            return $"Short name: {Info.Tag}, Friendly name: {Info.FriendlyName}";
        }
    }
}