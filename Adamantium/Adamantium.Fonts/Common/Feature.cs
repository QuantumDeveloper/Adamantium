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

        public void Apply(TypeFace typeface, uint glyphIndex)
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
                        case LookupOwnerType.GSUB:
                            subTable.SubstituteGlyphs(Info, typeface, glyphIndex);
                            break;
                        case LookupOwnerType.GPOS:
                            subTable.PositionGlyph(Info, typeface, glyphIndex, 1);
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