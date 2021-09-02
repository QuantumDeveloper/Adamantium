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
        
        public FeatureParametersTable FeatureParameters { get; set; }
        
        public bool IsEnabled { get; set; }
        
        internal ILookupTable[] Lookups { get; set; }
        
        public void Apply(FontLanguage language, TypeFace typeface, Glyph glyph)
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
                            subTable.SubstituteGlyphs(language, Info, typeface, glyph.Index);
                            break;
                        case FeatureKind.GPOS:
                            subTable.PositionGlyph(language, Info, typeface, glyph.Index, 1);
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