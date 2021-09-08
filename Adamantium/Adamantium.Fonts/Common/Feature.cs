using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Common
{
    public class Feature
    {
        public Feature(FeatureInfo featureInfo)
        {
            Info = featureInfo;
        }
        
        public FeatureInfo Info { get; }
        
        public FeatureParametersTable FeatureParameters { get; internal set; }
        
        public bool IsEnabled { get; set; }
        
        internal ILookupTable[] Lookups { get; set; }
        
        public void Apply(GlyphLayoutContainer container, uint index, uint length)
        {
            if (!IsEnabled)
            {
                return;
            }
            
            foreach (var lookup in Lookups)
            {
                foreach (var subTable in lookup.SubTables)
                {
                    switch (subTable.OwnerType)
                    {
                        case FeatureKind.GSUB:
                            subTable.SubstituteGlyphs(container, Info, index, length);
                            break;
                        case FeatureKind.GPOS:
                            subTable.PositionGlyph(container, Info, index, length);
                            break;
                    }
                }
            }
        }

        public override string ToString()
        {
            return $"Short name: {Info.Tag}, Friendly name: {Info.FriendlyName}";
        }
    }
}