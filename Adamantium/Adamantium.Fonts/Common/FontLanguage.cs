using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    public class FontLanguage
    {
        private List<Feature> features;

        public FontLanguage(LanguageTag tag)
        {
            Info = tag;
            features = new List<Feature>();
            FriendlyName = tag.FriendlyName;
            ShortName = tag.Tag;
            ISONames = tag.ISONames;
        }
        
        public LanguageTag Info { get; }
        
        public string FriendlyName { get; }
        
        public string ShortName { get; }
        
        public string[] ISONames { get; }
        
        public IReadOnlyCollection<Feature> Features => features.AsReadOnly();
        
        public Feature RequiredFeature { get; internal set; }

        internal void SetFeatures(IEnumerable<Feature> inputFeatures)
        {
            features.Clear();
            features.AddRange(inputFeatures);
        }

        internal void AddFeature(Feature feature)
        {
            features.Add(feature);
        }

        public override string ToString()
        {
            return $"Short name: {ShortName}, Friendly name: {FriendlyName}";
        }
    }
}