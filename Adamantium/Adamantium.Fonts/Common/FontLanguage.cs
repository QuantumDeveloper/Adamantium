using System.Collections.Generic;

namespace Adamantium.Fonts.Common
{
    public class FontLanguage
    {
        private List<Feature> features;

        public FontLanguage(string shortName, string friendlyName)
        {
            ShortName = shortName;
            FriendlyName = friendlyName;
        }
        
        public string FriendlyName { get; private set; }
        
        public string ShortName { get; private set; }

        public IReadOnlyCollection<Feature> Features => features.AsReadOnly();
        
        public Feature RequiredFeature { get; internal set; }

        internal void SetFeatures(IEnumerable<Feature> features)
        {
            this.features = this.features;
        }
    }
}